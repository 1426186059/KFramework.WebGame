// ============ 基础工具 ============
const $ = id => document.getElementById(id);
const esc = s => (s ?? "").toString().replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

async function api(path, opts) {
    const r = await fetch(path, opts);
    if (!r.ok) {
        let msg = r.status + " " + r.statusText;
        try { msg = (await r.json()).message || msg; } catch { }
        throw new Error(msg);
    }
    return r.json();
}

function toast(msg) {
    const t = $("toast");
    t.textContent = msg;
    t.classList.remove("hidden");
    clearTimeout(toast._t);
    toast._t = setTimeout(() => t.classList.add("hidden"), 2600);
}

function logLine(line, cls) {
    const el = document.createElement("div");
    if (cls) el.className = cls;
    el.textContent = line;
    $("log").appendChild(el);
    $("log").scrollTop = $("log").scrollHeight;
}

$("btnClearLog").onclick = () => { $("log").innerHTML = ""; };

// ============ 目录浏览弹窗 ============
const state = { browsePath: "", curRootInput: "rootPath" };

function openBrowse(targetInput) {
    state.curRootInput = targetInput;
    $("browseTitle").textContent = "选择目录";
    $("browseModal").classList.remove("hidden");
    browseGo($(targetInput).value.trim());
}

async function browseGo(path) {
    try {
        const d = await api("/api/fs?path=" + encodeURIComponent(path || ""));
        state.browsePath = d.path || "";
        $("browsePath").textContent = state.browsePath || "（我的电脑）";

        const parts = [];
        (d.drives || []).forEach(f => parts.push(renderBrowseItem(f.name, f.fullPath, false)));
        (d.directories || []).forEach(f => parts.push(renderBrowseItem(f.name, f.fullPath, false)));
        if (parts.length === 0) parts.push('<div class="browse-item muted">（空）</div>');
        $("browseList").innerHTML = parts.join("");
    } catch (e) {
        toast("列举目录失败: " + e.message);
    }
}

function renderBrowseItem(name, fullPath, isFile) {
    const icon = isFile ? "📄" : "📁";
    return `<div class="browse-item" data-dir="${esc(fullPath)}"><span>${icon}</span><span>${esc(name)}</span></div>`;
}

function closeBrowse() { $("browseModal").classList.add("hidden"); }

$("btnBrowseRoot").onclick = () => openBrowse("rootPath");
$("browseClose").onclick = closeBrowse;
$("browseModal").addEventListener("click", e => { if (e.target === $("browseModal")) closeBrowse(); });
$("browseUp").onclick = () => {
    const p = state.browsePath.replace(/[\\/]+$/, "");
    const idx = Math.max(p.lastIndexOf("\\"), p.lastIndexOf("/"));
    browseGo(idx > 0 ? p.slice(0, idx) : "");
};
$("browsePick").onclick = () => {
    if (state.browsePath) {
        $(state.curRootInput).value = state.browsePath;
        $("dot-rootPath").classList.add("ok");
        closeBrowse();
        scanSubDirs();
    } else {
        toast("请先进入一个目录");
    }
};
$("browseList").addEventListener("click", e => {
    const dir = e.target.closest("[data-dir]");
    if (dir) browseGo(dir.getAttribute("data-dir"));
});

// ============ 子目录预览 ============
async function scanSubDirs() {
    const root = $("rootPath").value.trim();
    if (!root) { toast("请先选择资源根目录"); return; }

    $("subDirList").classList.add("hidden");
    $("subDirInfo").textContent = "列举中…";
    $("btnPack").disabled = true;

    try {
        const d = await api("/api/fs?path=" + encodeURIComponent(root));
        const dirs = d.directories || [];
        if (dirs.length === 0) {
            $("subDirInfo").textContent = "根目录下没有子目录 → 将把根目录整体打成一个 .web.lib 包。";
            $("btnPack").disabled = false;
            return;
        }
        $("subDirInfo").textContent = `检测到 ${dirs.length} 个子目录，将分别打成一个 .web.lib 包：`;
        $("subDirList").classList.remove("hidden");
        $("subDirList").innerHTML = dirs.map(f =>
            `<div class="subdir-item"><span>📁</span><span class="name">${esc(f.name)}</span><span class="meta">${esc(f.fullPath)}</span></div>`
        ).join("");
        $("btnPack").disabled = false;
    } catch (e) {
        $("subDirInfo").textContent = "列举失败: " + e.message;
        $("dot-rootPath").classList.remove("ok");
    }
}

$("btnScan").onclick = scanSubDirs;
$("rootPath").addEventListener("change", () => { $("dot-rootPath").classList.remove("ok"); scanSubDirs(); });

// ============ 打包 ============
$("btnPack").onclick = async () => {
    const root = $("rootPath").value.trim();
    if (!root) { toast("请先选择资源根目录"); return; }

    const req = {
        root,
        outputDir: $("outputDir").value.trim() || null,
        kind: $("kind").value.trim() || "map",
        lossless: $("lossless").checked,
        quality: parseInt($("quality").value, 10) || 90,
    };

    $("btnPack").disabled = true;
    $("btnPack").textContent = "打包中…";
    logLine("开始打包: " + root);

    try {
        const r = await api("/api/pack", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req),
        });

        if (!r.ok) {
            logLine("✗ " + r.message, "err");
            toast("打包失败");
            return;
        }

        renderResult(r);
        (r.message || "").split("\n").forEach(l => logLine(l));
        $("pkgBadge").textContent = `${r.packageCount} 个包`;
        $("pkgBadge").className = "badge badge-green";
        toast("打包完成：" + r.packageCount + " 个资源包");
    } catch (e) {
        logLine("✗ " + e.message, "err");
        toast("打包失败: " + e.message);
    } finally {
        $("btnPack").disabled = false;
        $("btnPack").textContent = "开始打包";
    }
};

function renderResult(r) {
    $("resultEmpty").classList.add("hidden");
    $("resultWrap").classList.remove("hidden");

    $("pkgTbody").innerHTML = r.packages.map(p => `
        <tr>
            <td>${esc(p.name)}</td>
            <td class="file">${esc(p.file)}</td>
            <td>${fmtBytes(p.size)}</td>
            <td>${p.entries}</td>
            <td class="hash">${esc(p.hash)}</td>
            <td><a class="dl" href="${esc(p.url)}" download="${esc(p.file)}">下载</a></td>
        </tr>`).join("");

    $("manifestJson").textContent = r.manifestJson;
    $("manifestJson").classList.remove("muted");
    const m = $("btnManifest");
    m.classList.remove("hidden");
    m.href = "/api/file/" + encodeURIComponent(r.manifestFile);
}

function fmtBytes(b) {
    if (b >= 1073741824) return (b / 1073741824).toFixed(2) + " GB";
    if (b >= 1048576) return (b / 1048576).toFixed(1) + " MB";
    if (b >= 1024) return Math.round(b / 1024) + " KB";
    return b + " B";
}
