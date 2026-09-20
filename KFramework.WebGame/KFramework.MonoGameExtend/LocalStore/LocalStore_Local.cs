using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    public static class LocalStore_Local
    {
        public static void SetString(string key, string value)
        {
            JSBind_LocalStorage.SetString(key, value);
        }

        /// <summary>写入字符串值（账号/密码/设置等）。</summary>
        public static async Task SetStringAsync(string key, string value)
        {
            JSBind_LocalStorage.SetString(key, value);
        }

        public static string GetString(string key)
        {
            return JSBind_LocalStorage.GetString(key);
        }

        /// <summary>读取字符串值；缺失返回空字符串（空字符串与缺失不可区分，需区分请用 <see cref="HasKeyAsync"/>）。</summary>
        public static async Task<string> GetStringAsync(string key)
        {
            return JSBind_LocalStorage.GetString(key);
        }

        public static bool HasKey(string key)
        {
            return JSBind_LocalStorage.GetString(key) != null;
        }

        public static async Task<bool> HasKeyAsync(string key)
        { 
            return JSBind_LocalStorage.GetString(key) != null;
        }

        public static void RemoveKey(string key)
        {
            JSBind_LocalStorage.RemoveKey(key);
        }

        /// <summary>删除键。</summary>
        public static async Task RemoveKeyAsync(string key)
        { 
            JSBind_LocalStorage.RemoveKey(key);
        }

        public static void Clear()
        {
            JSBind_LocalStorage.Clear();
        }

        public static async Task ClearAsync()
        {
            JSBind_LocalStorage.Clear();
        }

    }
}
