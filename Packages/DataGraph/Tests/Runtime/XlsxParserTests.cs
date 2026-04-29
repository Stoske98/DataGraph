using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class XlsxParserTests
    {
        [Test]
        public void Parse_ByteArray_ReadsHeadersAndData()
        {
            var bytes = BuildMinimalXlsx(
                sheetName: "Sheet1",
                rows: new[]
                {
                    new[] { "Name", "Level" },
                    new[] { "Hero", "10" },
                    new[] { "Mage", "5" }
                });

            var result = XlsxParser.Parse(bytes);

            Assert.AreEqual(3, result.Length);
            CollectionAssert.AreEqual(new[] { "Name", "Level" }, result[0]);
            CollectionAssert.AreEqual(new[] { "Hero", "10" }, result[1]);
            CollectionAssert.AreEqual(new[] { "Mage", "5" }, result[2]);
        }

        [Test]
        public void Parse_Stream_ReadsSameAsBytes()
        {
            var bytes = BuildMinimalXlsx("Sheet1", new[]
            {
                new[] { "A", "B" },
                new[] { "1", "2" }
            });

            using var stream = new MemoryStream(bytes);
            var result = XlsxParser.Parse(stream);

            Assert.AreEqual(2, result.Length);
            CollectionAssert.AreEqual(new[] { "A", "B" }, result[0]);
            CollectionAssert.AreEqual(new[] { "1", "2" }, result[1]);
        }

        [Test]
        public void Parse_FilePath_ReadsFromDisk()
        {
            var bytes = BuildMinimalXlsx("Sheet1", new[]
            {
                new[] { "X" },
                new[] { "y" }
            });

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, bytes);
                var result = XlsxParser.Parse(tempFile);

                Assert.AreEqual(2, result.Length);
                CollectionAssert.AreEqual(new[] { "X" }, result[0]);
                CollectionAssert.AreEqual(new[] { "y" }, result[1]);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void Parse_UnknownSheet_Throws()
        {
            var bytes = BuildMinimalXlsx("Sheet1", new[] { new[] { "h" } });

            Assert.Throws<System.InvalidOperationException>(() =>
                XlsxParser.Parse(bytes, "NoSuchSheet"));
        }

        [Test]
        public void Parse_NullBytes_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                XlsxParser.Parse((byte[])null));
        }

        [Test]
        public void Parse_NullStream_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                XlsxParser.Parse((Stream)null));
        }

        [Test]
        public void GetSheetNames_Stream_ReturnsAllSheets()
        {
            var bytes = BuildMinimalXlsx("OnlySheet", new[] { new[] { "h" } });

            using var stream = new MemoryStream(bytes);
            var names = XlsxParser.GetSheetNames(stream);

            CollectionAssert.AreEqual(new[] { "OnlySheet" }, names);
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Builds a minimal valid .xlsx archive with one sheet.
        /// Uses inlineStr cells so we don't need a sharedStrings.xml part.
        /// </summary>
        private static byte[] BuildMinimalXlsx(string sheetName, string[][] rows)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"" +
                    " xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets>" +
                    $"<sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/>" +
                    "</sheets>" +
                    "</workbook>");

                AddEntry(archive, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\"" +
                    " Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\"" +
                    " Target=\"worksheets/sheet1.xml\"/>" +
                    "</Relationships>");

                var sheet = new StringBuilder();
                sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                sheet.Append("<sheetData>");
                for (int r = 0; r < rows.Length; r++)
                {
                    sheet.Append($"<row r=\"{r + 1}\">");
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        var cellRef = $"{ColumnLetter(c)}{r + 1}";
                        sheet.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{Escape(rows[r][c])}</t></is></c>");
                    }
                    sheet.Append("</row>");
                }
                sheet.Append("</sheetData></worksheet>");

                AddEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
            }

            return ms.ToArray();
        }

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        private static string ColumnLetter(int colIndex)
        {
            var sb = new StringBuilder();
            int n = colIndex;
            while (n >= 0)
            {
                sb.Insert(0, (char)('A' + n % 26));
                n = n / 26 - 1;
            }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
