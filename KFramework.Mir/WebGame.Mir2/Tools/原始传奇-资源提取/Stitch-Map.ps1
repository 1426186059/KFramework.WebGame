param(
  [string]$TileDir,
  [string]$MetaJson,
  [string]$OutFile = 'map',   # 输出文件名前缀，例如 map_154_chunk_0_0.png
  [int]$Cols,
  [int]$Rows,
  [int]$TileW = 512,
  [int]$TileH = 256
)

# 若未显式传入 cols/rows，则从 meta.json 读取
if ((-not $Cols -or -not $Rows) -and $MetaJson -and (Test-Path $MetaJson)) {
  $meta = Get-Content $MetaJson -Raw -Encoding utf8 | ConvertFrom-Json
  if (-not $Cols)  { $Cols  = $meta.cols }
  if (-not $Rows)  { $Rows  = $meta.rows }
  if (-not $TileDir) { $TileDir = Split-Path $MetaJson }
}

if (-not $TileDir -or -not (Test-Path $TileDir)) { Write-Host 'ERROR: 需提供 -TileDir'; exit 1 }
if (-not $Cols -or -not $Rows) { Write-Host 'ERROR: 需提供 -Cols/-Rows 或有效的 -MetaJson'; exit 1 }

Add-Type -AssemblyName System.Drawing

# GDI+ 单边最大 32767px，故按块拼接（每块 < 15000px）
$MAX = 15000
$chunkCols = [Math]::Max(1, [Math]::Floor($MAX / $TileW))
$chunkRows = [Math]::Max(1, [Math]::Floor($MAX / $TileH))

$total = 0
for ($cr = 0; $cr * $chunkRows -lt $Rows; $cr++) {
  $r0 = $cr * $chunkRows
  $r1 = [Math]::Min($Rows, $r0 + $chunkRows)
  for ($cc = 0; $cc * $chunkCols -lt $Cols; $cc++) {
    $c0 = $cc * $chunkCols
    $c1 = [Math]::Min($Cols, $c0 + $chunkCols)

    $bmpW = ($c1 - $c0) * $TileW
    $bmpH = ($r1 - $r0) * $TileH
    $bmp  = New-Object System.Drawing.Bitmap $bmpW, $bmpH
    $g    = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::None

    for ($r = $r0; $r -lt $r1; $r++) {
      for ($c = $c0; $c -lt $c1; $c++) {
        $index = $r * $Cols + $c + 1          # 1-based 瓦片序号
        $p = Join-Path $TileDir ($index + '.jpg')
        if (Test-Path $p) {
          $tile = [System.Drawing.Image]::FromFile($p)
          $g.DrawImage($tile, ($c - $c0) * $TileW, ($r - $r0) * $TileH, $TileW, $TileH)
          $tile.Dispose()
        }
      }
    }
    $g.Dispose()
    $out = "{0}_chunk_{1}_{2}.png" -f $OutFile, $cr, $cc
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $out  ($bmpW x $bmpH)"
    $total++
  }
}
Write-Host "DONE: $total chunk(s), full = $($Cols*$TileW) x $($Rows*$TileH)"
