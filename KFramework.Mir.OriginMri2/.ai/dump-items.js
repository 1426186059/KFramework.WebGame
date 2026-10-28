const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';
const refItem = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;

// 找几个有 Stats 列(40/48/54) 的武器/装备，整条 dump
const want = [];
for (const k of Object.keys(refItem)) {
    const it = refItem[k];
    if (it['40'] !== undefined || it['48'] !== undefined || it['54'] !== undefined) {
        want.push(k);
        if (want.length >= 4) break;
    }
}
// 也加一个货币(idx1) 和一个药水(找带"药"名)
const names = Object.keys(refItem).map(Number).sort((a,b)=>a-b);
const extra = [];
for (const k of names) {
    const n = refItem[k]['2'] || refItem[k]['3'] || '';
    if (n.includes('药') || n.includes('剑') || n.includes('盔')) { extra.push(String(k)); if (extra.length>=3) break; }
}
const all = [...new Set([...want, ...extra])];
for (const k of all) {
    const it = refItem[k];
    console.log('\n===== idx ' + k + ' =====');
    for (const c of Object.keys(it).map(Number).sort((a,b)=>a-b)) {
        let v = it[c];
        if (typeof v === 'string' && v.length > 80) v = v.slice(0, 80) + '…';
        console.log(`  列${c}: ${v}`);
    }
}
