// copy_deps.mjs —— 把 deps/ 拷进 dist/jsengine/deps，让产物目录自包含。
//
// 为什么用 .mjs 而不是 .js？
//   Node 里 .mjs 永远按 ES Module 解析，和 package.json 的 "type" 无关；
//   而 .js 会跟随 package.json 的 type 字段——本工程没设 "type":"module"，
//   默认就是 CommonJS，那样用 import 语法就会直接报 SyntaxError。
//   用 .mjs 就不必操心 package.json 怎么配，import/export 随便用，最省心。
//   （如果坚持用 .js，就得把脚本写成 require() 的 CommonJS 风格。）

import { cpSync, existsSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const depsSrc = join(root, 'deps');
const depsDst = join(root, 'dist', 'jsengine', 'deps');

if (existsSync(depsDst)) rmSync(depsDst, { recursive: true, force: true });
cpSync(depsSrc, depsDst, { recursive: true });
console.log(`[copy_deps] deps -> ${depsDst}`);
