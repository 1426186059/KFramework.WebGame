param()
$mapPath = 'D:\OpenSource\Crystal\Build\Client\Debug\Map\0.map'
$base = 'D:\OpenSource\Crystal\Build\Client\Debug'
$bytes = [System.IO.File]::ReadAllBytes($mapPath)

$w = [BitConverter]::ToInt16($bytes, 4)
$h = [BitConverter]::ToInt16($bytes, 6)
$total = [long]$w * $h

# Tiles.Lib 容量
$tb = [System.IO.File]::ReadAllBytes($base + '\Data\Map\WemadeMir2\Tiles.Lib')
$tilesCount = [BitConverter]::ToInt32($tb, 4)
Write-Host ('Map 0 W=' + $w + ' H=' + $h + ' TOTAL=' + $total + '  Tiles.Lib容量=' + $tilesCount)

$off = 8
$noBack = 0
$hasBack = 0
$curOOB = 0      # 当前 Draw 解码 (bimg & 0x1FFFFFFF)-1 越界
$mask7fffOOB = 0 # 假设 (bimg & 0x7FFF)-1
$maskFFFFOOB = 0 # 假设 (bimg & 0xFFFF)-1
$hiDist = @{}    # 高16位分布
$samples = @()
for ($x = 0; $x -lt $w; $x++) {
    for ($y = 0; $y -lt $h; $y++) {
        $bi = [BitConverter]::ToInt16($bytes, $off); $off += 2
        $bimg = [BitConverter]::ToInt32($bytes, $off); $off += 4
        $off += 20
        $raw = [uint32]$bimg -band 0x1FFFFFFF
        if ($bi -lt 0 -or $raw -eq 0) { $noBack++; continue }
        if ($bi -ne 0) { continue }  # 只看底图主库 Tiles
        $hasBack++
        $idxCur = [int]$raw - 1
        if ($idxCur -ge $tilesCount) { $curOOB++ }
        $idx7 = ($bimg -band 0x7FFF) - 1
        if ($idx7 -ge $tilesCount) { $mask7fffOOB++ }
        $idxF = ($bimg -band 0xFFFF) - 1
        if ($idxF -ge $tilesCount) { $maskFFFFOOB++ }
        $hi = ($bimg -shr 16) -band 0xFFFF
        $hiDist[$hi] = if ($hiDist.ContainsKey($hi)) { $hiDist[$hi]+1 } else { 1 }
        if ($samples.Count -lt 8) { $samples += ('0x{0:X8}' -f $bimg) }
    }
}
Write-Host ('无底图(全图)=' + $noBack + ' (' + ('{0:P1}' -f ([double]$noBack/[double]$total)) + ')')
Write-Host ('有底图且BackIndex=0(指向Tiles)的格子=' + $hasBack)
Write-Host ('  当前Draw解码 (bimg&0x1FFFFFFF)-1 越界(>=容量) = ' + $curOOB)
Write-Host ('  假设解码 (bimg&0x7FFF)-1      越界 = ' + $mask7fffOOB)
Write-Host ('  假设解码 (bimg&0xFFFF)-1      越界 = ' + $maskFFFFOOB)
Write-Host ('  BackImage 高16位分布(top):')
foreach ($k in ($hiDist.Keys | Sort-Object -Descending { $hiDist[$k] } | Select-Object -First 8)) {
    Write-Host ('    0x{0:X4} -> {1} 格' -f $k, $hiDist[$k])
}
Write-Host ('  采样 BackImage 原始值: ' + ($samples -join ', '))
