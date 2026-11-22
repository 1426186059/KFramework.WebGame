namespace MirEngine
{
    // 保留 Color4（SlimDX shim），Color 已统一为 KFramework.MonoGame.Color。
    public struct Color4
    {
        public float Red, Green, Blue, Alpha;

        public Color4(float alpha, float red, float green, float blue)
        {
            Alpha = alpha; Red = red; Green = green; Blue = blue;
        }
        public Color4(KFramework.MonoGame.Color c)
        {
            Alpha = c.A / 255f; Red = c.R / 255f; Green = c.G / 255f; Blue = c.B / 255f;
        }
        public static implicit operator Color4(KFramework.MonoGame.Color c) => new Color4(c);
    }
}
