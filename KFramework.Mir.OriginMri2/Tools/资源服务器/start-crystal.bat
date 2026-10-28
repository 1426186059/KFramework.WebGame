@echo off
REM 启动 Mir2Res_Crystal 资源服务器（根 = Mir2Res_Crystal，专门托管 Crystal 版配置与资源）
REM 复用同目录的 asset-server.js（Node，仅开发用，支持 CORS / Range / 防目录穿越）
REM   配置表 JSON 放在 root\Crystal\Config\  ->  http://127.0.0.1:5082/Crystal/Config/ItemInfo.json
REM   游戏服务器（Web_Mir2.Server）的 ConfigBaseUrl 应指向 http://127.0.0.1:5082/
set TOOLS=%~dp0
start "Mir2Res_Crystal :5082" node "%TOOLS%asset-server.js" --port 5082 --root "D:\OpenSource\KFramework.WebGame\KFramework.Mir.原始传奇\Mir2Res_Crystal"
echo Started:
echo   Mir2Res_Crystal  http://127.0.0.1:5082/
echo   (Crystal/Config/*.json -> /Crystal/Config/*.json)
echo Close the console window to stop it.
