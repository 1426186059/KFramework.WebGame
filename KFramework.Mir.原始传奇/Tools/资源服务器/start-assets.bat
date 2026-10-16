@echo off
REM 启动两个本地资源服务器（不复制资源，直接从源目录提供，节省磁盘）
REM   Mir2 默认资源 lib -> D:\OpenSource\Crystal\Build\Client\Debug                         (http://127.0.0.1:5080/)
REM   Mir2Res（http 根）   -> D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res  (http://127.0.0.1:5081/)
REM     :5081 根 = Mir2Res，按类别分子目录：
REM       /Map/            -> Mir2Res\Map\            （地图图片 Lib 蒸馏产物）
REM       /hot_update_res/ -> Mir2Res\hot_update_res\ （kfc 打包的 AssetBundle）
REM       /UI/ 等将来扩展也放 Mir2Res 下

set TOOLS=%~dp0

start "Mir2 assets :5080" node "%TOOLS%asset-server.js" --port 5080 --root "D:\OpenSource\Crystal\Build\Client\Debug"
start "Mir2Res :5081" node "%TOOLS%asset-server.js" --port 5081 --root "D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res"

echo Started:
echo   Mir2 assets        http://127.0.0.1:5080/
echo   Mir2Res (Map/hot_update_res/...)  http://127.0.0.1:5081/
echo Close the two console windows to stop them.
