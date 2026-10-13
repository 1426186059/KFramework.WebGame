// 【Release 专用】就地压缩 dist/Release/jsengine 顶层的引擎模块，并剥离所有 console.*。
//
// 为什么单独一步、而不是让 tsc 干：tsc 只做类型检查 + 语法降级，【从不删代码、也不压缩】。
// 所以 Release 的产物处理必须放在 tsc 之后 —— 见 package.json 的 build:release。
//
// 只压 tsc 的产物（该目录顶层 *.js），【不碰 deps/】：
// 那是第三方（ktx2 转码器等），压它可能破坏其内部假设，且体积本来就不归我们管。
//
// drop_console 是【删除】console.* 调用，不是替换成空函数：
// 引擎里所有 console 调用都只做输出、没有副作用，删掉不改变语义。
// 反过来这也说明了 src/cusotm_func.ts 那条约定的必要性 ——
// 断言一律走 assert()（只 throw，不依赖 console），否则 Release 会把断言一并删掉。
//
// 产物里【不保留注释】（压缩本来就要扔掉）。要看带注释的实现，读 src/*.ts 或 Debug 产物。

import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { minify } from 'terser';

const root = dirname(fileURLToPath(import.meta.url));

// 压哪个目录由参数决定：node minify.mjs Release（省略时按 Release）。
// 产物分配置存（dist/Debug/jsengine 给开发调试、dist/Release/jsengine 给发布），只压后者。
const config = process.argv[2] ?? 'Release';
const engineDir = join(root, 'dist', config, 'jsengine');

const files = readdirSync(engineDir).filter((name) => name.endsWith('.js'));
if (files.length === 0) {
    console.error(`[minify] ${engineDir} 下没有 .js —— 是不是还没跑 tsc？`);
    process.exit(1);
}

let bytesBefore = 0;
let bytesAfter = 0;

for (const name of files) {
    const path = join(engineDir, name);
    const source = readFileSync(path, 'utf8');

    const result = await minify(source, {
        // module: true —— 产物是浏览器原生 ESM，必须保留 import / export 语句，
        // 否则浏览器加载即报 "Unexpected token 'export'"。terser 在该模式下也不会
        // 改动导出的名字，所以 C# 侧 [JSImport("netSend", ...)] 这类名字不受影响。
        module: true,
        compress: {
            drop_console: true,   // 删掉所有 console.*（含 log / error / warn / assert）
            passes: 2,
        },
        mangle: true,             // 只改局部名；顶层的导出名 terser 会保护
        format: {
            comments: false,
        },
    });

    if (result.code == null) {
        console.error(`[minify] ${name} 压缩后没有输出，已终止（避免留下半个产物的目录）`);
        process.exit(1);
    }

    bytesBefore += Buffer.byteLength(source);
    bytesAfter += Buffer.byteLength(result.code);
    writeFileSync(path, result.code, 'utf8');
}

const kb = (n) => (n / 1024).toFixed(1) + ' KB';
const saved = bytesBefore > 0 ? (100 - (bytesAfter / bytesBefore) * 100).toFixed(1) : '0';
console.log(`[minify] ${files.length} 个模块：${kb(bytesBefore)} -> ${kb(bytesAfter)}（省 ${saved}%），已剥离 console.*`);
