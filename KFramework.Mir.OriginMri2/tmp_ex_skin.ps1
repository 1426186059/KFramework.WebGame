$file = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/js/main10.25487.1.min.js'
$out  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/tmp_deliver_skin.txt'
$text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
$sb = New-Object System.Text.StringBuilder

# DeliverDialogSkin 定义特征
$marker = '"btn_close","list_page","list_content"'
$idx = $text.IndexOf($marker)
if ($idx -lt 0) { $sb.AppendLine("DeliverDialogSkin marker NOT FOUND") | Out-Null }
else {
    $a = [Math]::Max(0, $idx - 300)
    $b = [Math]::Min($text.Length, $idx + 9000)
    $sb.AppendLine("=== DeliverDialogSkin def (marker at $idx) ===") | Out-Null
    $sb.AppendLine($text.Substring($a, $b - $a)) | Out-Null
}

# DeliverPageRenderSkin 定义特征：skinParts=["btn"] 不唯一，用 DeliverPageRenderSkin=function 或 skinName 附近
foreach ($m in @('DeliverPageRenderSkin','DeliverBtnRenderSkin')) {
    # 找 "className"? 没有。找该 skin 的 skinParts。Page: ["btn"]，Btn: ["btn_go","txt_name"]
    $mk = if ($m -eq 'DeliverPageRenderSkin') { '"btn"]' } else { '"btn_go","txt_name"' }
    $i2 = $text.IndexOf($mk)
    if ($i2 -lt 0) { $sb.AppendLine("$m ($mk) NOT FOUND") | Out-Null; continue }
    $a2 = [Math]::Max(0, $i2 - 300); $b2 = [Math]::Min($text.Length, $i2 + 6000)
    $sb.AppendLine("=== $m def (marker $mk at $i2) ===") | Out-Null
    $sb.AppendLine($text.Substring($a2, $b2 - $a2)) | Out-Null
}
[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output ('wrote ' + $out + ' bytes=' + $sb.Length)
