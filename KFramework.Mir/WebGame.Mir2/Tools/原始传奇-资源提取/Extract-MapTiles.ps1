param(
  [string]$ConfigPath = 'C:\Users\Administrator\Downloads\0config10.25341.1.json',
  [int]$MapId = 154,
  [string]$ImageServer = 'https://cdn.yscq-wy.yscq.com/fugu_20201203/assets/resource/',
  [string]$Ver = '1.0.2',
  [string]$OutDir
)

$TILE_W = 512
$TILE_H = 256

if (-not $OutDir) { $OutDir = ".\map_$MapId`_tiles" }

$m = (Get-Content $ConfigPath -Raw -Encoding utf8 | ConvertFrom-Json).map."$MapId"
if (-not $m) { Write-Host "map $MapId not found"; exit 1 }

$cols = [Math]::Ceiling($m.width / $TILE_W)
$rows = [Math]::Ceiling($m.height / $TILE_H)
$n    = $cols * $rows
$img  = $m.img

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$base = $ImageServer + "zipmap/$img/"

$ok = 0; $fail = 0
for ($i = 1; $i -le $n; $i++) {
  $url  = $base + $i + '.jpg?ver=' + $Ver
  $dest = Join-Path $OutDir ($i + '.jpg')
  if (Test-Path $dest) { $ok++; continue }
  $good = $false
  for ($r = 1; $r -le 3 -and -not $good; $r++) {
    try { Invoke-WebRequest -Uri $url -OutFile $dest -TimeoutSec 30 -ErrorAction Stop; $good = $true }
    catch { Start-Sleep -Milliseconds 200 }
  }
  if ($good) { $ok++ } else { $fail++; Write-Host "FAIL $i" }
  if ($i % 500 -eq 0) { Write-Host "progress $i/$n" }
}

$meta = [ordered]@{
  mapId = $MapId
  name  = $m.name
  img   = $img
  cols  = $cols
  rows  = $rows
  tileW = $TILE_W
  tileH = $TILE_H
  count = $n
  base  = $base
  ver   = $Ver
} | ConvertTo-Json
Set-Content -Path (Join-Path $OutDir 'meta.json') -Value $meta -Encoding utf8

Write-Host "DONE ok=$ok fail=$fail  tiles dir=$OutDir (meta.json written)"
