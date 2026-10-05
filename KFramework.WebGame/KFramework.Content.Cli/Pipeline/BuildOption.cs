using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public sealed class BuildOptions
    {
        /// <summary>单张图集的边长上限。</summary>
        public int AtlasMaxSize { get; set; } = 2048;

        /// <summary>图集内相邻精灵的间隔。</summary>
        public int AtlasPadding { get; set; } = 2;

        /// <summary>是否额外输出 atlas_N.png 预览图，方便用看图工具检查发布结果；预览图写到配置 tempDir 指定的临时目录（默认 Content/temp），不随 outDir 发布。</summary>
        public bool WritePreviewPng { get; set; } = true;

        /// <summary>是否裁掉精灵四周的透明边。</summary>
        public bool TrimSprites { get; set; } = true;

        /// <summary>图集页（整图纹理）的统一转换目标（见 <see cref="ContentTextureSwitchTarget"/>）：
        /// <c>Rgba</c>（默认，裸 RGBA8，运行端零解码、直接上传 GPU）/ <c>Webp</c>（编码 WebP，体积更小，运行端借浏览器原生解码）/
        /// <c>Ktx2</c>（KTX2/Basis 超压缩 GPU 纹理，显存与上传开销最低，构建端需 basisu，运行端需浏览器 Basis 转码器）/
        /// <c>None</c>（不转码，保留每张图自身的 <see cref="ContentTextureDataFormat"/> 原图格式）。
        /// Png 等具体数据格式不作为统一目标，而是 <see cref="ContentTextureDataFormat"/> 中的可原样保留格式。
        /// 可在 build.config.json 的 <c>textureFormat</c> 配置，或用 kfc --format 覆盖。</summary>
        public ContentTextureSwitchTarget TextureSwitchTarget { get; set; } = ContentTextureSwitchTarget.Webp;

        /// <summary>basisu 可执行文件路径（<c>Ktx2</c> 编码用）。为空则用 PATH 中的 "basisu"。</summary>
        public string? BasisuPath { get; set; }

        /// <summary>KTX2（Basis UASTC）质量等级 0~4，越大越好越慢。仅 <c>TextureFormat=Ktx2</c> 时生效。</summary>
        public int Ktx2Quality { get; set; } = 2;
    }
}
