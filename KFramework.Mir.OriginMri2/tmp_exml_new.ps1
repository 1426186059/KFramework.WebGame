$file = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/default.thm.25487.json'
$dir  = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2'
$text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
foreach ($name in @('DeliverDialogSkin','DeliverPageRenderSkin','DeliverBtnRenderSkin')) {
    $cn = '"className": "' + $name + '"'
    $cnIdx = $text.IndexOf($cn)
    if ($cnIdx -lt 0) { Write-Output ("$name : className NOT FOUND"); continue }
    $gjsMarker = $text.LastIndexOf('"gjs": "', $cnIdx)
    $contentStart = $gjsMarker + 8
    $contentEnd = $cnIdx - 1   # 末尾的闭合引号
    $gjs = $text.Substring($contentStart, $contentEnd - $contentStart)
    # 反转义
    $gjs = $gjs.Replace('\"', '"').Replace('\n', "`n").Replace('\t', "`t").Replace('\\', '\')
    $out = Join-Path $dir ("tmp_exml_" + $name + ".txt")
    [System.IO.File]::WriteAllText($out, $gjs, [System.Text.Encoding]::UTF8)
    Write-Output ("wrote " + $out + " (" + $gjs.Length + " chars)")
}
