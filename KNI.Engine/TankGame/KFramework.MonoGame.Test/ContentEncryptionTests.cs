using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace KFramework.MonoGame.Test
{
    [TestClass]
    public class ContentEncryptionTests
    {
        // 1. 编解码可往返还原
        [TestMethod]
        public void Encode_Decode_RoundTrip()
        {
            string original = "hello world 你好世界";
            string encoded = ContentEncryption.Encode(original);
            string decoded = ContentEncryption.Decode(encoded);
            Assert.AreEqual(original, decoded, "round-trip should restore original");
        }

        // 2. 编码后不是明文（肉眼不可读）
        [TestMethod]
        public void Encode_IsNotPlaintext()
        {
            string original = "secret-config-value";
            string encoded = ContentEncryption.Encode(original);
            Assert.AreNotEqual(original, encoded, "encoded output must differ from plaintext");
            Assert.IsFalse(encoded.Contains("secret"), "encoded output should not leak plaintext");
        }

        // 3. 空字符串处理
        [TestMethod]
        public void Encode_EmptyString_RoundTrip()
        {
            string original = "";
            string encoded = ContentEncryption.Encode(original);
            string decoded = ContentEncryption.Decode(encoded);
            Assert.AreEqual(original, decoded, "empty string round-trip");
        }

        // 4. 长文本往返（含随机字符）
        [TestMethod]
        public void Encode_LongText_RoundTrip()
        {
            var sb = new System.Text.StringBuilder();
            var rnd = new Random(12345);
            for (int i = 0; i < 5000; i++)
                sb.Append((char)(' ' + rnd.Next(95))); // 可打印 ASCII
            string original = sb.ToString();

            string encoded = ContentEncryption.Encode(original);
            string decoded = ContentEncryption.Decode(encoded);

            Assert.AreEqual(original, decoded, "long text round-trip");
        }

        // 5. 多次编码结果稳定（确定性）
        [TestMethod]
        public void Encode_IsDeterministic()
        {
            string original = "deterministic-check";
            string a = ContentEncryption.Encode(original);
            string b = ContentEncryption.Encode(original);
            Assert.AreEqual(a, b, "same input should produce same output");
        }

        // 6. 编码结果可被 Base64 解析（输出确为合法 Base64 字符串）
        [TestMethod]
        public void Encode_OutputIsValidBase64()
        {
            string encoded = ContentEncryption.Encode("any content");
            // 若不是合法 Base64，下方会抛 FormatException
            byte[] bytes = Convert.FromBase64String(encoded);
            Assert.IsTrue(bytes.Length > 0, "decoded base64 should have content");
        }
    }
}
