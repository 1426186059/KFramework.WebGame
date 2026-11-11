@echo off
REM 启动本地资源服务器（不复制资源，直接从源目录提供，节省磁盘）
REM   资源根目录 -> D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Mir2Res
REM   对外地址   -> http://127.0.0.1:5080/
REM
REM   Mir2Res/ 下的目录与客户端请求路径一一对应：
REM     MyRes/Localization/  语言包（Chinese.json）
REM     MyRes/Sound/         音效 wav + SoundList.lst
REM     MyRes/Login/         登录 UI 美术 PNG
REM     resource/            图集 png / json / fnt、default.res.json 等索引
REM     assets/              地图瓦片 jpg
REM   客户端侧基址见 WebGame.China.Mir2.Client/2026New/RemoteWebSetting.cs (LibBaseUrl)。

set TOOLS=%~dp0
set RESROOT=%~dp0..\..\Mir2Res

start "Mir2 assets :5080" node "%TOOLS%asset-server.js" --port 5080 --root "%RESROOT%"

echo Started:
echo   Mir2 assets        http://127.0.0.1:5080/  (root = %RESROOT%)
echo Close the console window to stop it.
