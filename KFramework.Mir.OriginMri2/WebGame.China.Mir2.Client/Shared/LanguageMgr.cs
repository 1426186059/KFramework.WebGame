using KFramework.MonoGame;
using System.Text;

/// <summary>
/// 字符串键语言管理器：直接从 JSON 加载扁平字典，LanguageMgr.Get("Key") 取文本。
/// </summary>
public static class LanguageMgr
{
    private static Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (Texts.TryGetValue(key, out string value))
        {
            return value ?? string.Empty;
        }

        return key;
    }

    public static string Get(string key, params object[] args)
    {
        string text = Get(key);
        if (args == null || args.Length == 0)
        {
            return text;
        }

        return string.Format(text, args);
    }

    public static void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        Texts = JsonTool.FromJson<Dictionary<string, string>>(json, AppJsonContext.Default.LanguageTextDictionary)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static async Task LoadLanguageAsync(string languageJsonPath)
    {
        byte[] data = await BrowserResource.GetBytesAsync(RemoteWebSetting.LanguageBaseRootDir + languageJsonPath).ConfigureAwait(false);
        LoadFromJson(Encoding.UTF8.GetString(data));
    }

    public static string ToLocalizedString(this Enum @enum)
    {
        string key = $"{@enum.GetType().Name}_{@enum}";
        string text = Get(key);
        return text == key ? @enum.ToString() : text;
    }
}
