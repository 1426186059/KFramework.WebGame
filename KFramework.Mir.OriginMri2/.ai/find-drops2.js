const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';

const m4 = JSON.parse(fs.readFileSync(path.join(refDir, '4config10.25341.1.json'), 'utf8'));
const mk = Object.keys(m4.monster);
// 找一个字段最多的怪物，打印全字段
let best = mk[0], bestN = -1;
for (const k of mk) { const n = Object.keys(m4.monster[k]).length; if (n > bestN) { bestN = n; best = k; } }
const m = m4.monster[best];
console.log('字段最多的怪物 idx=' + best + ' 字段数=' + bestN);
for (const k of Object.keys(m)) {
    let v = m[k];
    if (typeof v === 'string' && v.length > 100) v = v.slice(0, 100) + '…';
    console.log(`  ${k}: ${JSON.stringify(v)}`);
}

// 在所有 config 里搜 bossDrop 示例值 50600001，看掉落表定义在哪里
console.log('\n=== 搜索 50600001 出现在哪些文件/键 ===');
for (let i = 0; i <= 5; i++) {
    const f = path.join(refDir, `${i}config10.25341.1.json`);
    if (!fs.existsSync(f)) continue;
    const raw = fs.readFileSync(f, 'utf8');
    if (raw.includes('50600001')) console.log(`  [${i}config] 命中`);
}
// 另外扫一遍：有没有以数字 id 为键、值是物品数组/掉落串的对象（掉落表特征）
console.log('\n=== 顶层表里“疑似掉落表”的特征扫描（值为含 item/drop 结构的对象）===');
const names = ['drop', 'Drop', 'DropList', 'bossDrop', 'monsterDrop', 'loot', 'DropTable'];
for (let i = 0; i <= 5; i++) {
    const f = path.join(refDir, `${i}config10.25341.1.json`);
    if (!fs.existsSync(f)) continue;
    const j = JSON.parse(fs.readFileSync(f, 'utf8'));
    for (const k of Object.keys(j)) {
        if (names.some(n => k.toLowerCase().includes(n.toLowerCase()))) console.log(`  [${i}config] 命中键: ${k}`);
    }
}
