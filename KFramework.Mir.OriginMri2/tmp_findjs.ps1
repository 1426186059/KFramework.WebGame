$manifest = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/allmanifest10.25487.1.json'
$text = [System.IO.File]::ReadAllText($manifest, [System.Text.Encoding]::UTF8)
# 找所有 .js（非 .sheet.json）的 key
$keys = [regex]::Matches($text, '"(resource/[^"]*\.js)"')
Write-Output ("=== manifest 中的 .js 入口 (" + $keys.Count + ") ===")
foreach ($m in $keys) { Write-Output $m.Groups[1].Value }

# 抓 index.html 看主脚本引用
$base = 'https://cdn.yscq-wy.yscq.com/fugu_20201203'
foreach ($p in @('/index.html','/','/fugu/index.html')) {
    try {
        $html = Invoke-WebRequest -Uri ($base + $p) -TimeoutSec 40 -ErrorAction Stop
        $body = $html.Content
        Write-Output ("=== index.html @ $p (len=" + $body.Length + ") ===")
        foreach ($mm in [regex]::Matches($body, '<script[^>]*src="([^"]+)"')) {
            Write-Output ('  script: ' + $mm.Groups[1].Value)
        }
        foreach ($mm in [regex]::Matches($body, '(main[a-zA-Z0-9_.-]*\.js)')) {
            Write-Output ('  mainjs: ' + $mm.Groups[1].Value)
        }
        break
    } catch {
        Write-Output ("index.html @ $p ERR=" + $_.Exception.Response.StatusCode.value__)
    }
}
