param(
  [string]$ConfigPath = 'C:\Users\Administrator\Downloads\0config10.25341.1.json',
  [int]$MapId
)

$raw = Get-Content $ConfigPath -Raw -Encoding utf8
$cfg = $raw | ConvertFrom-Json

$TILE_W = 512
$TILE_H = 256

if ($MapId) {
  $m = $cfg.map."$MapId"
  if (-not $m) { Write-Host "map $MapId not found"; exit 1 }
  $cols = [Math]::Ceiling($m.width / $TILE_W)
  $rows = [Math]::Ceiling($m.height / $TILE_H)
  $n    = $cols * $rows
  Write-Host "=== Map $MapId : $($m.name) ==="
  Write-Host "img=$($m.img)  data=$($m.data)  cls=$($m.cls)"
  Write-Host "width=$($m.width)  height=$($m.height)"
  Write-Host "grid cols=$cols  rows=$rows  tiles=$n  (tile ${TILE_W}x${TILE_H})"
  Write-Host "pixel size = $($cols*$TILE_W) x $($rows*$TILE_H)"
} else {
  Write-Host "Total maps: $($cfg.map.PSObject.Properties.Count)"
  foreach ($p in $cfg.map.PSObject.Properties) {
    $m = $p.Value
    $cols = [Math]::Ceiling($m.width / $TILE_W)
    $rows = [Math]::Ceiling($m.height / $TILE_H)
    Write-Host ('{0,6} {1,-14} img={2,5} data={3,5} {4}x{5} tiles={6}' -f $m.id, $m.name, $m.img, $m.data, $cols, $rows, ($cols*$rows))
  }
}
