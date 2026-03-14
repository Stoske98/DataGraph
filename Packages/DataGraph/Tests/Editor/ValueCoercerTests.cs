using NUnit.Framework;
using DataGraph.Editor.Domain;
using DataGraph.Editor.Parsing;
using UnityEngine;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class ValueCoercerTests
    {
        [Test]
        public void String_ReturnsAsIs()
        {
            var result = Coerce("hello", FieldValueType.String);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("hello", result.Value);
        }

        [Test]
        public void String_Empty_ReturnsEmpty()
        {
            var result = Coerce("", FieldValueType.String);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.Value);
        }

        [Test]
        public void Int_ValidNumber_Parses()
        {
            var result = Coerce("42", FieldValueType.Int);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(42, result.Value);
        }

        [Test]
        public void Int_NegativeNumber_Parses()
        {
            var result = Coerce("-7", FieldValueType.Int);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(-7, result.Value);
        }

        [Test]
        public void Int_FloatString_TruncatesToInt()
        {
            var result = Coerce("3.7", FieldValueType.Int);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Value);
        }

        [Test]
        public void Int_InvalidString_Fails()
        {
            var result = Coerce("abc", FieldValueType.Int);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void Int_Empty_ReturnsDefault()
        {
            var result = Coerce("", FieldValueType.Int);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Value);
        }

        [Test]
        public void Float_ValidNumber_Parses()
        {
            var result = Coerce("3.14", FieldValueType.Float);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3.14f, (float)result.Value, 0.001f);
        }

        [Test]
        public void Float_IntegerString_Parses()
        {
            var result = Coerce("10", FieldValueType.Float);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(10f, result.Value);
        }

        [Test]
        public void Float_Invalid_Fails()
        {
            var result = Coerce("xyz", FieldValueType.Float);

            Assert.IsTrue(result.IsFailure);
        }

        [TestCase("true", true)]
        [TestCase("True", true)]
        [TestCase("TRUE", true)]
        [TestCase("1", true)]
        [TestCase("yes", true)]
        [TestCase("false", false)]
        [TestCase("False", false)]
        [TestCase("0", false)]
        [TestCase("no", false)]
        public void Bool_ValidValues_Parse(string input, bool expected)
        {
            var result = Coerce(input, FieldValueType.Bool);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expected, result.Value);
        }

        [Test]
        public void Bool_Invalid_Fails()
        {
            var result = Coerce("maybe", FieldValueType.Bool);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void Vector2_CommaSeparated_Parses()
        {
            var field = MakeField("pos", "A", FieldValueType.Vector2, separator: ",");
            var result = ValueCoercer.Coerce("1.5,2.5", FieldValueType.Vector2, field);

            Assert.IsTrue(result.IsSuccess);
            var vec = (Vector2)result.Value;
            Assert.AreEqual(1.5f, vec.x, 0.001f);
            Assert.AreEqual(2.5f, vec.y, 0.001f);
        }

        [Test]
        public void Vector2_SemicolonSeparated_Parses()
        {
            var field = MakeField("pos", "A", FieldValueType.Vector2, separator: ";");
            var result = ValueCoercer.Coerce("10;20", FieldValueType.Vector2, field);

            Assert.IsTrue(result.IsSuccess);
            var vec = (Vector2)result.Value;
            Assert.AreEqual(10f, vec.x, 0.001f);
            Assert.AreEqual(20f, vec.y, 0.001f);
        }

        [Test]
        public void Vector2_WrongComponentCount_Fails()
        {
            var field = MakeField("pos", "A", FieldValueType.Vector2, separator: ",");
            var result = ValueCoercer.Coerce("1,2,3", FieldValueType.Vector2, field);

            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void Vector3_Parses()
        {
            var field = MakeField("pos", "A", FieldValueType.Vector3, separator: ",");
            var result = ValueCoercer.Coerce("1,2,3", FieldValueType.Vector3, field);

            Assert.IsTrue(result.IsSuccess);
            var vec = (Vector3)result.Value;
            Assert.AreEqual(1f, vec.x, 0.001f);
            Assert.AreEqual(2f, vec.y, 0.001f);
            Assert.AreEqual(3f, vec.z, 0.001f);
        }

        [Test]
        public void Color_Hex_Parses()
        {
            var field = MakeField("tint", "A", FieldValueType.Color, format: "hex");
            var result = ValueCoercer.Coerce("#FF0000", FieldValueType.Color, field);

            Assert.IsTrue(result.IsSuccess);
            var color = (Color)result.Value;
            Assert.AreEqual(1f, color.r, 0.01f);
            Assert.AreEqual(0f, color.g, 0.01f);
            Assert.AreEqual(0f, color.b, 0.01f);
        }

        [Test]
        public void Color_HexWithoutHash_Parses()
        {
            var field = MakeField("tint", "A", FieldValueType.Color, format: "hex");
            var result = ValueCoercer.Coerce("00FF00", FieldValueType.Color, field);

            Assert.IsTrue(result.IsSuccess);
            var color = (Color)result.Value;
            Assert.AreEqual(0f, color.r, 0.01f);
            Assert.AreEqual(1f, color.g, 0.01f);
        }

        [Test]
        public void Color_Rgba_Parses()
        {
            var field = MakeField("tint", "A", FieldValueType.Color, format: "rgba");
            var result = ValueCoercer.Coerce("0.5,0.5,0.5,1", FieldValueType.Color, field);

            Assert.IsTrue(result.IsSuccess);
            var color = (Color)result.Value;
            Assert.AreEqual(0.5f, color.r, 0.01f);
            Assert.AreEqual(1f, color.a, 0.01f);
        }

        [Test]
        public void Enum_ByName_Parses()
        {
            var field = MakeField("day", "A", FieldValueType.Enum, enumType: typeof(System.DayOfWeek));
            var result = ValueCoercer.Coerce("Monday", FieldValueType.Enum, field);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(System.DayOfWeek.Monday, result.Value);
        }

        [Test]
        public void Enum_ByNameCaseInsensitive_Parses()
        {
            var field = MakeField("day", "A", FieldValueType.Enum, enumType: typeof(System.DayOfWeek));
            var result = ValueCoercer.Coerce("monday", FieldValueType.Enum, field);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(System.DayOfWeek.Monday, result.Value);
        }

        [Test]
        public void Enum_ByIntValue_Parses()
        {
            var field = MakeField("day", "A", FieldValueType.Enum, enumType: typeof(System.DayOfWeek));
            var result = ValueCoercer.Coerce("3", FieldValueType.Enum, field);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(System.DayOfWeek.Wednesday, result.Value);
        }

        [Test]
        public void Enum_Invalid_Fails()
        {
            var field = MakeField("day", "A", FieldValueType.Enum, enumType: typeof(System.DayOfWeek));
            var result = ValueCoercer.Coerce("NotADay", FieldValueType.Enum, field);

            Assert.IsTrue(result.IsFailure);
        }

        private static Runtime.Result<object> Coerce(string raw, FieldValueType type)
        {
            var field = MakeField("test", "A", type);
            return ValueCoercer.Coerce(raw, type, field);
        }

        private static ParseableCustomField MakeField(
            string name, string column, FieldValueType type,
            string separator = null, string format = null,
            System.Type enumType = null)
        {
            return new ParseableCustomField(name, column, type, separator, format, enumType);
        }
    }
}
