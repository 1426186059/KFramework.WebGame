using Shared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 字符串键语言管理器：直接用 LanguageMgr.Get("Key") 取文本，不再经过枚举。
/// JSON 配置表直接反序列化为 Dictionary&lt;string, string&gt;：{ "Key": "文本" }。
/// </summary>
public static class LanguageMgr
{
    private static Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);

    static LanguageMgr()
    {
        foreach (KeyValuePair<string, string> pair in GameLanguage.ServerTextMap.Text)
        {
            Texts[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, string> pair in GameLanguage.ClientTextMap.Text)
        {
            if (!Texts.ContainsKey(pair.Key))
            {
                Texts[pair.Key] = pair.Value;
            }
        }
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (Texts.TryGetValue(key, out string value))
        {
            return value;
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

    public static void Load(Dictionary<string, string> language)
    {
        Texts = language ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        LoadFromJson(File.ReadAllText(path));
    }
}
