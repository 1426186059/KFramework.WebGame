MirEngine.Browser\tsengine —— 浏览器引擎的 TypeScript 源码仓库
================================================================

【这个目录是干什么的】

    tsengine\src\     唯一可编辑源：*.ts（入库）
    tsengine\dist\    tsc 编译产物：*.js（不入库，.gitignore 已忽略）
    Web_Mir2\wwwroot\jsengine\    运行时实际加载的目录
    Web_Mir3\wwwroot\jsengine\    运行时实际加载的目录

    流程：改 src\*.ts  ->  tsc 编译到 dist\  ->  csproj 的 CopyJsEngine 目标把 dist\*.js
          拷进各站 wwwroot\jsengine  ->  浏览器加载 wwwroot\jsengine\main.js

    注意：dist\ 是生成物，不要手改；要改永远改 src\ 里的 .ts。


【换机器 / 新克隆后：一次性的准备】

    1) 确认有 Node.js（含 npm）。在命令行里验证：
           node -v
           npm -v
       没有就去 https://nodejs.org/ 装 LTS 版（本项目在 Node 24 上验证过，Node 18+ 均可）。

    2) 安装 TypeScript 编译依赖（只需做一次）：
           cd WebAssemblyBrowserApp\MirEngine.Browser\tsengine
           npm install
       这一步会在本目录生成 node_modules\（已忽略入库）和 package-lock.json（已忽略入库）。
       国内网络慢可先切镜像再装：
           npm config set registry https://registry.npmmirror.com
       完全离线时：从已装好的机器上把整个 node_modules 目录拷到本目录即可（无需联网）。

    3) 验证能编译：
           npm run build
       没输出 = 零错误，产物已写进 dist\。
       想边改边编译就开：
           npm run watch


【日常使用】

    * 只改 .ts：开着 npm run watch 就行，改完自动出 dist\*.js。
    * 在 VS 里直接“生成解决方案”：csproj 会自动先跑 tsc 再拷贝到 wwwroot\jsengine，
      不需要你手动执行任何命令。
    * TS 有类型错误：整个构建直接失败（错误列表里可双击跳到出错的 .ts 行），
      错误示例：...\tsengine\src\core\cursor.ts(39,7): error TS2322: ...


【常见问题】

    Q1 构建报“缺少 tsengine 编译产物 dist\main.js”
       A：dist\ 还没生成（或 node_modules 没装）。先执行上面的第 2、3 步。

    Q2 构建时看到警告“未检测到 …\node_modules\typescript，本次跳过了 TS 编译”
       A：没装依赖，编译被跳过，wwwroot\jsengine 会沿用旧产物——改了 TS 不会生效。
          执行 npm install 即可恢复自动编译。

    Q3 改了 .ts 但页面没变化
       A：按顺序排查：dist\*.js 的时间戳有没有更新 -> 是否开着 npm run watch（没开就
          npm run build）-> VS 生成时有没有真的执行 CopyJsEngine -> 浏览器强刷（Ctrl+F5）。
          实在不对就删掉整个 dist\ 目录再 npm run build 重新生成。

    Q4 想确认 wwwroot\jsengine 和 dist\ 是否一致
       A：两边文件都应是 15 个（main.js、shared.js、core\*9 个、render\webgl\*3 个、
          render\webgpu\*1 个）。数量或时间对不上，重新生成一次解决方案即可。

    Q5 能不能不装 Node？
       A：不能保证。仓库里只有 wwwroot\jsengine 的旧产物兜底，改任何 .ts 都必须先编译。


【配置速查】

    tsconfig.json：rootDir=src、outDir=dist、target/module=ES2022、strict=true（全量严格检查）。
    package.json：npm run build（tsc -p tsconfig.json）、npm run watch（--watch）。
    两个站的 csproj：BuildJsEngine（跑 tsc）-> CopyJsEngine（拷 dist\*.js 到 wwwroot\jsengine）
                    -> CheckJsEngineOutput（缺 dist\main.js 直接报错）。
