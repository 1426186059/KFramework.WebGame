using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>富文本排版结果，对标 TMP_Text.textInfo：含逐字符、逐行、逐链接信息。
    /// 坐标均相对排版矩形原点 (0,0)，由 RichTextObject 在排版时一次性构建并缓存。</summary>
    public sealed class RichTextInfo
    {
        /// <summary>排版时用的矩形（屏幕坐标），供把鼠标位置转成相对坐标。</summary>
        public RectangleF Bounds { get; internal set; }

        public IReadOnlyList<RichCharacterInfo> Characters => _characters;
        public IReadOnlyList<RichLineInfo> Lines => _lines;
        public IReadOnlyList<RichLinkInfo> Links => _links;

        public int CharacterCount => _characters.Count;
        public int LineCount => _lines.Count;
        public int LinkCount => _links.Count;

        private readonly List<RichCharacterInfo> _characters = new List<RichCharacterInfo>();
        private readonly List<RichLineInfo> _lines = new List<RichLineInfo>();
        private readonly List<RichLinkInfo> _links = new List<RichLinkInfo>();

        internal void Clear()
        {
            _characters.Clear();
            _lines.Clear();
            _links.Clear();
        }

        internal void AddCharacter(RichCharacterInfo c) => _characters.Add(c);
        internal void AddLine(RichLineInfo l) => _lines.Add(l);

        internal int AddLink(RichLinkInfo l)
        {
            int index = _links.Count;
            _links.Add(l);
            return index;
        }

        /// <summary>更新某个链接的字符范围（同一 url 跨行/分段时合并）。</summary>
        internal void ExtendLink(int index, int startChar, int endChar)
        {
            RichLinkInfo cur = _links[index];
            _links[index] = new RichLinkInfo(cur.Url,
                                              Math.Min(cur.StartChar, startChar),
                                              Math.Max(cur.EndChar, endChar));
        }
    }

    /// <summary>单个字符的排版信息，对标 TMP_CharacterInfo。基线与升降部已按该字符字号缩放好。</summary>
    public readonly record struct RichCharacterInfo(
        char Character,
        int Index,          // 在 PlainText 中的索引
        int Line,           // 所在行号
        float X,            // 字形左上角 X（相对排版原点）
        float Width,        // 字符推进宽度（已缩放）
        float BaselineY,    // 该字符基线 Y（相对排版原点，已落到行基线）
        float Ascent,       // 基线→字顶（已缩放）
        float Descent,      // 基线→字底（已缩放）
        Color Color,        // 最终颜色（已含 foreColor 兜底）
        int LinkIndex);     // 所属 link 在 TextInfo.Links 中的索引，-1 表示非链接

    /// <summary>一行信息，对标 TMP_LineInfo。</summary>
    public readonly record struct RichLineInfo(
        int StartChar,      // 行首字符在 Characters 中的索引
        int EndChar,        // 行尾字符的下一个索引（开区间）
        float Top,          // 行顶 Y（相对排版原点）
        float Height);      // 行高

    /// <summary>一个超链接信息，对标 TMP_LinkInfo（同一 url 合并为一条）。</summary>
    public readonly record struct RichLinkInfo(
        string Url,
        int StartChar,      // 覆盖字符在 Characters 中的起始索引
        int EndChar);       // 结束索引（开区间）
}
