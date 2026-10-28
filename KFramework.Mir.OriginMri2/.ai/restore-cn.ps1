$ErrorActionPreference = 'Stop'
$ws = 'D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2'
$backup = Join-Path $ws '.ai\backup-cn'

$targets = @(
    @{ dst = Join-Path $ws 'Mir2Res_Crystal\Crystal\Config';   src = 'Mir2Res_Crystal-Crystal-Config' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server.WinForms\bin\Debug\net10.0-windows\Envir';   src = 'WinForms-Envir' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server.WinForms\bin\Debug\net10.0-windows\Configs'; src = 'WinForms-Configs' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server\bin\Debug\net10.0\Envir';   src = 'Server-Envir' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server\bin\Debug\net10.0\Configs'; src = 'Server-Configs' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server\Envir';   src = 'Src-Envir' },
    @{ dst = Join-Path $ws 'Web_Mir2.Server\Configs'; src = 'Src-Configs' }
)

foreach ($t in $targets) {
    $src = Join-Path $backup $t.src
    if (-not (Test-Path $src)) { Write-Output ("SKIP(no backup): " + $t.src); continue }
    robocopy $src $t.dst /MIR /NJH /NDL /NP /NFL | Out-Null
    Write-Output ("Restored: " + $t.dst)
}
Write-Output 'done'
