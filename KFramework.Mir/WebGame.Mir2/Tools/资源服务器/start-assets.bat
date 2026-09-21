@echo off
REM 启动两个本地资源服务器（不复制资源，直接从源目录提供，节省磁盘）
REM   Mir2 默认资源 lib -> D:\OpenSource\Crystal\Build\Client\Debug                         (http://127.0.0.1:5080/)
REM   hot_update_res(AssetBundle) -> D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res\hot_update_res  (http://127.0.0.1:5081/)

set TOOLS=%~dp0

start "Mir2 assets :5080" node "%TOOLS%asset-server.js" --port 5080 --root "D:\OpenSource\Crystal\Build\Client\Debug"
start "hot_update_res :5081" node "%TOOLS%asset-server.js" --port 5081 --root "D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res\hot_update_res"

echo Started:
echo   Mir2 assets        http://127.0.0.1:5080/
echo   hot_update_res     http://127.0.0.1:5081/
echo Close the two console windows to stop them.
