$base    = 'https://cdn.yscq-wy.yscq.com/fugu_20201203/resource'
$manifest= 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/allmanifest10.25487.1.json'
$outDir  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/NPCTeleporterDialog'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$json = [System.IO.File]::ReadAllText($manifest, [System.Text.Encoding]::UTF8)
$mf   = $json | ConvertFrom-Json

# 目标 key：背景图 + 图集(json/png)
$targets = @('y_cs_bg.jpg', 'y_common-sheet.json', 'y_common-sheet.png')

foreach ($key in $mf.PSObject.Properties.Name) {
    foreach ($t in $targets) {
        if ($key.EndsWith($t)) {
            $v = $mf.$key.v
            $s = $mf.$key.s
            $ext = $t.Split('.')[-1]
            $url = "$base/$($v.Substring(0,2))/${v}_$s.$ext"
            $name = $key.Substring($key.LastIndexOf('/') + 1)
            $out = Join-Path $outDir $name
            try {
                Invoke-WebRequest -Uri $url -OutFile $out -TimeoutSec 120 -ErrorAction Stop
                Write-Output ("OK   $name  (key=$key)  len=" + (Get-Item $out).Length)
            } catch {
                Write-Output ("FAIL $name  url=$url  code=" + $_.Exception.Response.StatusCode.value__)
            }
        }
    }
}

# 列出 y_common-sheet.json 里本 UI 用到的帧，便于核对
$atlas = Join-Path $outDir 'y_common-sheet.json'
if (Test-Path $atlas) {
    $a = [System.IO.File]::ReadAllText($atlas, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    $frames = @('com_line_h','com_btn_close0','com_z_btn_1','com_z_btn_1_1','com_z_btn_2','com_z_btn_2_1')
    Write-Output "`n--- frames used by NPCTeleporterDialog (in y_common-sheet) ---"
    foreach ($f in $frames) {
        if ($a.PSObject.Properties.Name -contains $f) {
            $fr = $a.$f
            Write-Output ("FOUND  $f  x=$($fr.x) y=$($fr.y) w=$($fr.w) h=$($fr.h)")
        } else {
            Write-Output ("MISSING $f")
        }
    }
}
