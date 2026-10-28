// 精确校验：只校验 MonsterInfo.DropPath 实际引用的掉落文件（含 #INSERT 递归）
// 1) DropPath 文件不存在 → 服务器会静默创建空文件（怪物无掉落，隐藏损失）
// 2) 掉落行物品名不在 ItemInfo → 服务器报"无法加载掉落"
const fs = require('fs');
const path = require('path');

const ws = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2';
const cfgDir = path.join(ws, 'Mir2Res_Crystal/Crystal/Config');
const dataRoots = [
    path.join(ws, 'Web_Mir2.Server.WinForms/bin/Debug/net10.0-windows'),
    path.join(ws, 'Web_Mir2.Server/bin/Debug/net10.0'),
];

const itemNames = new Set(JSON.parse(
    fs.readFileSync(path.join(cfgDir, 'ItemInfo.json'), 'utf8'))['$values'].map(e => e.Name.replace(/ /g, '').toLowerCase()));
const monsters = JSON.parse(
    fs.readFileSync(path.join(cfgDir, 'MonsterInfo.json'), 'utf8'))['$values'];

const chanceRe = /^\d+\/\d+$/;
const insertRe = /#INSERT\s+\[(.*?)\]/i;

function parseFile(filePath, itemName, missing, depth) {
    if (depth > 5 || !fs.existsSync(filePath)) return;
    const lines = fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '').split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
        const t = lines[i].trim();
        if (!t || t.startsWith(';') || t === '{' || t === '}') continue;
        const ins = t.match(insertRe);
        if (ins) { parseFile(path.join(path.dirname(filePath), ins[1] + '.txt'), itemName, missing, depth + 1); continue; }
        const parts = t.split(/\s+/);
        if (parts.length < 2 || !chanceRe.test(parts[0])) continue;
        if (parts[1].toUpperCase() === 'GOLD' || parts[1].toUpperCase().startsWith('GROUP')) continue;
        if (!itemNames.has(parts[1].replace(/ /g, '').toLowerCase())) missing.push(`${itemName} (${path.relative(dataRoots[0], filePath)}:${i + 1}) 行「${t}」 -> ${parts[1]}`);
    }
}

for (const root of dataRoots) {
    const dropsDir = path.join(root, 'Envir', 'Drops');
    const missing = [];
    const noFile = [];
    for (const m of monsters) {
        if (!m.DropPath) continue;
        const fp = path.join(dropsDir, m.DropPath + '.txt');
        if (!fs.existsSync(fp)) { noFile.push(`${m.Name} -> ${m.DropPath}`); continue; }
        parseFile(fp, m.Name, missing, 0);
    }
    const uniqItem = [...new Set(missing.map(e => e.split('-> ')[1]))];
    const uniqNoFile = [...new Set(noFile)];
    console.log(`=== [${path.basename(root)}] ===`);
    console.log(`缺失物品名: ${missing.length} 处 / ${uniqItem.length} 种`);
    for (const e of missing.slice(0, 50)) console.log('  ' + e);
    console.log(`DropPath 文件不存在（怪物将无掉落）: ${uniqNoFile.length} 种`);
    for (const e of uniqNoFile.slice(0, 30)) console.log('  ' + e);
}
