using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class RawTableDataTests
    {
        [Test]
        public void GetCell_ByIndex_ReturnsValue()
        {
            var table = MakeTable();

            Assert.AreEqual("1", table.GetCell(0, 0));
            Assert.AreEqual("Sword", table.GetCell(0, 1));
            Assert.AreEqual("100", table.GetCell(0, 2));
        }

        [Test]
        public void GetCell_ByColumnLetter_ReturnsValue()
        {
            var table = MakeTable();

            Assert.AreEqual("1", table.GetCell(0, "A"));
            Assert.AreEqual("Sword", table.GetCell(0, "B"));
            Assert.AreEqual("100", table.GetCell(0, "C"));
        }

        [Test]
        public void GetCell_OutOfBoundsRow_ReturnsEmpty()
        {
            var table = MakeTable();

            Assert.AreEqual(string.Empty, table.GetCell(99, 0));
            Assert.AreEqual(string.Empty, table.GetCell(-1, 0));
        }

        [Test]
        public void GetCell_OutOfBoundsColumn_ReturnsEmpty()
        {
            var table = MakeTable();

            Assert.AreEqual(string.Empty, table.GetCell(0, 99));
            Assert.AreEqual(string.Empty, table.GetCell(0, -1));
        }

        [Test]
        public void GetRow_ReturnsFullRow()
        {
            var table = MakeTable();

            var row = table.GetRow(1);

            Assert.AreEqual(3, row.Count);
            Assert.AreEqual("2", row[0]);
            Assert.AreEqual("Shield", row[1]);
        }

        [Test]
        public void GetRow_OutOfBounds_ReturnsEmpty()
        {
            var table = MakeTable();

            var row = table.GetRow(99);

            Assert.AreEqual(0, row.Count);
        }

        [Test]
        public void RowCount_ReturnsDataRowCount()
        {
            var table = MakeTable();

            Assert.AreEqual(2, table.RowCount);
        }

        [Test]
        public void ColumnCount_ReturnsHeaderCount()
        {
            var table = MakeTable();

            Assert.AreEqual(3, table.ColumnCount);
        }

        [Test]
        public void Headers_ReturnsHeaderRow()
        {
            var table = MakeTable();

            Assert.AreEqual("ID", table.Headers[0]);
            Assert.AreEqual("Name", table.Headers[1]);
            Assert.AreEqual("Value", table.Headers[2]);
        }

        [TestCase("A", 0)]
        [TestCase("B", 1)]
        [TestCase("Z", 25)]
        [TestCase("AA", 26)]
        [TestCase("AB", 27)]
        [TestCase("AZ", 51)]
        [TestCase("BA", 52)]
        [TestCase("a", 0)]
        [TestCase("b", 1)]
        public void ColumnLetterToIndex_ConvertsCorrectly(string letter, int expected)
        {
            Assert.AreEqual(expected, RawTableData.ColumnLetterToIndex(letter));
        }

        [TestCase(null, -1)]
        [TestCase("", -1)]
        [TestCase("1", -1)]
        public void ColumnLetterToIndex_InvalidInput_ReturnsNegative(string letter, int expected)
        {
            Assert.AreEqual(expected, RawTableData.ColumnLetterToIndex(letter));
        }

        [TestCase(0, "A")]
        [TestCase(1, "B")]
        [TestCase(25, "Z")]
        [TestCase(26, "AA")]
        [TestCase(27, "AB")]
        [TestCase(51, "AZ")]
        [TestCase(52, "BA")]
        public void IndexToColumnLetter_ConvertsCorrectly(int index, string expected)
        {
            Assert.AreEqual(expected, RawTableData.IndexToColumnLetter(index));
        }

        [Test]
        public void ColumnLetterToIndex_RoundTrips()
        {
            for (int i = 0; i < 100; i++)
            {
                string letter = RawTableData.IndexToColumnLetter(i);
                int back = RawTableData.ColumnLetterToIndex(letter);
                Assert.AreEqual(i, back, $"Round-trip failed for index {i} (letter: {letter})");
            }
        }

        [Test]
        public void GetCell_NullCellValue_ReturnsEmpty()
        {
            var rows = new[] { new string[] { "a", null, "c" } };
            var table = new RawTableData(rows, new[] { "A", "B", "C" });

            Assert.AreEqual(string.Empty, table.GetCell(0, 1));
        }

        [Test]
        public void GetCell_JaggedRows_HandlesGracefully()
        {
            var rows = new[]
            {
                new[] { "a", "b", "c" },
                new[] { "x" },
            };
            var table = new RawTableData(rows, new[] { "A", "B", "C" });

            Assert.AreEqual("x", table.GetCell(1, 0));
            Assert.AreEqual(string.Empty, table.GetCell(1, 1));
            Assert.AreEqual(string.Empty, table.GetCell(1, 2));
        }

        private static RawTableData MakeTable()
        {
            var headers = new[] { "ID", "Name", "Value" };
            var rows = new[]
            {
                new[] { "1", "Sword", "100" },
                new[] { "2", "Shield", "50" },
            };
            return new RawTableData(rows, headers);
        }
    }
}
