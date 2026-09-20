using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器 localStorage 键值存储绑定（module: "localstorage"，见 KFramework.TSEngine/src/storage_local.ts）。
    /// 暴露四个核心方法：SetString / GetString / HasKey / RemoveKey（与 TS 端一一对应）。
    /// <para>GetStringAsync 缺失返回空字符串（空字符串即哨兵值，setString(key, '') 视为删除）；
    /// 若需精确区分「键不存在」与「值为空字符串」，用 <see cref="HasKeyAsync"/>（底层 getItem 对缺失返回 null）。</para>
    /// 业务不要直接调用，请用上层封装。
    /// </summary>
    public static partial class JSBind_LocalStorage
    {
        [JSImport("setItem", "localstorage")]
        public static partial void SetString(string key, string value);

        [JSImport("getItem", "localstorage")]
        public static partial string GetString(string key);

        [JSImport("removeItem", "localstorage")]
        public static partial void RemoveKey(string key);
        
        [JSImport("clear", "localstorage")]
        public static partial void Clear();
    }
}
