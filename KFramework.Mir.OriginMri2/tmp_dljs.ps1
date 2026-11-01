$base = 'https://cdn.yscq-wy.yscq.com/fugu_20201203'
$out = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2'
$cands = @(
    @{n='qufu1.min.js'; u="$base/qufu1.min.js"},
    @{n='game.js'; u="$base/libs/modules/game/game.js"}
)
foreach ($c in $cands) {
    $f = Join-Path $out $c.n
    try {
        Invoke-WebRequest -Uri $c.u -OutFile $f -TimeoutSec 300 -ErrorAction Stop
        $len = (Get-Item $f).Length
        $t = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
        $cnt = ([regex]::Matches($t, 'DeliverDialog')).Count
        $cnt2 = ([regex]::Matches($t, 'DeliverPageRender')).Count
        $cnt3 = ([regex]::Matches($t, 'DeliverBtnRender')).Count
        Write-Output ("OK   " + $c.n + " len=" + $len + " DeliverDialog=" + $cnt + " Page=" + $cnt2 + " Btn=" + $cnt3)
    } catch {
        Write-Output ("FAIL " + $c.n + " " + $_.Exception.Response.StatusCode.value__)
    }
}
