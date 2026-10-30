@echo off
REM 启动本地资源服务器（不复制资源，直接从源目录提供，节省磁盘）
REM   Mir2 默认资源 lib -> D:\OpenSource\Crystal\Build\Client\Debug                         (http://127.0.0.1:5080/)
REM
REM   注：原 5081(Mir2Res) 本地资源服务器已移除——现客户端资源改由 CDN 提供，
REM       不再需要指向老 Crystal 工程 KFramework.Mir.Crystal\...\Mir2Res 的本地服务器。

set TOOLS=%~dp0

start "Mir2 assets :5080" node "%TOOLS%asset-server.js" --port 5080 --root "D:\OpenSource\Crystal\Build\Client\Debug"

echo Started:
echo   Mir2 assets        http://127.0.0.1:5080/
echo Close the console window to stop it.
