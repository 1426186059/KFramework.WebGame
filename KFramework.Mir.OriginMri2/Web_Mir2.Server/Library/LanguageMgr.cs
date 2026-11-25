using Shared.Helpers;
using System;
using System.Collections.Generic;
using System.IO;

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

    public static void Load(Dictionary<string, string> language)
    {
        Texts = language ?? new Dictionary<string, string>(StringComparer.Ordinal);
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

    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        LoadFromJson(File.ReadAllText(path));
    }

    public static void LoadServerLanguage(string languageJsonPath)
    {
        Load(languageJsonPath);
    }

    public static void SaveServerLanguage(string languageJsonPath)
    {
        File.Delete(languageJsonPath);
        File.WriteAllText(languageJsonPath, JsonTool.ToJson(Texts, AppJsonContext.Default.LanguageTextDictionary));
    }

    public static string ToLocalizedString(this Enum @enum)
    {
        string key = $"{@enum.GetType().Name}_{@enum}";
        string text = Get(key);
        return text == key ? @enum.ToString() : text;
    }
}
