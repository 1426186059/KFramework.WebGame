// 构建门禁：扫描 src/**/*.ts，确保所有“相对” import/export 都带扩展名（.js / .wasm / .css / .json）。
//
// 原因：本项目的 tsconfig 使用 moduleResolution:"bundler"，允许无扩展名的相对引用，
// tsc 会原样把它们写进产物 ESM。而浏览器原生 ESM 无法解析无扩展名路径
// （如 './input_common' 会被请求成 jsengine/input_common，静态服务器回退成 index.html，
// 触发 “Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of text/html”）。
//
// 不依赖任何 ESLint/TS 解析器，纯正则扫描，零额外依赖；发现违规即退出码 1 终止构建。
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const SRC = join(dirname(fileURLToPath(import.meta.url)), "src");
const RELATIVE = /^\.\.?\/.*/; // 仅相对路径（./ 或 ../），裸模块名（如 'dotnet'）忽略
const OK_EXT = /\.(js|wasm|css|json)$/;

const FROM_RE = /\bfrom\s*['"]([^'"]+)['"]/g; // import ... from '...' / export ... from '...'（含多行）
const DYN_RE = /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g; // 动态 import('...')

function* walk(dir) {
    for (const name of readdirSync(dir)) {
        const p = join(dir, name);
        if (statSync(p).isDirectory()) yield* walk(p);
        else if (p.endsWith(".ts")) yield p;
    }
}

let errors = 0;
for (const file of walk(SRC)) {
    const text = readFileSync(file, "utf8");
    const lineOf = (idx) => text.slice(0, idx).split("\n").length;
    for (const re of [FROM_RE, DYN_RE]) {
        re.lastIndex = 0;
        let m;
        while ((m = re.exec(text)) !== null) {
            const spec = m[1];
            if (spec && RELATIVE.test(spec) && !OK_EXT.test(spec)) {
                errors++;
                console.error(
                    `✖ ${file}:${lineOf(m.index)}  相对模块引用 "${spec}" 缺少扩展名（应写为 .js），浏览器原生 ESM 无法解析无扩展名路径`,
                );
            }
        }
    }
}

if (errors > 0) {
    console.error(`\n发现 ${errors} 处缺少扩展名的相对引用，已终止构建（修复后再试）。`);
    process.exit(1);
}
console.log("✓ 所有相对模块引用均带扩展名");
