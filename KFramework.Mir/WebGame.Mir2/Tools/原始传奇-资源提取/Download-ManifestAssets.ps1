param(
  [string]$ManifestPath = 'C:\Users\Administrator\Downloads\allmanifest10.25341.1.json',
  [string]$OutDir       = 'D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Tools\原始传奇-资源提取\assets',
  [int]   $Throttle      = 16,
  [int]   $Retries       = 3
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$CDN = 'https://cdn.yscq-wy.yscq.com/fugu_20201203/resource/'

# ---- 解析 manifest ----
if (-not (Test-Path $ManifestPath)) { throw "manifest 不存在: $ManifestPath" }
$m = Get-Content $ManifestPath -Raw -Encoding utf8 | ConvertFrom-Json

$items = @()
foreach ($prop in $m.PSObject.Properties) {
  $k = $prop.Name
  $v = $prop.Value.v
  $s = $prop.Value.s
  if (-not $v -or $v.Length -lt 2 -or $null -eq $s -or $s -le 0) { Write-Warning "跳过(无效 v/s): $k"; continue }
  $ext = [System.IO.Path]::GetExtension($k).TrimStart('.')
  if (-not $ext) { $ext = 'bin' }
  $folder = $v.Substring(0, 2)
  $url    = $CDN + $folder + '/' + $v + '_' + $s + '.' + $ext
  $local  = Join-Path $OutDir $k   # 用 manifest 逻辑名作本地路径, 保证 png/json 配对
  $items += [pscustomobject]@{ key = $k; url = $url; local = $local; s = [int64]$s }
}
Write-Host "待下载条目数 = $($items.Count)"

# ---- 并行下载 (runspace 池) ----
$scriptblock = {
  param($url, $local, $s, $retries)
  try {
    $dir = Split-Path $local
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $ok = $false
    for ($i = 0; $i -lt $retries -and -not $ok; $i++) {
      $wc = $null
      try {
        $wc = New-Object System.Net.WebClient
        $wc.DownloadFile($url, $local)
        if ((Get-Item $local).Length -eq $s) { $ok = $true }
        else { throw "size mismatch ($( (Get-Item $local).Length ) != $s)" }
      }
      catch {
        if (Test-Path $local) { Remove-Item $local -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 300
      }
      finally { if ($wc) { $wc.Dispose() } }
    }
    return $ok
  }
  catch { return $false }
}

$pool = [runspacefactory]::CreateRunspacePool(1, $Throttle)
$pool.Open()
$jobs = @()
foreach ($it in $items) {
  $ps = [powershell]::Create().AddScript($scriptblock).AddArgument($it.url).AddArgument($it.local).AddArgument($it.s).AddArgument($Retries)
  $ps.RunspacePool = $pool
  $jobs += [pscustomobject]@{ handle = $ps.BeginInvoke(); ps = $ps; item = $it }
}

$ok = 0; $fail = 0; $failed = @()
$total = $jobs.Count
$counter = 0
foreach ($j in $jobs) {
  $counter++
  $res = $j.ps.EndInvoke($j.handle)
  if ($res -and $res -eq $true) { $ok++ }
  else { $fail++; $failed += $j.item.key }
  if ($counter % 100 -eq 0) { Write-Host "进度 $counter/$total  成功=$ok 失败=$fail" }
}
$pool.Close()

Write-Host "================ 完成 ================"
Write-Host "成功=$ok  失败=$fail  总计=$total"
if ($failed.Count) {
  $failedPath = Join-Path (Split-Path $OutDir) 'download_failed.txt'
  $failed | Out-File $failedPath -Encoding utf8
  Write-Host "失败列表 -> $failedPath"
}
