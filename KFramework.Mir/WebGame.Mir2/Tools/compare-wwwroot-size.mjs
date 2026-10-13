// compare-wwwroot-size.mjs —— 把发布出的 wwwroot 按「形态」拆成三份，用来对比发布后的体积。
//
// 用法：
//   node compare-wwwroot-size.mjs "D:\...\bin\Release\net10.0\publish\wwwroot"
//
// 源目录里同一个文件有并存的三套形态（.NET 发布会同时给出 gz 与 br），
// 本工具按扩展名把它们【分类】到三个同级目录 —— 【不动任何文件名，只搬动】：
//   wwwroot-ori\  ← index.html、foo.wasm、foo.js、foo.dat …      （原始未压）
//   wwwroot-gz\   ← index.html.gz、foo.wasm.gz、foo.js.gz …      （gzip 预压缩）
//   wwwroot-br\   ← index.html.br、foo.wasm.br、foo.js.br …      （brotli 预压缩）
//
// 为什么要分：源目录里三套混在一起，看目录属性得到的是「三套之和」（本例 33 MB），
// 那不是客户端要下载的量。分开后，每份的大小才是「不压缩 / gzip / br」各自的体积。
//
// 三份相加应当等于源目录总大小 —— 报告里会打出来，用于核对没漏文件。
// 想直接给服务器用：wwwroot-ori\ 提供原始文件，wwwroot-br\ 提供同名 + .br 的预压缩版本。
//
// 为什么要用 node：Windows PowerShell 5.1 跑在 .NET Framework 上，能枚举目录、但没有 Brotli；
// node 在本工程本来就有（TSEngine 用 npm），下面只用标准文件 API 搬运，不重新压缩任何东西。

import { copyFileSync, existsSync, mkdirSync, readdirSync, rmSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';

const srcWwwroot = process.argv[2];
if (!srcWwwroot || !existsSync(srcWwwroot)) {
    console.error('用法：node compare-wwwroot-size.mjs "<publish 下的 wwwroot 路径>"');
    process.exit(1);
}

const parent = dirname(srcWwwroot);

const buckets = [
    { name: 'wwwroot-ori', label: '原始未压', match: (rel) => !/\.(gz|br)$/i.test(rel) },
    { name: 'wwwroot-gz', label: 'gzip 预压缩', match: (rel) => /\.gz$/i.test(rel) },
    { name: 'wwwroot-br', label: 'brotli 预压缩', match: (rel) => /\.br$/i.test(rel) },
];

// ---------- 先把源目录整个列出来 ----------
const all = [];   // { rel, size }
(function walk(dir) {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = join(dir, entry.name);
        if (entry.isDirectory()) {
            walk(full);
        } else {
            all.push({ rel: relative(srcWwwroot, full), size: statSync(full).size });
        }
    }
})(srcWwwroot);

// ---------- 分类搬运（同名复制，不做任何重命名/重压缩） ----------
for (const b of buckets) {
    const outDir = join(parent, b.name);
    if (existsSync(outDir)) rmSync(outDir, { recursive: true, force: true });

    let total = 0;
    let count = 0;
    for (const f of all) {
        if (!b.match(f.rel)) continue;
        const dst = join(outDir, f.rel);
        mkdirSync(dirname(dst), { recursive: true });
        copyFileSync(join(srcWwwroot, f.rel), dst);
        total += f.size;
        count++;
    }

    b.outDir = outDir;
    b.total = total;
    b.count = count;
}

// ---------- 报告 ----------
const MB = (n) => (n / 1048576).toFixed(2) + ' MB';
const srcTotal = all.reduce((s, f) => s + f.size, 0);
const sumTotal = buckets.reduce((s, b) => s + b.total, 0);

console.log('\n=== wwwroot 按形态分类 ===');
console.log(`源目录：${srcWwwroot}`);
console.log(`源：${all.length} 个文件，合计 ${MB(srcTotal)}\n`);

for (const b of buckets) {
    console.log(`  ${b.name.padEnd(12)} ${String(b.count).padStart(4)} 个文件  ${MB(b.total).padStart(11)}   ${b.label}`);
    console.log(`     -> ${b.outDir}`);
}

console.log(`\n  三份相加 ${MB(sumTotal)}（源 ${MB(srcTotal)}，${sumTotal === srcTotal ? '一致 ✓' : '不一致 ✗'}）`);
console.log();
