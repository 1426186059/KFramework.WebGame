Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = Get-Item "d:/OpenSource/KFramework.WebGame/KFramework.WebGame/KFramework.Example3/wwwroot/content/myres/atlas.20c1b1da068d6674.web.lib"
$arc = [System.IO.Compression.ZipFile]::OpenRead($z.FullName)
$m = $arc.GetEntry('manifest.json')
$r = New-Object System.IO.StreamReader($m.Open())
$json = $r.ReadToEnd()
$r.Close()
$arc.Dispose()
$o = $json | ConvertFrom-Json
Write-Host ("TotalEntries=" + $o.Entries.Count)
$pages = @{}
foreach ($e in $o.Entries) { if ($e.Page -ge 0) { $pages[$e.Page] = 1 } }
Write-Host ("DistinctPages=" + $pages.Keys.Count)
$pages.Keys | Sort-Object | ForEach-Object { Write-Host ("Page=" + $_) }
$cnt = 0
foreach ($e in $o.Entries) { if ($e.Page -lt 0) { $cnt++ } }
Write-Host ("NonPageEntries=" + $cnt)
foreach ($e in $o.Entries) { Write-Host ($e.Path + " page=" + $e.Page + " x=" + $e.X + " y=" + $e.Y + " w=" + $e.Width + " h=" + $e.Height) }

