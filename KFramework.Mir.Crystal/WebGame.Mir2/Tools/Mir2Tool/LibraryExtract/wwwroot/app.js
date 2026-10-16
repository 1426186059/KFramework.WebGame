"use strict";

const $ = (id) => document.getElementById(id);

const state = {
    config: null,
    polling: false,
    timer: null,
    lastLogCount: 0,
    browseTarget: null,
    browseFile: false,
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
    toastTimer = setTimeout(() => el.classList.add("hidden"), 3200);
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

function renderLog(log, truncated) {
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

/* ---------------- 配置 ---------------- */

function applyConfig(cfg) {
    if (!cfg) return;
    $("libPath").value = cfg.libPath || "";
    $("outputFolder").value = cfg.outputFolder || "";
    $("rbFile").checked = !cfg.libIsFolder;
    $("rbFolder").checked = !!cfg.libIsFolder;
    refreshDots();
}

function collectConfig() {
    return {
        libPath: $("libPath").value.trim(),
        libIsFolder: $("rbFolder").checked,
        outputFolder: $("outputFolder").value.trim(),
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
        if (!silent) toast("配置已保存");
    } catch (e) {
        toast("保存配置失败: " + e.message);
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

// 这里只能判断"路径是否填写"，真实存在性由服务端校验
function refreshDots() {
    setDot("libPath", ($("libPath").value.trim().length > 0));
    setDot("outputFolder", ($("outputFolder").value.trim().length > 0));
}

/* ---------------- 扫描 ---------------- */

async function scan() {
    const btn = $("btnScan");
    btn.disabled = true;
    try {
        await saveConfig(true);
        const r = await api("/api/scan", { method: "POST" });
        $("scanInfo").textContent = r.message || "";
        if (r.count > 0) {
            $("scanListWrap").classList.remove("hidden");
            $("scanList").innerHTML = (r.files || [])
                .map(f => `<div>${esc(f)}</div>`).join("");
        } else {
            $("scanListWrap").classList.add("hidden");
            $("scanList").innerHTML = "";
        }
        toast(r.message || "");
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

function stopPolling() {
    state.polling = false;
    if (state.timer) { clearInterval(state.timer); state.timer = null; }
}

async function poll() {
    let s;
    try { s = await api("/api/status"); }
    catch (e) { return; }

    renderLog(s.log || [], s.logTruncated);
    $("progressBar").style.width = (s.percent || 0) + "%";
    $("lblProgress").textContent = s.message || "";
    $("runInfo").textContent = s.totalFiles
        ? `已完成 ${s.completedFiles}/${s.totalFiles} 文件 · ${s.totalImages} 张图片`
        : "";

    if (s.running) {
        setBadge("运行中", "badge-blue");
        return;
    }

    // 已结束
    stopPolling();
    $("btnConvert").disabled = false;
    $("btnCancel").disabled = true;

    if (s.cancelled) setBadge("已取消", "badge-red");
    else if (s.error) setBadge("出错", "badge-red");
    else if (s.result) setBadge("已完成", "badge-green");

    if (s.result) toast(s.result);
    else if (s.error) toast("转换出错: " + s.error);
}

/* ---------------- 目录浏览 ---------------- */

function openBrowse(target, isFile) {
    state.browseTarget = target;
    state.browseFile = isFile;
    $("browseTitle").textContent = isFile ? "选择 .Lib 文件" : "选择目录";
    $("browsePick").style.display = isFile ? "none" : "";
    $("browseModal").classList.remove("hidden");
    browseGo($(target).value.trim());
}

async function browseGo(path) {
    try {
        const url = `/api/fs?path=${encodeURIComponent(path || "")}` +
            (state.browseFile ? "&files=true&filter=*.Lib" : "");
        const d = await api(url);
        state.browsePath = d.path || "";
        $("browsePath").textContent = d.path || "（此电脑）";
        $("browseUp").disabled = !d.parent;

        const parts = [];
        (d.drives || []).forEach(f => parts.push(renderBrowseItem(f, true, true)));
        (d.directories || []).forEach(f => parts.push(renderBrowseItem(f, true, false)));
        (d.files || []).forEach(f => parts.push(renderBrowseItem(f, false, false)));
        if (parts.length === 0) parts.push('<div class="browse-item muted">（空）</div>');
        $("browseList").innerHTML = parts.join("");

        if (state.browseFile) {
            $("browseList").querySelectorAll("[data-file]").forEach(el => {
                el.onclick = () => {
                    $(state.browseTarget).value = el.getAttribute("data-file");
                    closeBrowse();
                    saveConfig(true);
                };
            });
        }
    } catch (e) {
        toast("读取目录失败: " + e.message);
    }
}

function renderBrowseItem(f, isDir, isDrive) {
    const attrs = isDir
        ? `data-dir="${esc(f.fullPath)}"`
        : `data-file="${esc(f.fullPath)}" style="color:#7b8798"`;
    const icon = isDrive ? "💽" : (isDir ? "📁" : "📄");
    return `<div class="browse-item" ${attrs}><span>${icon}</span><span>${esc(f.name)}</span></div>`;
}

function closeBrowse() { $("browseModal").classList.add("hidden"); }

/* ---------------- 事件绑定 ---------------- */

function bind() {
    $("libPath").addEventListener("change", scheduleSave);
    $("outputFolder").addEventListener("change", scheduleSave);
    $("rbFile").addEventListener("change", scheduleSave);
    $("rbFolder").addEventListener("change", scheduleSave);

    $("btnSaveConfig").addEventListener("click", () => saveConfig(false));
    $("btnScan").addEventListener("click", scan);
    $("btnConvert").addEventListener("click", convert);
    $("btnCancel").addEventListener("click", cancel);
    $("btnClearLog").addEventListener("click", clearLog);

    document.querySelectorAll("[data-browse]").forEach(btn => {
        const target = btn.getAttribute("data-browse");
        btn.addEventListener("click", () => openBrowse(target, target === "libPath"));
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
        poll(); // 拉取一次状态，恢复上次结果
    } catch (e) {
        toast("初始化失败: " + e.message);
    }
}

main();
