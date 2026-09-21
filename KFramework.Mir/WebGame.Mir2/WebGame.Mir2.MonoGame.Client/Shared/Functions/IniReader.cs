using System.Threading.Tasks;
using MirEngine;

public class InIReader
{
    #region Fields
    private readonly List<string> _contents;
    private readonly string _fileName;
    private bool _loaded;
    private bool _autoPersist = true;
    #endregion

    #region Constructor
    public InIReader(string fileName)
    {
        _fileName = fileName;
        _contents = new List<string>();
        // 浏览器端不再读写本地文件：整份 ini 作为文本序列化后存取于 IndexedDB（见 LoadAsync/SaveAsync）。
    }
    #endregion

    #region Functions
    private string FindValue(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
            if (String.CompareOrdinal(_contents[a], "[" + section + "]") == 0)
                for (int b = a + 1; b < _contents.Count; b++)
                    if (String.CompareOrdinal(_contents[b].Split('=')[0], key) == 0)
                        return _contents[b].Split('=')[1];
                    else if (_contents[b].StartsWith("[") && _contents[b].EndsWith("]"))
                        return null;
        return null;
    }

    private int FindIndex(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
            if (String.CompareOrdinal(_contents[a], "[" + section + "]") == 0)
                for (int b = a + 1; b < _contents.Count; b++)
                    if (String.CompareOrdinal(_contents[b].Split('=')[0], key) == 0)
                        return b;
                    else if (_contents[b].StartsWith("[") && _contents[b].EndsWith("]"))
                    {
                        _contents.Insert(b - 1, key + "=");
                        return b - 1;
                    }
                    else if (_contents.Count - 1 == b)
                    {
                        _contents.Add(key + "=");
                        return _contents.Count - 1;
                    }
        if (_contents.Count > 0)
            _contents.Add("");

        _contents.Add("[" + section + "]");
        _contents.Add(key + "=");
        return _contents.Count - 1;
    }

    // ---- 浏览器端持久化：整份 ini 作为纯文本存取于 localStorage（MirEngine.LocalStorage → JSBind_LocalStorage） ----
    // 文件名即 DB 键；加载为异步操作，须 await LoadAsync() 之后才能读取/写入有效数据。
    private string DbKey => _fileName;

    /// <summary>是否已从浏览器 DB 载入（_contents 是否反映存储内容）。</summary>
    public bool IsLoaded => _loaded;

    /// <summary>是否自动落库：Write 之后是否立即异步持久化。批量读取/写入期间建议关闭，结束后再显式 SaveAsync 一次以减少写次数。</summary>
    public bool AutoPersist
    {
        get => _autoPersist;
        set => _autoPersist = value;
    }

    public bool IsEmpty => _contents.Count == 0;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        try
        {
            string text = await LocalStorage.GetStringAsync(DbKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(text))
                _contents.AddRange(text.Replace("\r\n", "\n").Split('\n'));
        }
        catch
        {
        }
        _loaded = true;
    }

    /// <summary>显式把整份内容持久化到浏览器 DB（等待完成）。</summary>
    public async Task SaveAsync()
    {
        try
        {
            await LocalStorage.SetStringAsync(DbKey, string.Join("\n", _contents)).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    /// <summary>同步兼容：触发一次异步落库（不等待）。受 AutoPersist 与已加载状态约束（Write 的自动落库走这里）。</summary>
    public void Save()
    {
        if (!_autoPersist || !_loaded) return;
        _ = SaveAsync();
    }
    #endregion

    #region Read
    public bool ReadBoolean(string section, string key, bool Default, bool writeWhenNull = true)
    {
        bool result;

        if (!bool.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public byte ReadByte(string section, string key, byte Default, bool writeWhenNull = true)
    {
        byte result;

        if (!byte.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public sbyte ReadSByte(string section, string key, sbyte Default, bool writeWhenNull = true)
    {
        sbyte result;

        if (!sbyte.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public ushort ReadUInt16(string section, string key, ushort Default, bool writeWhenNull = true)
    {
        ushort result;

        if (!ushort.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public short ReadInt16(string section, string key, short Default, bool writeWhenNull = true)
    {
        short result;

        if (!short.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public uint ReadUInt32(string section, string key, uint Default, bool writeWhenNull = true)
    {
        uint result;

        if (!uint.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public int ReadInt32(string section, string key, int Default, bool writeWhenNull = true)
    {
        int result;

        if (!int.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public ulong ReadUInt64(string section, string key, ulong Default, bool writeWhenNull = true)
    {
        ulong result;

        if (!ulong.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public long ReadInt64(string section, string key, long Default, bool writeWhenNull = true)
    {
        long result;

        if (!long.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public float ReadSingle(string section, string key, float Default, bool writeWhenNull = true)
    {
        float result;

        if (!float.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public double ReadDouble(string section, string key, double Default, bool writeWhenNull = true)
    {
        double result;

        if (!double.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public decimal ReadDecimal(string section, string key, decimal Default, bool writeWhenNull = true)
    {
        decimal result;

        if (!decimal.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public string ReadString(string section, string key, string Default, bool writeWhenNull = true)
    {
        string result = FindValue(section, key);

        if (string.IsNullOrEmpty(result))
        {
            result = Default;

            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public char ReadChar(string section, string key, char Default, bool writeWhenNull = true)
    {
        char result;

        if (!char.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public Point ReadPoint(string section, string key, Point Default)
    {
        string temp = FindValue(section, key);
        int tempX, tempY;
        if (temp == null || !int.TryParse(temp.Split(',')[0], out tempX))
        {
            Write(section, key, Default);
            return Default;
        }
        if (!int.TryParse(temp.Split(',')[1], out tempY))
        {
            Write(section, key, Default);
            return Default;
        }

        return new Point(tempX, tempY);
    }

    public Size ReadSize(string section, string key, Size Default)
    {
        string temp = FindValue(section, key);
        int tempX, tempY;
        if (!int.TryParse(temp.Split(',')[0], out tempX))
        {
            Write(section, key, Default);
            return Default;
        }
        if (!int.TryParse(temp.Split(',')[1], out tempY))
        {
            Write(section, key, Default);
            return Default;
        }

        return new Size(tempX, tempY);
    }

    public TimeSpan ReadTimeSpan(string section, string key, TimeSpan Default)
    {
        TimeSpan result;

        if (!TimeSpan.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            Write(section, key, Default);
        }


        return result;
    }

    public float ReadFloat(string section, string key, float Default)
    {
        float result;

        if (!float.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            Write(section, key, Default);
        }

        return result;
    }
    #endregion

    #region Write
    public void Write(string section, string key, bool value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, byte value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, sbyte value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, ushort value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, short value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, uint value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, int value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, ulong value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, long value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, float value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, double value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, decimal value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, string value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, char value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, Point value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value.X + "," + value.Y;
        Save();
    }

    public void Write(string section, string key, Size value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value.Width + "," + value.Height;
        Save();
    }

    public void Write(string section, string key, TimeSpan value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }
    #endregion
}