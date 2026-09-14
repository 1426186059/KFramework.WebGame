using System.Diagnostics;
using System.Text;

namespace KFramework.MonoGame
{
    public static class PrintTool
    {
        private static readonly StringBuilder mStringBuilder = new StringBuilder();
        private const string ConcatStr = "___";

        private static string GetStr(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
            mStringBuilder.Clear();
            if (data1 != null)
            {
                mStringBuilder.Append(data1);
                mStringBuilder.Append(ConcatStr);
            }
            if (data2 != null)
            {
                mStringBuilder.Append(data2);
                mStringBuilder.Append(ConcatStr);
            }
            if (data3 != null)
            {
                mStringBuilder.Append(data3);
                mStringBuilder.Append(ConcatStr);
            }
            if (data4 != null)
            {
                mStringBuilder.Append(data4);
                mStringBuilder.Append(ConcatStr);
            }
            if (data5 != null)
            {
                mStringBuilder.Append(data5);
                mStringBuilder.Append(ConcatStr);
            }
            if (data6 != null)
            {
                mStringBuilder.Append(data6);
                mStringBuilder.Append(ConcatStr);
            }
            if (data7 != null)
            {
                mStringBuilder.Append(data7);
                mStringBuilder.Append(ConcatStr);
            }
            if (data8 != null)
            {
                mStringBuilder.Append(data8);
                mStringBuilder.Append(ConcatStr);
            }
            if (data9 != null)
            {
                mStringBuilder.Append(data9);
                mStringBuilder.Append(ConcatStr);
            }
            return mStringBuilder.ToString();
        }

        public static void LogWithColor(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = GetStr(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine($"<color=yellow>{content}</color>");
#endif
        }

        public static void LogFormatWithColor(string formatStr, object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = string.Format(formatStr, data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine($"<color=yellow>{content}</color>");
#endif
        }

        public static void LogJsonObj(object data)
        {
#if DEBUG
            Debug.WriteLine(JsonTool.ToJson(data));
#endif
        }

        public static void Log(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = GetStr(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine(content);
#endif
        }

        public static void LogFormat(string formatStr, object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = string.Format(formatStr, data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine(content);
#endif
        }

        public static void LogError(object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = GetStr(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine(content);
#endif
        }

        public static void LogErrorFormat(string formatStr, object data1, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = string.Format(formatStr, data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.WriteLine(content);
#endif
        }

        public static void Assert(bool isTrue, object data1 = null, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = GetStr(data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.Assert(isTrue, content);
#endif
        }

        public static void AssertFormat(bool isTrue, string formatStr, object data1 = null, object data2 = null, object data3 = null, object data4 = null, object data5 = null, object data6 = null, object data7 = null, object data8 = null, object data9 = null)
        {
#if DEBUG
            string content = string.Format(formatStr, data1, data2, data3, data4, data5, data6, data7, data8, data9);
            Debug.Assert(isTrue, content);
#endif
        }

    }
}
