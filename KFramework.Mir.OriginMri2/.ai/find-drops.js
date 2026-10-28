const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';

// 1) 列出所有 config 文件的顶层表名
console.log('=== 参考库各 config 顶层表 ===');
for (let i = 0; i <= 5; i++) {
    const f = path.join(refDir, `${i}config10.25341.1.json`);
    if (!fs.existsSync(f)) continue;
    const j = JSON.parse(fs.readFileSync(f, 'utf8'));
    console.log(`[${i}config] ${Object.keys(j).join(', ')}`);
}

// 2) 在所有 config 里搜 drop/掉落 相关键
console.log('\n=== 含 drop/Drop/掉落 的键 ===');
for (let i = 0; i <= 5; i++) {
    const f = path.join(refDir, `${i}config10.25341.1.json`);
    if (!fs.existsSync(f)) continue;
    const j = JSON.parse(fs.readFileSync(f, 'utf8'));
    for (const k of Object.keys(j)) {
        if (/drop|掉落|爆|loot/i.test(k)) console.log(`  [${i}config] ${k}`);
    }
}

// 3) 看 4config.monster 第一条：是否内嵌掉落
const m4 = JSON.parse(fs.readFileSync(path.join(refDir, '4config10.25341.1.json'), 'utf8'));
if (m4.monster) {
    const mk = Object.keys(m4.monster);
    console.log('\n=== 4config.monster 条目数:', mk.length, '===');
    const sample = m4.monster[mk[0]];
    console.log('monster[0] 字段:', Object.keys(sample).join(', '));
    // 找掉落相关字段
    for (const k of Object.keys(sample)) {
        if (/drop|掉落|item|爆|goods/i.test(k)) {
            const v = sample[k];
            console.log(`  -> ${k}: ${typeof v === 'string' && v.length > 120 ? v.slice(0,120)+'…' : JSON.stringify(v).slice(0,120)}`);
        }
    }
}
