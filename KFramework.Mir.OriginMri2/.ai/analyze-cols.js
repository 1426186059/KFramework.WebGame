const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';
const refItem = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;

const cols = {};
for (const k of Object.keys(refItem)) {
    const it = refItem[k];
    for (const c of Object.keys(it)) {
        if (!cols[c]) cols[c] = { count: 0, samples: new Set() };
        cols[c].count++;
        const v = it[c];
        cols[c].samples.add(typeof v === 'string' ? v : JSON.stringify(v));
    }
}
const colArr = Object.keys(cols).map(Number).sort((a, b) => a - b);
console.log('列号 | 出现次数 | 样例值(前5)');
for (const c of colArr) {
    const s = [...cols[c].samples].slice(0, 5).map(x => x.length > 30 ? x.slice(0, 30) + '…' : x);
    console.log(`${c}\t${cols[c].count}\t${s.join(' | ')}`);
}
