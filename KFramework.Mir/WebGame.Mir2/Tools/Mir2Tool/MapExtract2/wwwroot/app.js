"use strict";

const $ = (id) => document.getElementById(id);

const state = {
    config: null,
    maps: [],
    selected: new Set(),
    active: null,
    mirDbLoaded: false,
    mirDbCount: 0,
    browseTarget: null,
    browseFile: false,
    browseFilter: "*",
    browsePath: "",
    configSaveTimer: null,
};

/* ---------------- 通用 ---------------- */

function esc(s) {
    return String(s == null ? "" : s)
        .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function FmtBytes(bytes) {
    if (bytes >= 1073741824) return (bytes / 1073741824).toFixed(2) + " GB";
    if (bytes >= 1048576) return (bytes / 1048576).toFixed(1) + " MB";
    if (bytes >= 1024) return (bytes / 1024).toFixed(0) + " KB";
    return bytes + " B";
}

async function api(path, options) {
    const res = await fetch(path, options);
    if (!res.ok) {
        const text = await res.text();
        throw new Error(text || ("HTTP " + res.status));
    }
    const ct = res.headers.get("content-type") || "";
    return ct.includes("application/json") ? res.json() : res.text();
}

let toastTimer = null;
function toast(msg) {
    const el = $("toast");
    el.textContent = msg;
    el.classList.remove("hidden");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.add("hidden"), 3200);
}

function logLine(text, cls) {
    const el = $("log");
    const div = document.createElement("div");
    if (cls) div.className = cls;
    else if (/\[ERROR\]/.test(text)) div.className = "err";
    else if (/\[WARN\]/.test(text)) div.className = "warn";
    div.textContent = text;
    el.appendChild(div);
    el.scrollTop = el.scrollHeight;
}

function clearLog() { $("log").innerHTML = ""; }

/* ---------------- 配置 ---------------- */

const CONFIG_FIELDS = ["clientRootPath", "mapDir", "sourcePath", "destinationPath", "packRootPath", "kfcPath", "minimapLibPath", "mirDBPath"];

function applyConfigToForm(cfg) {
    if (!cfg) return;
    $("clientRootPath").value = cfg.clientRootPath || "";
    $("mapDir").value = cfg.mapDir || "";
    $("sourcePath").value = cfg.sourcePath || "";
    $("destinationPath").value = cfg.destinationPath || "";
    $("packRootPath").value = cfg.packRootPath || "";
    $("kfcPath").value = cfg.kfcPath || "";
    $("minimapLibPath").value = cfg.minimapLibPath || "";
    $("mirDBPath").value = cfg.mirDBPath || "";
    $("recursiveScan").checked = !!cfg.recursiveScan;
    $("doorFrameSafety").value = String(cfg.doorFrameSafety ?? 8);
    $("maxMaps").value = String(cfg.maxMaps ?? 0);
    $("writeRawMap").checked = cfg.writeRawMap !== false;
}

function collectConfig() {
    return {
        clientRootPath: $("clientRootPath").value.trim(),
        mapDir: $("mapDir").value.trim(),
        sourcePath: $("sourcePath").value.trim(),
        destinationPath: $("destinationPath").value.trim(),
        packRootPath: $("packRootPath").value.trim(),
        kfcPath: $("kfcPath").value.trim(),
        minimapLibPath: $("minimapLibPath").value.trim(),
        mirDBPath: $("mirDBPath").value.trim(),
        recursiveScan: $("recursiveScan").checked,
        doorFrameSafety: (() => { const q = parseInt($("doorFrameSafety").value.trim(), 10); return isNaN(q) ? 8 : Math.max(0, q); })(),
        maxMaps: (() => { const q = parseInt($("maxMaps").value.trim(), 10); return isNaN(q) ? 0 : Math.max(0, q); })(),
        writeRawMap: $("writeRawMap").checked,
    };
}

async function loadConfig() {
    state.config = await api("/api/config");
    applyConfigToForm(state.config);
    updateMirDbBadge();
    await checkPaths();
}

async function saveConfig(silent) {
    try {
        state.config = await api("/api/config", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(collectConfig()),
        });
        await checkPaths();
        if (!silent) toast("配置已保存");
    } catch (e) {
        toast("保存配置失败: " + e.message);
    }
}

function scheduleSave() {
    clearTimeout(state.configSaveTimer);
    state.configSaveTimer = setTimeout(() => saveConfig(true), 500);
}

function setDot(id, ok) {
    const el = $("dot-" + id);
    if (!el) return;
    el.classList.remove("ok", "bad");
    el.classList.add(ok ? "ok" : "bad");
}

async function checkPaths() {
    try {
        const c = await api("/api/paths/check");
        setDot("clientRootPath", ($("clientRootPath").value.trim().length > 0));
        setDot("mapDir", c.mapDirExists);
        setDot("sourcePath", c.sourceDirExists);
        setDot("destinationPath", true);
        setDot("minimapLibPath", c.minimapLibExists);
        setDot("mirDBPath", c.mirDBExists);

        const rows = [
            ["地图目录", c.mapDirExists],
            ["素材源路径", c.sourceDirExists],
            ["  Tiles.Lib", c.tilesExists],
            ["  Smtiles.Lib", c.smTilesExists],
            ["  Objects.Lib", c.objectsExists],
            ["小地图 Lib", c.minimapLibExists],
            ["MirDB 文件", c.mirDBExists],
        ];
        $("checkList").innerHTML = rows.map(([name, ok]) =>
            `<div class="${ok ? "ok" : "bad"}">${ok ? "✔" : "✘"} ${esc(name)} ${ok ? "存在" : "缺失"}</div>`
        ).join("");
    } catch (e) {
        /* ignore */
    }
}

function updateMirDbBadge() {
    const el = $("mirDbBadge");
    if (state.mirDbLoaded) {
        el.className = "badge badge-green";
        el.textContent = `MirDB 已加载 ${state.mirDbCount} 张`;
    } else {
        el.className = "badge badge-gray";
        el.textContent = "MirDB 未加载";
    }
}

function updateMapBadge(count) {
    const el = $("mapBadge");
    if (count > 0) {
        el.className = "badge badge-blue";
        el.textContent = `已扫描 ${count} 张`;
    } else {
        el.className = "badge badge-gray";
        el.textContent = "未扫描";
    }
}

/* ---------------- MirDB ---------------- */

async function loadMirDb() {
    try {
        await saveConfig(true);
        const r = await api("/api/mirdb/load", { method: "POST" });
        state.mirDbLoaded = !!r.ok;
        state.mirDbCount = r.count || 0;
        updateMirDbBadge();
        logLine(r.message || "");
        toast(r.message || "");
    } catch (e) {
        toast("加载 MirDB 失败: " + e.message);
    }
}

/* ---------------- 扫描 ---------------- */

async function scan() {
    const btn = $("btnScan");
    btn.disabled = true;
    btn.textContent = "扫描中...";
    clearLog();
    try {
        await saveConfig(true);
        const r = await api("/api/scan", { method: "POST" });
        state.mirDbLoaded = !!r.mirDBLoaded;
        state.mirDbCount = r.mirDBCount || 0;
        updateMirDbBadge();

        state.maps = r.maps || [];
        state.selected = new Set();
        state.active = null;
        updateMapBadge(state.maps.length);

        logLine(r.message || "");
        if (r.formatSummary) logLine("格式分布: " + r.formatSummary.join(", "));

        $("listWrap").classList.remove("hidden");
        renderTable();
        if (state.maps.length === 0) toast("未找到 .map 文件");
    } catch (e) {
        toast("扫描失败: " + e.message);
    } finally {
        btn.disabled = false;
        btn.textContent = "扫描地图";
    }
}

/* ---------------- 列表渲染 ---------------- */

function getFiltered() {
    const kw = ($("searchFilter").value || "").trim().toLowerCase();
    const onlyMirDb = $("onlyMirDb").checked && state.mirDbLoaded;
    return state.maps.filter(m => {
        if (onlyMirDb && !m.inMirDB) return false;
        if (!kw) return true;
        return (m.name || "").toLowerCase().includes(kw) || (m.title || "").toLowerCase().includes(kw);
    });
}

function renderTable() {
    const tbody = $("mapTbody");
    const rows = getFiltered();
    const onlyMirDb = $("onlyMirDb").checked && state.mirDbLoaded;

    tbody.innerHTML = rows.map(m => {
        const sel = state.selected.has(m.name) ? "selected" : "";
        const act = state.active === m.name ? "active" : "";
        const cn = m.title ? `<span class="title-cn">${esc(m.title)}</span>` : `<span class="muted">—</span>`;
        const mm = m.inMirDB ? `${m.miniMap}/${m.bigMap}` : "—";
        const fmtCls = "fmt-" + (m.format || "unknown").replace(/[^A-Za-z0-9]/g, "");
        return `<tr class="${sel} ${act}" data-name="${esc(m.name)}">
            <td class="col-check"><input type="checkbox" ${state.selected.has(m.name) ? "checked" : ""} data-check="${esc(m.name)}" /></td>
            <td class="col-idx">${m.index}</td>
            <td>${esc(m.name)}</td>
            <td>${cn}</td>
            <td>Type ${m.typeId >= 0 ? m.typeId : "?"}</td>
            <td class="fmt ${fmtCls}">${esc(m.format)}</td>
            <td>${esc(mm)}</td>
        </tr>`;
    }).join("");

    const selectedCount = state.selected.size;
    $("selInfo").textContent = `已选: ${selectedCount} / ${state.maps.length}` +
        (onlyMirDb ? `   (当前显示: ${rows.length})` : "");

    const btn = $("btnExtract");
    btn.disabled = selectedCount === 0;
    btn.textContent = selectedCount === 0
        ? (state.maps.length === 0 ? "请先点击「扫描地图」" : "提取选中的 0 张地图")
        : `提取选中的 ${selectedCount} 张地图`;

    const allVisibleSelected = rows.length > 0 && rows.every(m => state.selected.has(m.name));
    $("chkAll").checked = allVisibleSelected;
}

/* ---------------- 选择 ---------------- */

function toggleSelect(name, on) {
    if (on) state.selected.add(name); else state.selected.delete(name);
    renderTable();
}

function selectAllVisible() {
    getFiltered().forEach(m => state.selected.add(m.name));
    renderTable();
}

function clearAll() {
    state.selected.clear();
    renderTable();
}

/* ---------------- 提取（蒸馏 → AssetBundle）---------------- */

async function extract() {
    const names = Array.from(state.selected);
    if (names.length === 0) { toast("请先勾选要提取的地图。"); return; }

    const btn = $("btnExtract");
    btn.disabled = true;
    const oldText = btn.textContent;
    btn.textContent = `提取中... (${names.length})`;
    clearLog();
    logLine(`开始蒸馏 ${names.length} 张地图...`);

    try {
        const r = await api("/api/extract", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ maps: names }),
        });
        (r.log || []).forEach(line => logLine(line));
        logLine(r.message || "");
        if (r.logTruncated) logLine("[WARN] 日志已截断");
        toast(`提取完成：成功 ${r.success} / 失败 ${r.fail}`);
        if (state.active) refreshPreview(state.active);
    } catch (e) {
        logLine("[ERROR] " + e.message);
        toast("提取失败: " + e.message);
    } finally {
        btn.disabled = false;
        btn.textContent = oldText;
        renderTable();
    }
}

/* ---------------- 产物信息 ---------------- */

function refreshPreview(name) {
    state.active = name;
    const m = state.maps.find(x => x.name === name);
    if (!m) { $("resultEmpty").classList.remove("hidden"); $("resultBody").classList.add("hidden"); renderTable(); return; }

    $("resultEmpty").classList.add("hidden");
    $("resultBody").classList.remove("hidden");
    $("resultName").textContent = m.title ? `地图: ${m.title} (${m.name})` : `地图: ${m.name}`;
    loadResult(name);
    renderTable();
}

async function loadResult(name) {
    try {
        const r = await api("/api/result?map=" + encodeURIComponent(name));
        if (!r.exists) {
            $("resultMeta").textContent = "尚未提取（勾选后点「提取选中的地图」）";
            $("resultList").innerHTML = "";
            return;
        }
        $("resultMeta").innerHTML = `库数: ${r.libCount} ｜ 用到的图: ${r.imageCount}` +
            (r.manifest ? " ｜ 清单: version.manifest" : "") +
            (r.skipped ? `<br/>跳过/缺失: ${esc(r.skipped)}` : "");
        $("resultList").innerHTML = (r.bundles || []).map(b =>
            `<div class="result-item">${esc(b.name)} <span class="muted">${FmtBytes(b.size)}</span></div>`
        ).join("") || '<div class="muted">（无产物文件）</div>';
    } catch (e) {
        $("resultMeta").textContent = "读取产物信息失败: " + e.message;
    }
}

/* ---------------- 客户端资源根目录 → 相对路径探测 ---------------- */

async function detect() {
    const btn = $("btnDetect");
    btn.disabled = true;
    btn.textContent = "探测中...";
    try {
        await saveConfig(true);
        const root = $("clientRootPath").value.trim();
        const d = await api("/api/detect?root=" + encodeURIComponent(root));

        if (d.mapDir) $("mapDir").value = d.mapDir;
        if (d.sourcePath) $("sourcePath").value = d.sourcePath;
        if (d.minimapLibPath) $("minimapLibPath").value = d.minimapLibPath;
        if (d.mirDBPath) $("mirDBPath").value = d.mirDBPath;
        await saveConfig(true);

        $("detectInfo").textContent = d.message || "";
        $("detectResult").innerHTML =
            `Map ${d.mapDirFound ? "✔" : "✘"} ｜ 素材源 ${d.sourcePathFound ? "✔" : "✘"} ｜ ` +
            `小地图 Lib ${d.minimapLibFound ? "✔" : "✘"} ｜ ` +
            `MirDB ${d.mirDBFound ? "✔" : "✘"}`;
        toast(d.message || "探测完成");
    } catch (e) {
        toast("探测失败: " + e.message);
    } finally {
        btn.disabled = false;
        btn.textContent = "自动探测相对路径";
    }
}

/* ---------------- 目录浏览 ---------------- */

function openBrowse(target, isFile, filter) {
    state.browseTarget = target;
    state.browseFile = isFile;
    state.browseFilter = filter || "*";
    const cur = $(target).value.trim();
    $("browseTitle").textContent = isFile ? "选择文件" : "选择目录";
    $("browsePick").style.display = isFile ? "none" : "";
    $("browseModal").classList.remove("hidden");
    browseGo(cur);
}

async function browseGo(path) {
    try {
        const url = `/api/fs?path=${encodeURIComponent(path || "")}` +
            (state.browseFile ? "&files=true&filter=" + encodeURIComponent(state.browseFilter) : "");
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
                    const full = el.getAttribute("data-file");
                    $(state.browseTarget).value = full;
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
    CONFIG_FIELDS.forEach(id => $(id).addEventListener("change", scheduleSave));
    $("recursiveScan").addEventListener("change", scheduleSave);
    $("doorFrameSafety").addEventListener("change", scheduleSave);
    $("maxMaps").addEventListener("change", scheduleSave);
    $("writeRawMap").addEventListener("change", scheduleSave);
    $("onlyMirDb").addEventListener("change", renderTable);

    document.querySelectorAll("[data-browse]").forEach(btn => {
        btn.addEventListener("click", () => openBrowse(btn.getAttribute("data-browse"), false));
    });
    document.querySelectorAll("[data-browse-file]").forEach(btn => {
        const target = btn.getAttribute("data-browse-file");
        const filter = btn.getAttribute("data-filter");
        btn.addEventListener("click", () => openBrowse(target, true, filter));
    });

    $("btnSaveConfig").addEventListener("click", () => saveConfig(false));
    $("btnDetect").addEventListener("click", detect);
    $("btnLoadMirDb").addEventListener("click", loadMirDb);
    $("btnScan").addEventListener("click", scan);
    $("btnExtract").addEventListener("click", extract);

    $("searchFilter").addEventListener("input", renderTable);
    $("btnSelectAll").addEventListener("click", selectAllVisible);
    $("btnClearAll").addEventListener("click", clearAll);
    $("chkAll").addEventListener("change", (e) => {
        if (e.target.checked) selectAllVisible(); else clearAll();
    });

    $("btnClearLog").addEventListener("click", clearLog);

    $("toggleCheck").addEventListener("click", () => {
        const body = $("checkBody");
        body.classList.toggle("hidden");
        $("toggleCheck").querySelector(".caret").textContent = body.classList.contains("hidden") ? "▸" : "▾";
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

    $("mapTbody").addEventListener("click", (e) => {
        const check = e.target.closest("[data-check]");
        if (check) {
            const name = check.getAttribute("data-check");
            toggleSelect(name, check.checked);
            refreshPreview(name);
            return;
        }
        const row = e.target.closest("tr[data-name]");
        if (row) {
            refreshPreview(row.getAttribute("data-name"));
        }
    });
    $("mapTbody").addEventListener("change", (e) => {
        const check = e.target.closest("[data-check]");
        if (check) {
            toggleSelect(check.getAttribute("data-check"), check.checked);
        }
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
        logLine("地图蒸馏工具已就绪。");
        logLine("提示：① 填「客户端资源根目录」→「自动探测相对路径」");
        logLine("      ②「加载 MirDB」→「扫描地图」→ 勾选后「提取」");
        logLine("      提取 = 蒸馏用到的素材 Lib → 按地图打包成 AssetBundle");
    } catch (e) {
        toast("初始化失败: " + e.message);
    }
}

main();
