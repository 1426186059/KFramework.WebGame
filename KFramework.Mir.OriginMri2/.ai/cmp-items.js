const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';
const cfgDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Mir2Res_Crystal/Crystal/Config';

const refItem = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;
const refNames = new Set();
const refType = {};
for (const k of Object.keys(refItem)) {
    const it = refItem[k];
    const n2 = it['2'], n3 = it['3'];
    if (typeof n2 === 'string') { refNames.add(n2); refType[n2] = it['5']; }
    if (typeof n3 === 'string') refNames.add(n3);
}

const ours = JSON.parse(fs.readFileSync(path.join(cfgDir, 'ItemInfo.json'), 'utf8'))['$values'];
console.log(`参考物品数: ${Object.keys(refItem).length}（唯一名 ${refNames.size}）`);
console.log(`我们物品数: ${ours.length}`);

let inRef = 0;
const notInRef = [];
const ourNames = new Set();
for (const e of ours) {
    ourNames.add(e.Name);
    if (refNames.has(e.Name)) inRef++;
    else notInRef.push(e.Name);
}
const overlap = inRef;
console.log(`我们物品中 名字命中参考表: ${overlap} / ${ours.length} = ${(overlap / ours.length * 100).toFixed(1)}%`);
console.log(`我们物品中 名字不在参考表: ${notInRef.length}（占 ${(notInRef.length / ours.length * 100).toFixed(1)}%）`);

// 参考里有但我们完全没有的物品名（仅规模参考，因内容体系不同意义有限）
const onlyRef = [...refNames].filter(n => !ourNames.has(n));
console.log(`参考独有名字（我们无）: ${onlyRef.length}`);

console.log('\n--- 我们物品中“不在参考表”的样例（前 80，多为 Crystal 扩充或译名差异，供复核）---');
[...new Set(notInRef)].slice(0, 80).forEach(n => console.log('  ' + n));
