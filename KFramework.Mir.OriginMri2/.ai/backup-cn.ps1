$ErrorActionPreference = 'Stop'
$ws = 'D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2'
$backup = Join-Path $ws '.ai\backup-cn'
New-Item -ItemType Directory -Force -Path $backup | Out-Null

$targets = @(
    @{ src = Join-Path $ws 'Mir2Res_Crystal\Crystal\Config';   dst = 'Mir2Res_Crystal-Crystal-Config';  filter = '*.json' },
    @{ src = Join-Path $ws 'Web_Mir2.Server.WinForms\bin\Debug\net10.0-windows\Envir';   dst = 'WinForms-Envir';   filter = '*.txt' },
    @{ src = Join-Path $ws 'Web_Mir2.Server.WinForms\bin\Debug\net10.0-windows\Configs'; dst = 'WinForms-Configs'; filter = '*.ini' },
    @{ src = Join-Path $ws 'Web_Mir2.Server\bin\Debug\net10.0\Envir';   dst = 'Server-Envir';   filter = '*.txt' },
    @{ src = Join-Path $ws 'Web_Mir2.Server\bin\Debug\net10.0\Configs'; dst = 'Server-Configs'; filter = '*.ini' },
    @{ src = Join-Path $ws 'Web_Mir2.Server\Envir';   dst = 'Src-Envir';   filter = '*.txt' },
    @{ src = Join-Path $ws 'Web_Mir2.Server\Configs'; dst = 'Src-Configs'; filter = '*.ini' }
)

foreach ($t in $targets) {
    if (-not (Test-Path $t.src)) { Write-Output ("SKIP(no src): " + $t.dst); continue }
    $dst = Join-Path $backup $t.dst
    robocopy $t.src $dst $t.filter /S /NJH /NDL /NP /NFL | Out-Null
    $n = (Get-ChildItem $dst -Recurse -File).Count
    Write-Output ("OK: " + $t.dst + " (" + $n + " files)")
}
Write-Output ("Backup root: " + $backup)
