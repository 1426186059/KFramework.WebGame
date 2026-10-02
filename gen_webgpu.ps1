$repo = 'D:\OpenSource\KFramework.WebGame'
$src = Join-Path $repo 'KFramework.WebGame\KFramework.MonoGame\JSBind\JSBind_WEBGL20.cs'
$dst = Join-Path $repo 'KFramework.WebGame\KFramework.MonoGame\JSBind\JSBind_WebGPU.cs'

$t = [IO.File]::ReadAllText($src, [Text.Encoding]::UTF8)
$t = $t.Replace('JSBind_WEBGL20', 'JSBind_WebGPU')
$t = $t.Replace('"render_webgl20"', '"render_webgpu"')
$t = $t.Replace(
    'WebGL 2.0 的底层绑定。所有方法一对一映射到 <c>JSBind_WebGPU.xxx</c>，由 KFramework.TSEngine/src/render_webgl20.ts 编译出的 wwwroot/jsengine/render_webgl20.js 提供实现（本绑定依赖 KFramework.TSEngine 项目）。',
    'WebGPU 的底层绑定（脚手架）。方法一对一映射自 <c>JSBind_WebGPU.xxx</c>；对应 TS 模块 KFramework.TSEngine/src/render_webgpu.ts 尚未实现，此处仅先立起 C# 绑定结构（模块名 "render_webgpu"）。')

[IO.File]::WriteAllText($dst, $t, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "wrote $dst"
