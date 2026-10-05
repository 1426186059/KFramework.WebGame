$ErrorActionPreference = 'Stop'
Set-Location 'd:\OpenSource\KFramework.WebGame\KFramework.WebGame'
$cfg = 'KFramework.Test.Common\Content\build.config.json'
$bak = $cfg + '.bak'
Copy-Item $cfg $bak -Force

# 临时改为 Ktx2 + 一个不存在的 exe 路径，验证校验应抛异常
$json = Get-Content $cfg -Raw | ConvertFrom-Json
$json.TextureSwitchTarget = 'Ktx2'
$json.Ktx2ExePath = 'D:\OpenSource\KFramework.WebGame\KFramework.WebGame\Need_DLL\no_such_basisu.exe'
$json | ConvertTo-Json -Depth 10 | Set-Content $cfg -Encoding UTF8

Write-Host '=== 测试1：Ktx2 + 不存在的 exe，应抛异常 ==='
dotnet run --project KFramework.Content.Cli/KFramework.Content.Cli.csproj -v q --nologo -- --root KFramework.Test.Common/Content 2>&1 | Out-String -Width 300 | Select-Object -Last 20

# 恢复
Copy-Item $bak $cfg -Force
Remove-Item $bak -Force
Write-Host '=== 配置已恢复 ==='
