"use strict";

const $ = (id) => document.getElementById(id);

const state = {
    config: null,
    polling: false,
    timer: null,
    lastLogCount: 0,
    browseTarget: null,
    browsePath: "",
    saveTimer: null,
};

/* ---------------- 通用 ---------------- */

function esc(s) {
    return String(s == null ? "" : s)
        .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

async function api(path, options) {
    const res = await fetch(path, options);
    if (!res.ok) {
        const text = await res.text();
        throw new Error(text || ("HTTP " + res.status));
    }
    return res.json();
}

let toastTimer = null;
function toast(msg) {
    const el = $("toast");
    el.textContent = msg;
    el.classList.remove("hidden");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.add("hidden"), 3500);
}

function pushLogLine(text) {
    const el = $("log");
    const div = document.createElement("div");
    if (/\[ERROR\]/.test(text)) div.className = "err";
    else if (/\[WARN\]/.test(text)) div.className = "warn";
    div.textContent = text;
    el.appendChild(div);
    el.scrollTop = el.scrollHeight;
}

function renderLog(log) {
    const el = $("log");
    if (log.length < state.lastLogCount) {
        el.innerHTML = "";
        state.lastLogCount = 0;
    }
    for (let i = state.lastLogCount; i < log.length; i++) pushLogLine(log[i]);
    state.lastLogCount = log.length;
    el.scrollTop = el.scrollHeight;
}

function clearLog() { $("log").innerHTML = ""; state.lastLogCount = 0; }

function fmtBytes(n) {
    if (!n) return "0 B";
    if (n >= 1073741824) return (n / 1073741824).toFixed(2) + " GB";
    if (n >= 1048576) return (n / 1048576).toFixed(1) + " MB";
    if (n >= 1024) return (n / 1024).toFixed(0) + " KB";
    return n + " B";
}

/* ---------------- 配置 ---------------- */

function applyConfig(cfg) {
    if (!cfg) return;
    $("sourceFolder").value = cfg.sourceFolder || "";
    $("outputFolder").value = cfg.outputFolder || "";
    $("quality").value = String(cfg.quality ?? 90);
    $("extensions").value = cfg.extensions || ".png,.jpg,.jpeg,.bmp,.gif";
    $("lossless").checked = cfg.lossless !== false;
    $("recursive").checked = !!cfg.recursive;
    $("overwrite").checked = !!cfg.overwrite;
    $("deleteSource").checked = !!cfg.deleteSource;
    refreshDots();
}

function collectConfig() {
    const q = parseInt($("quality").value.trim(), 10);
    return {
        sourceFolder: $("sourceFolder").value.trim(),
        outputFolder: $("outputFolder").value.trim(),
        quality: isNaN(q) ? 90 : Math.min(100, Math.max(1, q)),
        extensions: $("extensions").value.trim(),
        lossless: $("lossless").checked,
        recursive: $("recursive").checked,
        overwrite: $("overwrite").checked,
        deleteSource: $("deleteSource").checked,
    };
}

async function loadConfig() {
    state.config = await api("/api/config");
    applyConfig(state.config);
}

async function saveConfig(silent) {
    try {
        state.config = await api("/api/config", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(collectConfig()),
        });
        refreshDots();
        if (!silent) toast("设置已保存");
    } catch (e) {
        toast("保存设置失败: " + e.message);
    }
}

function scheduleSave() {
    clearTimeout(state.saveTimer);
    state.saveTimer = setTimeout(() => saveConfig(true), 500);
}

function setDot(id, ok) {
    const el = $("dot-" + id);
    if (!el) return;
    el.classList.remove("ok", "bad");
    el.classList.add(ok ? "ok" : "bad");
}

function refreshDots() {
    setDot("sourceFolder", $("sourceFolder").value.trim().length > 0);
    setDot("outputFolder", true);
    const q = parseInt($("quality").value.trim(), 10);
    setDot("quality", !isNaN(q) && q >= 1 && q <= 100);
    setDot("extensions", $("extensions").value.trim().length > 0);
}

/* ---------------- 扫描 ---------------- */

async function scan() {
    const btn = $("btnScan");
    btn.disabled = true;
    try {
        await saveConfig(true);
        const r = await api("/api/scan", { method: "POST" });
        $("scanInfo").textContent = r.count > 0
            ? `${r.message}  合计 ${fmtBytes(r.totalBytes)}`
            : (r.message || "");
        if (r.count > 0) {
            $("scanListWrap").classList.remove("hidden");
            $("scanList").innerHTML = (r.files || []).map(f => `<div>${esc(f)}</div>`).join("");
        } else {
            $("scanListWrap").classList.add("hidden");
            $("scanList").innerHTML = "";
        }
        if (r.count === 0) toast(r.message || "未找到匹配的图片");
    } catch (e) {
        toast("扫描失败: " + e.message);
    } finally {
        btn.disabled = false;
    }
}

/* ---------------- 转换 ---------------- */

function setBadge(text, cls) {
    const el = $("stateBadge");
    el.textContent = text;
    el.className = "badge " + cls;
}

async function convert() {
    const btn = $("btnConvert");
    btn.disabled = true;
    clearLog();
    try {
        await saveConfig(true);
        const r = await api("/api/convert", { method: "POST" });
        if (!r.ok) {
            toast(r.message || "无法开始转换");
            btn.disabled = false;
            return;
        }
        $("btnCancel").disabled = false;
        setBadge("运行中", "badge-blue");
        startPolling();
    } catch (e) {
        toast("开始转换失败: " + e.message);
        btn.disabled = false;
    }
}

async function cancel() {
    try { await api("/api/cancel", { method: "POST" }); }
    catch (e) { toast("取消失败: " + e.message); }
}

function startPolling() {
    if (state.polling) return;
    state.polling = true;
    state.timer = setInterval(poll, 800);
    poll();
}

async function poll() {
    let s;
    try { s = await api("/api/status"); }
    catch (e) { return; }

    renderLog(s.log || []);
    $("progressBar").style.width = (s.percent || 0) + "%";
    $("lblProgress").textContent = s.message || "";
    $("runInfo").textContent = s.total ? `已完成 ${s.done}/${s.total}` : "";

    if (s.sourceBytes > 0) {
        const saved = 100 - (s.webpBytes / s.sourceBytes * 100);
        $("sizeInfo").textContent =
            `体积 ${fmtBytes(s.sourceBytes)} → ${fmtBytes(s.webpBytes)}（节省 ${saved.toFixed(1)}%）`;
    }

    if (s.running) return;

    clearInterval(state.timer);
    state.polling = false;
    $("btnConvert").disabled = false;
    $("btnCancel").disabled = true;

    if (s.cancelled) setBadge("已取消", "badge-red");
    else if (s.error) setBadge("出错", "badge-red");
    else if (s.result) setBadge("已完成", "badge-green");

    if (s.result) toast(s.result);
    else if (s.error) toast("转换出错: " + s.error);
}

/* ---------------- 目录浏览 ---------------- */

function openBrowse(target) {
    state.browseTarget = target;
    $("browseModal").classList.remove("hidden");
    browseGo($(target).value.trim());
}

async function browseGo(path) {
    try {
        const d = await api(`/api/fs?path=${encodeURIComponent(path || "")}`);
        state.browsePath = d.path || "";
        $("browsePath").textContent = d.path || "（此电脑）";
        $("browseUp").disabled = !d.parent;

        const parts = [];
        (d.drives || []).forEach(f => parts.push(renderBrowseItem(f, true)));
        (d.directories || []).forEach(f => parts.push(renderBrowseItem(f, false)));
        if (parts.length === 0) parts.push('<div class="browse-item muted">（空）</div>');
        $("browseList").innerHTML = parts.join("");
    } catch (e) {
        toast("读取目录失败: " + e.message);
    }
}

function renderBrowseItem(f, isDrive) {
    const icon = isDrive ? "💽" : "📁";
    return `<div class="browse-item" data-dir="${esc(f.fullPath)}"><span>${icon}</span><span>${esc(f.name)}</span></div>`;
}

function closeBrowse() { $("browseModal").classList.add("hidden"); }

/* ---------------- 事件绑定 ---------------- */

function bind() {
    ["sourceFolder", "outputFolder", "quality", "extensions"].forEach(id => {
        $(id).addEventListener("change", scheduleSave);
        $(id).addEventListener("input", refreshDots);
    });
    ["recursive", "overwrite", "deleteSource"].forEach(id => {
        $(id).addEventListener("change", scheduleSave);
    });

    $("btnSaveConfig").addEventListener("click", () => saveConfig(false));
    $("btnScan").addEventListener("click", scan);
    $("btnConvert").addEventListener("click", convert);
    $("btnCancel").addEventListener("click", cancel);
    $("btnClearLog").addEventListener("click", clearLog);

    document.querySelectorAll("[data-browse]").forEach(btn => {
        btn.addEventListener("click", () => openBrowse(btn.getAttribute("data-browse")));
    });

    $("browseClose").addEventListener("click", closeBrowse);
    $("browseModal").addEventListener("click", (e) => { if (e.target === $("browseModal")) closeBrowse(); });
    $("browseUp").addEventListener("click", () => {
        const parts = state.browsePath.replace(/[\\/]+$/, "").split(/[\\/]/);
        parts.pop();
        let parent = parts.join("\\");
        if (parent === "" || /^[A-Za-z]:$/.test(parent)) parent = parent + "\\";
        browseGo(parent);
    });
    $("browsePick").addEventListener("click", () => {
        if (state.browseTarget) {
            $(state.browseTarget).value = state.browsePath;
            saveConfig(true);
        }
        closeBrowse();
    });
    $("browseList").addEventListener("click", (e) => {
        const dir = e.target.closest("[data-dir]");
        if (dir) browseGo(dir.getAttribute("data-dir"));
    });
}

/* ---------------- 启动 ---------------- */

async function main() {
    bind();
    try {
        await loadConfig();
        poll();
    } catch (e) {
        toast("初始化失败: " + e.message);
    }
}

main();
