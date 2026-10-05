namespace KFramework.Content.Cli
{
    public static class BuildOptions
    {
        /// <summary>单张图集的边长上限。</summary>
        public static int AtlasMaxSize { get; set; } = 2048;

        /// <summary>图集内相邻精灵的间隔。</summary>
        public static int AtlasPadding { get; set; } = 2;

        /// <summary>是否额外输出 atlas_N.png 预览图，方便用看图工具检查发布结果；预览图写到配置 tempDir 指定的临时目录（默认 Content/temp），不随 outDir 发布。</summary>
        public static bool WritePreviewPng { get; set; } = true;

        /// <summary>是否裁掉精灵四周的透明边。</summary>
        public static bool TrimSprites { get; set; } = false;

        /// <summary>KTX2（Basis UASTC）质量等级 0~4，越大越好越慢。仅 <c>TextureFormat=Ktx2</c> 时生效。</summary>
        public static int Ktx2Quality { get; set; } = 2;
    }
}
