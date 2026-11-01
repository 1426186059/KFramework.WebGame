$file = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/main10.25341.1.min.js'
$out  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/tmp_deliver_js.txt'
$text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
$keys = @('DeliverDialog', 'DeliverPageRender', 'DeliverBtnRender', 'delieve', 'com_z_btn_6', 'z_boss_img0', 'z_com_line1', 'z_com_btn_close')
$sb = New-Object System.Text.StringBuilder
foreach ($k in $keys) {
    $idx = $text.IndexOf($k)
    if ($idx -lt 0) { $sb.AppendLine("### $k : NOT FOUND") | Out-Null; continue }
    $sb.AppendLine("### $k : found at $idx") | Out-Null
    $a = [Math]::Max(0, $idx - 2500)
    $b = [Math]::Min($text.Length, $idx + 2500)
    $sb.AppendLine($text.Substring($a, $b - $a)) | Out-Null
    $sb.AppendLine("`n-----END $k-----`n") | Out-Null
}
[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output ('wrote ' + $out + ' len=' + $sb.Length)
