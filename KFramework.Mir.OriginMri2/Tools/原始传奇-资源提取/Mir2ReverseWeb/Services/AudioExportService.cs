using System.Text;
using System.Text.Json.Nodes;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 原始传奇音频资源提取：CDN 上没有音频清单（allmanifest 无 audio 条目），音频按
/// assets/resource/audio/{soundId}.mp3 独立寻址。soundId 的唯一可靠来源是配置表里的
/// sound / file 字段（skillEffects、soundnormal、skill 等），本服务递归扫描全部配置分片，
/// 收集所有整数音频 id，再去重、下载、落盘。
/// URL 规则来自游戏 JS：ResUrl.url(id+"", 15) → AudioIcon("audio/") + id + ".mp3"。
/// </summary>
public class AudioExportService
{
    private static readonly HttpClient _http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        // 与 GameConfigService 一致：旧式 UA 才能拿到未乱码的配置中文（id 不受影响，但统一口径）
        h.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 2.0.50727)");
        return h;
    }

    private static string AudioUrl(int id)
        => $"{Mir2Config.CdnBase}/assets/resource/audio/{id}.mp3?ver={Mir2Config.ResourceVersion}";

    /// <summary>
    /// 音频默认落地目录 = Mir2Res/assets/resource/audio/。
    /// Mir2Res 是将来的远程资源根，相对路径与原始传奇 CDN 一致、仅去掉版本号段；
    /// :5080 资源服务器把 /assets/ 映射到该目录，客户端按同相对路径即可加载，无需再回源 CDN。
    /// </summary>
    private static string DefaultAudioDir() => Mir2Config.LocalAudioDir;

    /// <summary>递归收集 JsonNode 里所有音频 id：命中字段名 sound/file/music/audio 且值为正整数。</summary>
    private static void CollectIds(JsonNode? node, HashSet<int> ids)
    {
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                string key = kv.Key.ToLowerInvariant();
                bool audioField = key is "sound" or "file" or "music" or "audio" or "soundid";
                if (audioField && kv.Value is JsonValue v && TryAudioId(v, out int id))
                    ids.Add(id);
                CollectIds(kv.Value, ids);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr) CollectIds(item, ids);
        }
    }

    private static bool TryAudioId(JsonValue v, out int id)
    {
        id = 0;
        try
        {
            if (v.TryGetValue<int>(out int i)) { id = i; }
            else if (v.TryGetValue<string>(out var s) && int.TryParse(s, out i)) { id = i; }
            else return false;
        }
        catch { return false; }
        // 音频 id 已知范围约 1..12000；排除明显是标志位/数量的小值 0 与超大值
        return id > 0 && id <= 200000;
    }

    /// <summary>下载全部配置分片，递归收集音频 id（不落盘）。</summary>
    public async Task<AudioResult> CollectFromConfigAsync(string? baseUrl = null, string? version = null)
    {
        var res = new AudioResult { Messages = new() };
        try
        {
            var urls = Mir2Config.ConfigShardUrls(baseUrl, version).ToList();
            var ids = new HashSet<int>();
            int loaded = 0;
            foreach (var url in urls)
            {
                res.Messages.Add($"[下载配置] {url}");
                byte[] bytes;
                try { bytes = await _http.GetByteArrayAsync(url); }
                catch (Exception ex) { res.Messages.Add($"[跳过] {ex.Message}"); continue; }

                string text;
                try { text = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.GetEncoding("gbk").GetString(bytes); }
                var node = JsonNode.Parse(text);
                CollectIds(node, ids);
                loaded++;
                res.Messages.Add($"[解析] {url}：累计 {ids.Count} 个音频 id");
            }
            if (loaded == 0) { res.Error = "所有配置分片下载失败"; return res; }

            var sorted = ids.OrderBy(x => x).ToList();
            res.Ids = sorted;
            res.Total = sorted.Count;
            res.Ok = true;
            res.Messages.Add($"[完成] 从 {loaded} 个分片收集到 {sorted.Count} 个音频 id");
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>提取音频：Ids 为空时先从配置收集；逐个下载到输出目录 audio/{id}.mp3。</summary>
    public async Task<AudioResult> ExtractAsync(AudioExtractRequest req)
    {
        var res = new AudioResult { Messages = new() };
        try
        {
            var ids = req.Ids?.Where(x => x > 0).Distinct().OrderBy(x => x).ToList() ?? new List<int>();
            if ((ids.Count == 0 && req.FromConfig) || ids.Count == 0)
            {
                var collected = await CollectFromConfigAsync();
                if (!collected.Ok && collected.Total == 0) { res.Error = collected.Error ?? "未能收集音频 id"; return res; }
                ids = collected.Ids;
                res.Messages.AddRange(collected.Messages);
            }
            if (ids.Count == 0) { res.Error = "没有可提取的音频 id（配置表未找到 sound/file 字段）"; return res; }

            res.Total = ids.Count;
            res.Ids = ids;
            string baseDir = string.IsNullOrWhiteSpace(req.OutDir) ? DefaultAudioDir() : req.OutDir!;
            Directory.CreateDirectory(baseDir);

            int n = 0;
            foreach (int id in ids)
            {
                n++;
                string file = Path.Combine(baseDir, $"{id}.mp3");
                string url = AudioUrl(id);
                if (File.Exists(file) && new FileInfo(file).Length > 0)
                {
                    res.Skipped++;
                    continue;
                }
                try
                {
                    byte[] bytes = await _http.GetByteArrayAsync(url);
                    if (bytes.Length == 0) { res.Failed++; res.Messages.Add($"[空] {id}.mp3"); continue; }
                    await File.WriteAllBytesAsync(file, bytes);
                    res.Downloaded++;
                    res.Bytes += bytes.Length;
                }
                catch
                {
                    res.Failed++;   // 404：该 id 无对应音频，正常跳过
                }
                if (n % 50 == 0)
                    res.Messages.Add($"[进度] {n}/{ids.Count}（下载 {res.Downloaded} 跳过 {res.Skipped} 失败 {res.Failed}）");
            }
            res.Messages.Add($"[完成] 输出目录：{baseDir}");
            res.Messages.Add($"[完成] 共 {res.Total}：下载 {res.Downloaded}，跳过 {res.Skipped}，失败 {res.Failed}，{(res.Bytes / 1048576.0):F1} MB");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }
}
