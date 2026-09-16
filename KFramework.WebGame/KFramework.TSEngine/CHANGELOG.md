此文件解释 Visual Studio 如何创建项目。

以下工具用于生成此项目:
- TypeScript Compiler (tsc)

以下为生成此项目的步骤:
- 创建项目文件 (`KFramework.TSEngine.esproj`)。
- 创建 `launch.json` 以启用调试。
- 安装 npm 包并创建 `tsconfig.json`: `npm init && npm i --save-dev eslint @types/node typescript && npx tsc --init --sourceMap true`。
- 创建 `app.ts`。
- 更新 `package.json` 入口点。
- 更新 `package.json` 中的 TypeScript 生成脚本。
- 创建 `eslint.config.js` 以启用 Lint 分析。
- 向解决方案添加项目。
- 写入此文件。
