$base = 'https://cdn.yscq-wy.yscq.com/fugu_20201203'
$r = Invoke-WebRequest -Uri ($base + '/index.html') -TimeoutSec 40
[System.IO.File]::WriteAllText('D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/tmp_index.html', $r.Content, [System.Text.Encoding]::UTF8)
Write-Output ('saved tmp_index.html len=' + $r.Content.Length)
# 打印所有 script / main / js 相关片段
foreach ($mm in [regex]::Matches($r.Content, '.{40}(main|Main|MAIN|\.js|game|a?min\.js).{40}')) {
    Write-Output $mm.Value
}
