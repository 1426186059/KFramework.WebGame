namespace KFramework.MonoGame
{
    /// <summary>富文本工具类，对标 TMPro.TMP_TextUtilities：提供链接/字符命中查询。</summary>
    public static class RichTextUtil
    {
        /// <summary>查找与鼠标位置相交的超链接，返回 link 在 TextInfo.Links 中的索引，未命中返回 -1。
        /// 对标 TMP_TextUtilities.FindIntersectingLink；取 URL 用 text.TextInfo.Links[index].Url。
        /// 复用最近一次排版生成的 TextInfo，不重新布局。</summary>
        public static int FindIntersectingLink(RichTextObject text, Vector2 position)
        {
            RichTextInfo info = text?.TextInfo;
            if (info == null || info.LinkCount == 0)
            {
                return -1;
            }

            // 屏幕坐标 → 相对排版原点
            float px = position.X - info.Bounds.X;
            float py = position.Y - info.Bounds.Y;

            for (int li = 0; li < info.LinkCount; li++)
            {
                RichLinkInfo link = info.Links[li];
                for (int ci = link.StartChar; ci < link.EndChar && ci < info.CharacterCount; ci++)
                {
                    RichCharacterInfo ch = info.Characters[ci];
                    float left = ch.X;
                    float right = ch.X + ch.Width;
                    float top = ch.BaselineY - ch.Ascent;
                    float bottom = ch.BaselineY + ch.Descent;
                    if (px >= left && px <= right && py >= top && py <= bottom)
                    {
                        return li;
                    }
                }
            }
            return -1;
        }

        /// <summary>查找与鼠标位置相交的字符索引，未命中返回 -1。对标 TMP_TextUtilities.FindIntersectingCharacter。</summary>
        public static int FindIntersectingCharacter(RichTextObject text, Vector2 position)
        {
            RichTextInfo info = text?.TextInfo;
            if (info == null)
            {
                return -1;
            }
            float px = position.X - info.Bounds.X;
            float py = position.Y - info.Bounds.Y;
            for (int ci = 0; ci < info.CharacterCount; ci++)
            {
                RichCharacterInfo ch = info.Characters[ci];
                float left = ch.X;
                float right = ch.X + ch.Width;
                float top = ch.BaselineY - ch.Ascent;
                float bottom = ch.BaselineY + ch.Descent;
                if (px >= left && px <= right && py >= top && py <= bottom)
                {
                    return ci;
                }
            }
            return -1;
        }
    }
}
