$file = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/js/main10.25487.1.min.js'
$out  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/tmp_deliver_main.txt'
$text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
$sb = New-Object System.Text.StringBuilder

foreach ($k in @('DeliverDialogSkin','DeliverPageRenderSkin','DeliverBtnRenderSkin')) {
    $idx = $text.IndexOf($k)
    if ($idx -lt 0) { $sb.AppendLine("### $k : NOT FOUND") | Out-Null; continue }
    $a = [Math]::Max(0, $idx - 4000)
    $b = [Math]::Min($text.Length, $idx + 9000)
    $sb.AppendLine("### $k (idx=$idx) ===") | Out-Null
    $sb.AppendLine($text.Substring($a, $b - $a)) | Out-Null
    $sb.AppendLine("`n-----END $k-----`n") | Out-Null
}
[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output ('wrote ' + $out + ' bytes=' + $sb.Length)
