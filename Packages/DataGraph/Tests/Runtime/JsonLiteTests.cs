using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class JsonLiteTests
    {
        [Test]
        public void ParseValuesArray_TwoRows_ReturnsBothRows()
        {
            var json = "{\"values\":[[\"h1\",\"h2\"],[\"v1\",\"v2\"]]}";

            var result = JsonLite.ParseValuesArray(json);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(new[] { "h1", "h2" }, result[0]);
            CollectionAssert.AreEqual(new[] { "v1", "v2" }, result[1]);
        }

        [Test]
        public void ParseValuesArray_CapitalizedValuesKey_ReturnsRows()
        {
            var json = "{\"Values\":[[\"a\",\"b\"]]}";

            var result = JsonLite.ParseValuesArray(json);

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, result[0]);
        }

        [Test]
        public void ParseValuesArray_NoValuesKey_ReturnsEmpty()
        {
            var json = "{\"foo\":1}";

            var result = JsonLite.ParseValuesArray(json);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseValuesArray_WithExplicitPos_StartsFromThere()
        {
            var json = "[[\"x\",\"y\"]]";

            var result = JsonLite.ParseValuesArray(json, 0);

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEqual(new[] { "x", "y" }, result[0]);
        }

        [Test]
        public void ParseValuesArray_MixedTypes_KeepsUnquotedAsStrings()
        {
            var json = "{\"values\":[[\"name\",42,true,null]]}";

            var result = JsonLite.ParseValuesArray(json);

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEqual(new[] { "name", "42", "true", "" }, result[0]);
        }

        [Test]
        public void ParseStringArray_BasicStrings_ParsesCorrectly()
        {
            var json = "[\"a\",\"b\",\"c\"]";

            var result = JsonLite.ParseStringArray(json, 0, out int endPos);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
            Assert.AreEqual(json.Length, endPos);
        }

        [Test]
        public void ParseStringArray_EmptyArray_ReturnsEmpty()
        {
            var json = "[]";

            var result = JsonLite.ParseStringArray(json, 0, out int endPos);

            Assert.AreEqual(0, result.Length);
            Assert.AreEqual(2, endPos);
        }

        [Test]
        public void ParseJsonString_HandlesEscapeSequences()
        {
            var json = "\"line1\\nline2\\ttab\\\"quote\\\\back\"";

            var result = JsonLite.ParseJsonString(json, 0, out int endPos);

            Assert.AreEqual("line1\nline2\ttab\"quote\\back", result);
            Assert.AreEqual(json.Length, endPos);
        }

        [Test]
        public void ParseJsonString_EmptyString_ReturnsEmpty()
        {
            var json = "\"\"";

            var result = JsonLite.ParseJsonString(json, 0, out int endPos);

            Assert.AreEqual("", result);
            Assert.AreEqual(2, endPos);
        }

        [Test]
        public void ParseUnquotedValue_NumberFollowedByComma_ReadsUntilComma()
        {
            var json = "42,next";

            var result = JsonLite.ParseUnquotedValue(json, 0, out int endPos);

            Assert.AreEqual("42", result);
            Assert.AreEqual(2, endPos);
        }

        [Test]
        public void ParseUnquotedValue_NullLiteral_ReturnsEmptyString()
        {
            var json = "null]";

            var result = JsonLite.ParseUnquotedValue(json, 0, out int endPos);

            Assert.AreEqual("", result);
            Assert.AreEqual(4, endPos);
        }

        [Test]
        public void ExtractJsonStringField_FieldPresent_ReturnsValue()
        {
            var json = "{\"id\":\"abc123\",\"name\":\"file.xlsx\"}";

            Assert.AreEqual("abc123", JsonLite.ExtractJsonStringField(json, "id"));
            Assert.AreEqual("file.xlsx", JsonLite.ExtractJsonStringField(json, "name"));
        }

        [Test]
        public void ExtractJsonStringField_FieldMissing_ReturnsNull()
        {
            var json = "{\"id\":\"abc123\"}";

            Assert.IsNull(JsonLite.ExtractJsonStringField(json, "missing"));
        }

        [Test]
        public void ExtractJsonStringField_FieldWithEscapedValue_UnescapesIt()
        {
            var json = "{\"path\":\"C:\\\\Users\\\\file.txt\"}";

            var result = JsonLite.ExtractJsonStringField(json, "path");

            Assert.AreEqual("C:\\Users\\file.txt", result);
        }
    }
}
