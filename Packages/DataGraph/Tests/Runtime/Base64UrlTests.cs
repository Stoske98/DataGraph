using System.Text;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class Base64UrlTests
    {
        [Test]
        public void Encode_StripsPaddingAndUrlSafeChars()
        {
            // "??>" produces "+", "/", and trailing '='
            var bytes = new byte[] { 0xFB, 0xFF, 0x3E };

            var result = Base64Url.Encode(bytes);

            // Standard base64 of these bytes is "+/8+" — Base64Url replaces '+' with '-' and '/' with '_'
            Assert.AreEqual("-_8-", result);
            StringAssert.DoesNotContain("=", result);
            StringAssert.DoesNotContain("+", result);
            StringAssert.DoesNotContain("/", result);
        }

        [Test]
        public void Encode_AsciiText_RoundtripsViaStandardBase64Replacement()
        {
            var bytes = Encoding.ASCII.GetBytes("hello");

            var result = Base64Url.Encode(bytes);

            // "aGVsbG8=" -> trim '=' -> "aGVsbG8"
            Assert.AreEqual("aGVsbG8", result);
        }

        [Test]
        public void Encode_EmptyArray_ReturnsEmptyString()
        {
            Assert.AreEqual("", Base64Url.Encode(new byte[0]));
        }
    }
}
