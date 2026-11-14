param($path)
$j = Get-Content $path -Raw
$o = $j | ConvertFrom-Json
$keys = $o.PSObject.Properties.Name
Write-Host "TOP KEYS ($($keys.Count)):"
Write-Host ($keys -join ', ')

$mi = $o.modelInfo
$names = $mi.PSObject.Properties.Name
Write-Host "modelInfo count: $($names.Count)"
$firstModelKey = $names[0]
Write-Host "first model key: $firstModelKey"
$aModel = $mi.$firstModelKey
$actionNames = $aModel.PSObject.Properties.Name
Write-Host "actions under model $firstModelKey ($($actionNames.Count)): $($actionNames[0..4] -join ',')"
$firstActionKey = $actionNames[0]
$rec = $aModel.$firstActionKey | ConvertTo-Json -Compress
Write-Host "record sample (model $firstModelKey, action $firstActionKey): $rec"

# size estimate
Write-Host "total chars: $($j.Length)"
