namespace PixiJS;

/// <summary>对应 PixiJS Text：文本对象。font 形如 "bold 18px system-ui, sans-serif"。</summary>
public sealed class PixiText : PixiObject
{
    private string _text = "";

    public PixiText(string text, string font, Color fill, string align = "center")
        : base(PixiApi.Create("text"))
    {
        PixiApi.TextStyle(Handle, font, fill.ToCss(), align);
        _text = text;
        PixiApi.TextSet(Handle, text);
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PixiApi.TextSet(Handle, value);
        }
    }
}
