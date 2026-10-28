const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';
const cfgDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Mir2Res_Crystal/Crystal/Config';

const refItem = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;
const refNames = new Set();
for (const k of Object.keys(refItem)) {
    const it = refItem[k];
    if (typeof it['2'] === 'string') refNames.add(it['2']);
    if (typeof it['3'] === 'string') refNames.add(it['3']);
}

const ours = JSON.parse(fs.readFileSync(path.join(cfgDir, 'ItemInfo.json'), 'utf8'))['$values'];

const isEng = n => !/[一-鿿]/.test(n);
const isChn = n => /[一-鿿]/.test(n) && !/[A-Za-z]/.test(n);
// 混合：同时含中文与英文字母
const isMix = n => /[一-鿿]/.test(n) && /[A-Za-z]/.test(n);

let eng = 0, mix = 0, chn = 0;
const engList = [], mixList = [];
const matched = [];
for (const e of ours) {
    if (isEng(e.Name)) { eng++; engList.push(e.Name); }
    else if (isMix(e.Name)) { mix++; mixList.push(e.Name); }
    else { chn++; }
    if (refNames.has(e.Name)) matched.push(e.Name);
}

console.log(`我们物品总数: ${ours.length}`);
console.log(`  纯英文(未翻译): ${eng}  → 这些名字在参考表里也找不到(英文vs中文)`);
console.log(`  中英混合(半翻译): ${mix}`);
console.log(`  纯中文(已翻译): ${chn}`);
console.log(`名字命中参考表(即与传奇canon一致): ${matched.length}`);
console.log(`  = 命中率 ${(matched.length / ours.length * 100).toFixed(1)}%`);

console.log('\n--- 纯英文未翻译物品(前40) ---');
[...new Set(engList)].slice(0, 40).forEach(n => console.log('  ' + n));
console.log('\n--- 中英混合半翻译物品(前40) ---');
[...new Set(mixList)].slice(0, 40).forEach(n => console.log('  ' + n));
console.log('\n--- 命中参考表(与传奇canon一致, 前40) ---');
[...new Set(matched)].slice(0, 40).forEach(n => console.log('  ' + n));
