using System;
using System.Diagnostics;
using System.Text;

namespace KFramework.MonoGame
{
    public static partial class PrintTool
    {
        private static readonly StringBuilder mStringBuilder = new StringBuilder();
        internal const string ConcatStr = "  ";

        // 把参数拼接进共享 mStringBuilder（前缀时间戳），不直接 ToString，便于调用方用 GetChunks 直写避免分配
        private static void Build(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
            mStringBuilder.Clear();
            mStringBuilder.Append('[');
            Span<char> ts = stackalloc char[32];
            if (DateTime.Now.TryFormat(ts, out int w, "HH:mm:ss.fff".AsSpan(), null))
                mStringBuilder.Append(ts.Slice(0, w));
            mStringBuilder.Append(']');
            if (data1 != null) { mStringBuilder.Append(data1); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data2 != null) { mStringBuilder.Append(data2); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data3 != null) { mStringBuilder.Append(data3); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data4 != null) { mStringBuilder.Append(data4); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data5 != null) { mStringBuilder.Append(data5); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data6 != null) { mStringBuilder.Append(data6); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data7 != null) { mStringBuilder.Append(data7); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data8 != null) { mStringBuilder.Append(data8); mStringBuilder.Append(ConcatStr.AsSpan()); }
            if (data9 != null) { mStringBuilder.Append(data9); mStringBuilder.Append(ConcatStr.AsSpan()); }
        }

        private static string GetStr(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
            Build(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            return mStringBuilder.ToString();
        }

        public static void LogWithColor(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            Build(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Console.Out.Write("<color=yellow>".AsSpan());
            foreach (var chunk in mStringBuilder.GetChunks()) Console.Out.Write(chunk.Span);
            Console.Out.Write("</color>\n".AsSpan());
#endif
        }

        public static void LogFormatWithColor(string formatStr, object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            // string.Format 仍会产生格式化字符串（format API 固有），但避免外层的 "<color>...</color>" 拼接分配
            string content = string.Format(formatStr, data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Console.Out.Write("<color=yellow>".AsSpan());
            Console.Out.Write(content.AsSpan());
            Console.Out.Write("</color>\n".AsSpan());
#endif
        }

        public static void Log(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            Build(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            foreach (var chunk in mStringBuilder.GetChunks()) Console.Out.Write(chunk.Span);
            Console.Out.Write('\n');
#endif
        }

        public static void LogError(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            Build(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            foreach (var chunk in mStringBuilder.GetChunks()) Console.Error.Write(chunk.Span);
            Console.Error.Write('\n');
#endif
        }

        public static void Assert(bool isTrue, object data1 = null, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = GetStr(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.Assert(isTrue, content);
#endif
        }

        public static bool Assert(bool isTrue)
        {
#if DEBUG
            Debug.Assert(isTrue);
            return !isTrue;
#endif
        }


    }
}
