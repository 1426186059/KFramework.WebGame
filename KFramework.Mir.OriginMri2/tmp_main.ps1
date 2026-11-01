$cdn = 'https://cdn.yscq-wy.yscq.com/fugu_20201203/resource'
$out = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考/main10.25487.1.min.js'
$urls = @(
    "$cdn/main10.25487.1.min.js",
    "$cdn/main10.25487.1.min.js?v=20261029113003",
    "$cdn/main10.25487.1.min.js?20261029113003"
)
foreach ($u in $urls) {
    try {
        Invoke-WebRequest -Uri $u -OutFile $out -TimeoutSec 180 -ErrorAction Stop
        $len = (Get-Item $out).Length
        Write-Output ("OK   len=" + $len + "  " + $u)
        break
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Output ("FAIL " + $code + "  " + $u)
    }
}
