using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 跨平台本地存储工具。
    /// 基于 System.IO + Environment.SpecialFolder.LocalApplicationData，
    /// Windows/Linux/macOS/Android/iOS 均可原样使用，无需平台判断。
    /// </summary>
    public static class KStorage
    {
        /// <summary>存档根目录下的游戏子目录名，可自行修改。</summary>
        public static string GameName { get; set; } = "FCGame";

        private static string _root;

        /// <summary>存档根目录（平台映射 + 游戏子目录），首次访问时自动创建。</summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;

                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(baseDir))
                {
                    // 极少数环境 GetFolderPath 可能返回空，退回工作目录
                    baseDir = Directory.GetCurrentDirectory();
                }

                _root = Path.Combine(baseDir, GameName);
                Directory.CreateDirectory(_root);
                return _root;
            }
        }

        /// <summary>获取某文件在存档目录中的完整路径。</summary>
        public static string GetSavePath(string fileName) => Path.Combine(Root, fileName);

        /// <summary>文件是否存在。</summary>
        public static bool Exists(string fileName) => File.Exists(GetSavePath(fileName));

        /// <summary>写入文本（UTF-8），自动创建目录。</summary>
        public static void WriteAllText(string fileName, string text)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(GetSavePath(fileName), text, Encoding.UTF8);
        }

        /// <summary>读取文本，文件不存在返回 null。</summary>
        public static string ReadAllText(string fileName)
        {
            string path = GetSavePath(fileName);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        /// <summary>写入字节。</summary>
        public static void WriteAllBytes(string fileName, byte[] bytes)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllBytes(GetSavePath(fileName), bytes);
        }

        /// <summary>读取字节，文件不存在返回 null。</summary>
        public static byte[] ReadAllBytes(string fileName)
        {
            string path = GetSavePath(fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>追加一行文本，文件不存在则新建。</summary>
        public static void AppendLine(string fileName, string text)
        {
            Directory.CreateDirectory(Root);
            File.AppendAllLines(GetSavePath(fileName), new[] { text }, Encoding.UTF8);
        }

        /// <summary>删除文件，不存在时静默忽略。</summary>
        public static void Delete(string fileName)
        {
            string path = GetSavePath(fileName);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>清空整个存档目录。</summary>
        public static void ClearAll()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
                Directory.CreateDirectory(Root);
            }
        }

        /// <summary>列出存档目录下全部文件名。</summary>
        public static string[] GetAllFiles() => Directory.Exists(Root) ? Directory.GetFiles(Root) : Array.Empty<string>();

        /// <summary>对象序列化为 JSON 并写入。</summary>
        public static void WriteJson<T>(string fileName, T data)
        {
            WriteAllText(fileName, JsonSerializer.Serialize(data));
        }

        /// <summary>读取并反序列化 JSON；文件不存在或解析失败返回 default。</summary>
        public static T ReadJson<T>(string fileName)
        {
            string json = ReadAllText(fileName);
            if (json == null) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}
