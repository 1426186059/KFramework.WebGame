// 修复召唤系物品半截名断链：
// 1) 召唤Shinsu → 召唤神兽（掉落已全译，物品表未译，断链致 30 个掉落文件加载失败）
// 2) 召唤圣Deva/召唤Vampire/召唤Snakes → 彻底汉化（当前一致但半截，顺手统一）
// 范围：Crystal ItemInfo.json 的 Name + 3 个 dataRoot 的 Envir/**/*.txt 引用
const fs = require('fs');
const path = require('path');

const ws = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2';
const cfgDir = path.join(ws, 'Mir2Res_Crystal/Crystal/Config');
const dataRoots = [
    path.join(ws, 'Web_Mir2.Server.WinForms/bin/Debug/net10.0-windows'),
    path.join(ws, 'Web_Mir2.Server/bin/Debug/net10.0'),
    path.join(ws, 'Web_Mir2.Server'),
];

const renames = {
    '召唤Shinsu': '召唤神兽',
    '召唤圣Deva': '召唤圣精灵',
    '召唤Vampire': '召唤吸血蜘蛛',
    '召唤Snakes': '召唤魅惑之蛇',
};

// 冲突检查：ItemInfo 里不能已存在新名字（重名会破坏按名查找）
const itemPath = path.join(cfgDir, 'ItemInfo.json');
const items = JSON.parse(fs.readFileSync(itemPath, 'utf8'))['$values'];
const existing = new Set(items.map(e => e.Name));
for (const nn of Object.values(renames)) {
    if (existing.has(nn)) { console.error('冲突：物品表已存在 ' + nn); process.exit(1); }
}

// 1. 改 ItemInfo.json（纯文本替换保持格式，UTF-8 无 BOM）
let itemText = fs.readFileSync(itemPath, 'utf8');
let itemHits = 0;
for (const [o, n] of Object.entries(renames)) {
    const re = new RegExp('"Name": "' + o + '"', 'g');
    itemText = itemText.replace(re, () => { itemHits++; return '"Name": "' + n + '"'; });
}
fs.writeFileSync(itemPath, itemText, 'utf8');
console.log(`ItemInfo.json: ${itemHits} 个 Name 已改名`);

// 2. 改 3 个 dataRoot 的 Envir txt（词边界：前后不能是字母数字，避免部分匹配）
function walk(dir, cb) {
    for (const f of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, f.name);
        if (f.isDirectory()) walk(p, cb);
        else if (f.name.toLowerCase().endsWith('.txt')) cb(p);
    }
}
for (const root of dataRoots) {
    const envir = path.join(root, 'Envir');
    if (!fs.existsSync(envir)) continue;
    let files = 0, hits = 0;
    walk(envir, p => {
        let text = fs.readFileSync(p, 'utf8');
        let newText = text;
        let localHits = 0;
        for (const [o, n] of Object.entries(renames)) {
            const re = new RegExp('(?<![A-Za-z0-9_])' + o + '(?![A-Za-z0-9_])', 'g');
            newText = newText.replace(re, () => { localHits++; return n; });
        }
        if (newText !== text) {
            // 保持 BOM
            const hadBom = text.charCodeAt(0) === 0xFEFF;
            fs.writeFileSync(p, hadBom ? '\uFEFF' + newText : newText, 'utf8');
            files++; hits += localHits;
        }
    });
    console.log(`[${path.basename(root)}] Envir: ${files} 个文件改名, ${hits} 处替换`);
}

// 3. 其它 JSON（GameShop/Quest/NPC）引用检查
for (const t of ['GameShopItem.json', 'QuestInfo.json', 'NPCInfo.json', 'ConquestInfo.json']) {
    const p = path.join(cfgDir, t);
    if (!fs.existsSync(p)) continue;
    const text = fs.readFileSync(p, 'utf8');
    for (const o of Object.keys(renames)) if (text.includes(o)) console.log(`!! ${t} 仍引用 ${o}`);
}
console.log('完成');
