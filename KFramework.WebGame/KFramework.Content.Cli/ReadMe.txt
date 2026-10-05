KFramework.Content.Cli（kfc）— 资源打包工具：外部工具与依赖说明
============================================================

本目录是 KFramework 的资源打包命令行工具（编译产物名为 kfc）。
打包资源包（AssetBundle）时，图片纹理可编码为 KTX2（Basis 超压缩 GPU 纹理），
也可选 PNG / WebP（RGBA 中间格式，运行端借浏览器原生解码）。

以下是构建与运行本工具所需的外部工具及获取方式（下载地址）。

----------------------------------------------------------------------
一、必装：basisu（Basis Universal 命令行工具）
----------------------------------------------------------------------
用途：
  将图集页（PNG）编码为 KTX2 / UASTC GPU 压缩纹理。
  本工具通过 Process.Start 调用 basisu 命令行，实际参数形如：
    basisu -file x.png -ktx2 -uastc -uastc_level <q> -mipmap -output_file y.ktx2

下载 / 获取地址：
  1) 官方源码仓库（推荐，可拿到最新 v2.x）：
     https://github.com/BinomialLLC/basis_universal
     - 官方 GitHub Release 只提供 1.0 ~ 1.16.4 的预编译包；最新版需本地编译。
     - 编译示例（以带 MSVC 的 Visual Studio 为例，需安装“使用 C++ 的桌面开发”）：
         git clone --recursive --branch v2_50 https://github.com/BinomialLLC/basis_universal.git
         cd basis_universal
         cmake -S . -B build
         cmake --build build --config Release --target basisu
       产物为 build/Release/basisu.exe（或 bin/basisu.exe）。
     - 重要：旧版 1.16.x 在 “KTX2 + UASTC + mipmap” 路径上会崩溃（0xC0000005），
       必须使用编译得到的 v2.x。本项目已验证 v2.50.0 可用（能正常产出 KTX2）。

  2) 旧版预编译二进制（仅 1.16.4 及更早，不推荐用于本项目的 KTX2 流程）：
     https://github.com/BinomialLLC/basis_universal/releases
     资产形如 basisu_v1_16_4.7z（解压即得 basisu.exe）。

放置位置 / 配置：
  - 将编译好的 basisu.exe 放到本仓库的 Need_DLL/ 目录（即 Need_DLL/basisu.exe）；
    或放到任意位置，并在 Content 的 build.config.json 中设置：
        "basisuPath": "../../Need_DLL/basisu.exe"
    （路径相对 Content 根目录解析为绝对路径；留空则使用 PATH 中的 basisu。）
  - 该配置由 KFramework.Content.Cli/Pipeline/BuildConfig.cs 读取，经 Program.cs
    传入打包选项的 BasisuPath。

----------------------------------------------------------------------
二、已随仓库分发：KTexturePacker.Core.dll
----------------------------------------------------------------------
用途：
  图集打包核心（MaxRects 摆放 + 整页合成 + AtlasData 导出），本项目复用其 .NET 核心库
  （见 csproj 中对 ..\Need_DLL\KTexturePacker.Core.dll 的引用）。
位置：
  Need_DLL/KTexturePacker.Core.dll —— 已随仓库提供，正常情况无需单独下载。
若需重新获取：
  从 KTexturePacker 上游项目的 .NET 核心程序集取得同名 DLL，放回 Need_DLL/ 即可。

----------------------------------------------------------------------
三、NuGet 依赖（自动还原，无需手动下载）
----------------------------------------------------------------------
  - SkiaSharp（PNG/JPG 读写与位图处理）            —— 通过 NuGet 自动还原。
  - KFramework.MonoGame（项目引用）                —— 同解决方案内引用。
执行 `dotnet build` 时会自动还原以上依赖。

----------------------------------------------------------------------
四、可选：本地调试 Web 服务（仅 deploy=serve 时需要）
----------------------------------------------------------------------
用途：
  kfc 的 deploy=serve 会拉起本地静态服务器预览打包产物（Ctrl+C 退出）。
  工具依次尝试：python（-m http.server）/ python3 / npx http-server。
下载地址：
  - Python：  https://www.python.org/downloads/
  - Node.js（含 npx）：https://nodejs.org/

----------------------------------------------------------------------
五、运行端配套（非本工具依赖，但 KTX2 纹理需此才能上 GPU）
----------------------------------------------------------------------
Web 运行端通过浏览器原生 Basis 转码器（basis_transcoder.js / .wasm）
将 KTX2 转码为设备原生压缩格式（ASTC / BC7 / DXT 等），无需再安装任何工具。

----------------------------------------------------------------------
六、代码规范（C# 编码约定）
----------------------------------------------------------------------
禁止使用 C# 的「switch 表达式」写法（即 `x switch { A => ..., B => ... }`
这种带箭头 `=>` 的简化语法），一律改用传统的 `switch` 语句
（`case ...: return ...;` 或 `case ...: ...; break;`）。

禁止（✗）：
    return mFormat switch
    {
        ContentTextureDataFormat.Png  => ".png",
        ContentTextureDataFormat.Webp => ".webp",
        _ => ".png",
    };

允许（✓）：
    switch (mFormat)
    {
        case ContentTextureDataFormat.Png:  return ".png";
        case ContentTextureDataFormat.Webp: return ".webp";
        default: return ".png";
    }

原因：保持代码风格统一、可读性一致，避免表达式与语句两种风格混用。
