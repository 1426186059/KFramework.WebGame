param(
  [string]$ConfigPath = 'C:\Users\Administrator\Downloads\0config10.25341.1.json',
  [int]$MapId = 154,
  [string]$ConfigUrl,   # 数据服务器地址，即宿主页里的 js_gameVars.configUrl
  [string]$Ext = 'bin', # bin / dat / csv
  [string]$Ver = '1.0.2',
  [string]$OutDir = '.'
)

if (-not $ConfigUrl) {
  Write-Host 'ERROR: 必须提供 -ConfigUrl（游戏宿主页中的 js_gameVars.configUrl，或 DevTools Network 里 .bin/.dat 请求的目录部分）'
  exit 1
}

$m = (Get-Content $ConfigPath -Raw -Encoding utf8 | ConvertFrom-Json).map."$MapId"
if (-not $m) { Write-Host "map $MapId not found"; exit 1 }

$dataId = $m.data
$url = $ConfigUrl + $dataId + '.' + $Ext
if ($Ext -eq 'dat') { $url += '?ver=' + $Ver }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$dest = Join-Path $OutDir ("map_{0}.{1}" -f $dataId, $Ext)

try {
  Invoke-WebRequest -Uri $url -OutFile $dest -TimeoutSec 60 -ErrorAction Stop
  Write-Host "saved $dest  ($( (Get-Item $dest).Length ) bytes)  <- 这就是地图核心数据文件"
} catch {
  Write-Host "下载失败：$url"
  Write-Host $_.Exception.Message
  exit 1
}
