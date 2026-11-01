$base = 'https://cdn.yscq-wy.yscq.com/fugu_20201203'
$dir  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/js'
$paths = @(
    'resource/eui_skins/alert/deliver/DeliverDialogSkin.exml',
    'resource/eui_skins/alert/deliver/DeliverPageRenderSkin.exml',
    'resource/eui_skins/alert/deliver/DeliverBtnRenderSkin.exml'
)
foreach ($p in $paths) {
    $name = $p.Substring($p.LastIndexOf('/')+1)
    $out = Join-Path $dir $name
    try {
        Invoke-WebRequest -Uri ($base + '/' + $p) -OutFile $out -TimeoutSec 60 -ErrorAction Stop
        $len = (Get-Item $out).Length
        $t = [System.IO.File]::ReadAllText($out, [System.Text.Encoding]::UTF8)
        # 统计关键布局线索
        $hl = ([regex]::Matches($t,'HorizontalLayout')).Count
        $vl = ([regex]::Matches($t,'VerticalLayout')).Count
        $tl = ([regex]::Matches($t,'TileLayout')).Count
        Write-Output ("OK   $name len=$len  HLayout=$hl VLayout=$vl Tile=$tl  w=$([regex]::Match($t,'width=\"(\d+)').Groups[1].Value) h=$([regex]::Match($t,'height=\"(\d+)').Groups[1].Value)")
    } catch {
        Write-Output ("FAIL $name  code=" + $_.Exception.Response.StatusCode.value__)
    }
}
