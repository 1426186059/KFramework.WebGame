$file = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/main10.25341.1.min.js'
$out  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/tmp_deliver_js2.txt'
$text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
$sb = New-Object System.Text.StringBuilder

# 所有 DeliverDialogSkin 出现处
$lit = 'DeliverDialogSkin'
$pos = 0; $n = 0
while (($idx = $text.IndexOf($lit, $pos)) -ge 0) {
    $n++
    $a = [Math]::Max(0, $idx - 1500); $b = [Math]::Min($text.Length, $idx + 3500)
    $sb.AppendLine("=== DeliverDialogSkin #$n at $idx ===") | Out-Null
    $sb.AppendLine($text.Substring($a, $b - $a)) | Out-Null
    $sb.AppendLine("`n") | Out-Null
    $pos = $idx + $lit.Length
}
$sb.AppendLine("TOTAL DeliverDialogSkin occurrences: $n") | Out-Null

# gameui 资源引用
foreach ($k in @('gameui','y_common','com_z_btn','com_btn_close','deliver','delieve','jpg','z_boss','z_com_line')) {
    $c = 0; $p = 0
    while (($i = $text.IndexOf($k, $p)) -ge 0) { $c++; $p = $i + $k.Length; if ($c -ge 3) { break } }
    $sb.AppendLine("count[$k] (first 3) = $c") | Out-Null
}
[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output ('wrote ' + $out)
