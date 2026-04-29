using System;
using NUnit.Framework;
using DataGraph.Editor.Parsing;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    public class EnumParserTests
    {
        private enum Color { Red, Green, Blue }

        [Flags]
        private enum Style { None = 0, Bold = 1, Italic = 2, Underline = 4 }

        [Test]
        public void Parse_SingleValue_ReturnsEnum()
        {
            var result = EnumParser.Parse(typeof(Color), "Green");

            Assert.AreEqual(Color.Green, result);
        }

        [Test]
        public void Parse_CaseInsensitive_ReturnsEnum()
        {
            var result = EnumParser.Parse(typeof(Color), "GREEN");

            Assert.AreEqual(Color.Green, result);
        }

        [Test]
        public void Parse_FlagsWithPipeSeparator_CombinesValues()
        {
            var result = EnumParser.Parse(typeof(Style), "Bold|Italic");

            Assert.AreEqual(Style.Bold | Style.Italic, result);
        }

        [Test]
        public void Parse_FlagsWithSemicolonSeparator_CombinesValues()
        {
            var result = EnumParser.Parse(typeof(Style), "Bold;Underline");

            Assert.AreEqual(Style.Bold | Style.Underline, result);
        }

        [Test]
        public void Parse_FlagsWithCommaSeparator_CombinesValues()
        {
            var result = EnumParser.Parse(typeof(Style), "Bold, Italic, Underline");

            Assert.AreEqual(Style.Bold | Style.Italic | Style.Underline, result);
        }

        [Test]
        public void Parse_EmptyString_ReturnsDefault()
        {
            var result = EnumParser.Parse(typeof(Color), "");

            Assert.AreEqual(Color.Red, result);
        }

        [Test]
        public void Parse_NullString_ReturnsDefault()
        {
            var result = EnumParser.Parse(typeof(Style), null);

            Assert.AreEqual(Style.None, result);
        }

        [Test]
        public void Parse_TrimsWhitespaceAroundParts()
        {
            var result = EnumParser.Parse(typeof(Style), "  Bold  |   Italic  ");

            Assert.AreEqual(Style.Bold | Style.Italic, result);
        }
    }
}
