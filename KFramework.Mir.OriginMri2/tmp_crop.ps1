Add-Type -AssemblyName System.Drawing
$dir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/NPCTeleporterDialog'
$atlasPng = Join-Path $dir 'y_common-sheet.png'
$atlasJson= Join-Path $dir 'y_common-sheet.json'

$src = [System.Drawing.Bitmap]::new($atlasPng)
$a   = [System.IO.File]::ReadAllText($atlasJson, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$frames = $a.frames

$frameNames = @('com_line_h','com_btn_close0','com_z_btn_1','com_z_btn_1_1','com_z_btn_2','com_z_btn_2_1')
foreach ($f in $frameNames) {
    if (-not $frames.PSObject.Properties.Name.Contains($f)) { Write-Output ("SKIP $f (not in atlas)"); continue }
    $fr = $frames.$f
    $rect = New-Object System.Drawing.Rectangle($fr.x, $fr.y, $fr.w, $fr.h)
    $crop = New-Object System.Drawing.Bitmap($fr.w, $fr.h)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage($src, 0, 0, $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $out = Join-Path $dir ($f + '.png')
    $crop.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose()
    Write-Output ("CROP $f  -> $f.png  ($($fr.w)x$($fr.h))")
}
$src.Dispose()
Write-Output "done"
