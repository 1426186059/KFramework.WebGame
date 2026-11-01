$dir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/js'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$urls = @(
    'https://cdn.yscq-wy.yscq.com/fugu_20201203/js/eui.min.js?v=229',
    'https://cdn.yscq-wy.yscq.com/fugu_20201203/js/game.min.js?v=229',
    'https://cdn.yscq-wy.yscq.com/fugu_20201203/qufu1.min.js?v=229',
    'https://cdn.yscq-wy.yscq.com/fugu_20201203/main10.25487.1.min.js',
    'https://cdn.yscq-wy.yscq.com/fugu_20201203/js/assetsmanager.min.js?v=229',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/index.fe8ba9ea.js',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/8872.34227955.js',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/6594.0da04a76.js',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/5266.55c8e73a.js',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/8450.0a0b1e7d.js',
    'https://fenwan.cdn.bcebos.com/webgame/static/webgame/js/2385.9583ea77.js'
)

foreach ($u in $urls) {
    # 取 URL 路径里的文件名（去掉查询串）
    $name = $u.Split('?')[0]
    $name = $name.Substring($name.LastIndexOf('/') + 1)
    $out = Join-Path $dir $name
    try {
        Invoke-WebRequest -Uri $u -OutFile $out -TimeoutSec 300 -ErrorAction Stop
        $len = (Get-Item $out).Length
        Write-Output ("OK   " + $name + "  len=" + $len)
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Output ("FAIL " + $name + "  (" + $u + ")  code=" + $code)
    }
}
