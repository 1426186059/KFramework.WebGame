@echo off
REM 启动两个本地资源服务器（不复制资源，直接从源目录提供，节省磁盘）
REM   Mir2 -> D:\OpenSource\Crystal\Build\Client\Debug   (http://127.0.0.1:5080/)
REM   Mir3 -> D:\OpenSource\Zircon\Debug\Client          (http://127.0.0.1:5081/)

set TOOLS=%~dp0

start "Mir2 assets :5080" node "%TOOLS%asset-server.js" --port 5080 --root "D:\OpenSource\Crystal\Build\Client\Debug"
start "Mir3 assets :5081" node "%TOOLS%asset-server.js" --port 5081 --root "D:\OpenSource\Zircon\Debug\Client"

echo Started:
echo   Mir2 assets  http://127.0.0.1:5080/
echo   Mir3 assets  http://127.0.0.1:5081/
echo Close the two console windows to stop them.
