using KFramework.MonoGame;

namespace Client.MirGraphics
{
    /// <summary>
    /// 传奇用到的混合模式枚举（游戏层统一抽象）。引擎实际混合态是 <see cref="BlendState"/>，
    /// 通过 <see cref="MirBlendExtensions.ToBlendState"/> 转换。游戏代码一律引用本枚举，不要再直接写 BlendState。
    /// <para>各值 → 引擎对应关系：</para>
    /// <list type="bullet">
    ///   <item><term>Normal</term><description>正常 alpha 合成 = 引擎 NonPremultiplied（已修正：α 源因子=One，alpha 保持直线 a；修掉半透明漏底 / 整图变透）。</description></item>
    ///   <item><term>Additive</term><description>加法发光 / 叠加 = 引擎 Additive（等价原版 SetBlend(true)）。</description></item>
    ///   <item><term>Multiply</term><description>乘法压暗 = 引擎 Multiply（传奇昼夜 / 光照系统把光照图乘回主画面）。</description></item>
    ///   <item><term>AlphaBlend</term><description>预乘读取 = 引擎 AlphaBlend（颜色源=One，按已预乘方式读取预乘 RT）。</description></item>
    /// </list>
    /// </summary>
    public enum MirBlend
    {
        /// <summary>正常 alpha 合成（引擎 NonPremultiplied 修复版）。</summary>
        Normal,
        /// <summary>加法发光 / 叠加特效（引擎 Additive）。</summary>
        Additive,
        /// <summary>乘法压暗（引擎 Multiply；昼夜 / 光照）。</summary>
        Multiply,
        /// <summary>预乘读取（引擎 AlphaBlend；读取预乘 RT 用）。</summary>
        AlphaBlend,
    }

    /// <summary>
    /// MirBlend → 引擎 BlendState 的转换，集中在此一处；换引擎混合只改这里。
    /// </summary>
    public static class MirBlendExtensions
    {
        public static BlendState ToBlendState(this MirBlend blend) => blend switch
        {
            MirBlend.Normal => BlendState.NonPremultiplied,
            MirBlend.Additive => BlendState.Additive,
            MirBlend.Multiply => BlendState.Multiply,
            MirBlend.AlphaBlend => BlendState.AlphaBlend,
            _ => BlendState.NonPremultiplied,
        };
    }
}
