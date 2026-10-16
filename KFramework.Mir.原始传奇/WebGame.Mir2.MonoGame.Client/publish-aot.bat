@echo off
REM 本文件是 UTF-8 编码：中文注释与输出要靠 65001 代码页显示才不乱码。
REM 只影响本脚本所在的这个控制台窗口，不改系统全局设置。
chcp 65001 >nul

REM === AOT 发布：一条命令，省得每次手打那串 -p: 参数 ===
REM
REM 用法：双击本文件，或在任意目录执行它 —— 脚本会自己切到所在目录，发的是同目录的 csproj。
REM 工程：WebGame.Mir2.MonoGame.Client（传奇客户端）
REM
REM 三个属性的含义：
REM   PublishTrimmed=true      发布时裁剪没用到的代码（减小体积）
REM   TrimMode=link            裁剪粒度：按程序集链接（比 copy 更彻底）
REM   RunAOTCompilation=true   跑 AOT 编译（启动/运行更快，代价是构建慢很多）
REM 注：本工程 csproj 里已经写了 RunAOTCompilation，这里再传一次是无害的冗余 ——
REM     留着是为了让这条命令单独看也自解释。
REM
REM TS 引擎层（jsengine）不用手工准备：csproj 的 BuildTypeScript 会按 Release 自动跑
REM npm run build:release（terser 压缩 + 剥离 console.*），再由 SyncJsEngine 同步到 wwwroot。

setlocal
cd /d "%~dp0"

set "CSPROJ=WebGame.Mir2.MonoGame.Client.csproj"

echo.
echo === Publish AOT : %CSPROJ%  (Configuration=Release) ===
echo.

dotnet publish "%CSPROJ%" -c Release -p:PublishTrimmed=true -p:TrimMode=link -p:RunAOTCompilation=true
set "RC=%errorlevel%"

if not "%RC%"=="0" goto :failed

echo.
echo === Done. Output ===
for /d %%d in ("bin\Release\*\publish") do echo   %%~fd
for /d %%d in ("bin\Release\*\*\publish") do echo   %%~fd
echo.
pause
exit /b 0

:failed
echo.
echo *** FAILED (exit code %RC%) ***
echo.
pause
exit /b %RC%
