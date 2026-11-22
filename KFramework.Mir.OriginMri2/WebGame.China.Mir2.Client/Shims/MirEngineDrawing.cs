namespace MirEngine
{
    public static class ColorTranslator
    {
        public static Color FromHtml(string htmlColor)
        {
            if (string.IsNullOrEmpty(htmlColor)) return CommonColor.Empty;
            string name = htmlColor.Trim();
            string h = name;
            if (h.StartsWith("#")) h = h.Substring(1);
            if (h.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)) h = h.Substring(2);
            if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length == 4) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}{h[3]}{h[3]}";
            // 与 .NET 原版 ColorTranslator.FromHtml 一致：hex 按 #AARRGGBB / #ARGB 解释（alpha 在前）。
            // 6 位：#RRGGBB（不透明）；8 位：#AARRGGBB；4 位：#ARGB。
            if (h.Length == 6 && uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out uint v))
                return CommonColor.FromArgb(255, (int)((v >> 16) & 0xFF), (int)((v >> 8) & 0xFF), (int)(v & 0xFF));
            if (h.Length == 8 && uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out uint v8))
                return CommonColor.FromArgb((int)((v8 >> 24) & 0xFF), (int)((v8 >> 16) & 0xFF), (int)((v8 >> 8) & 0xFF), (int)(v8 & 0xFF));
            return CommonColor.FromName(name);
        }
    }
}
