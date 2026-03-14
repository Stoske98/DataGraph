using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class ValidationReportTests
    {
        [Test]
        public void Empty_IsValid()
        {
            Assert.IsTrue(ValidationReport.Empty.IsValid);
            Assert.IsFalse(ValidationReport.Empty.HasErrors);
            Assert.IsFalse(ValidationReport.Empty.HasWarnings);
            Assert.AreEqual(0, ValidationReport.Empty.Entries.Count);
        }

        [Test]
        public void WithErrors_IsNotValid()
        {
            var report = MakeReport(ValidationSeverity.Error, "something broke");

            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void WithWarningsOnly_IsValid()
        {
            var report = MakeReport(ValidationSeverity.Warning, "heads up");

            Assert.IsTrue(report.IsValid);
            Assert.IsTrue(report.HasWarnings);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void WithInfoOnly_IsValid()
        {
            var report = MakeReport(ValidationSeverity.Info, "fyi");

            Assert.IsTrue(report.IsValid);
            Assert.IsFalse(report.HasWarnings);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void GetBySeverity_FiltersCorrectly()
        {
            var entries = new List<ValidationEntry>
            {
                new(ValidationSeverity.Info, "info msg"),
                new(ValidationSeverity.Warning, "warn msg"),
                new(ValidationSeverity.Error, "err msg"),
                new(ValidationSeverity.Warning, "warn msg 2"),
            };
            var report = new ValidationReport(entries);

            var warnings = report.GetBySeverity(ValidationSeverity.Warning).ToList();

            Assert.AreEqual(2, warnings.Count);
            Assert.IsTrue(warnings.All(w => w.Severity == ValidationSeverity.Warning));
        }

        [Test]
        public void GetByNode_FiltersCorrectly()
        {
            var entries = new List<ValidationEntry>
            {
                new(ValidationSeverity.Error, "err on node1", sourceNodeId: "node1"),
                new(ValidationSeverity.Error, "err on node2", sourceNodeId: "node2"),
                new(ValidationSeverity.Warning, "warn on node1", sourceNodeId: "node1"),
            };
            var report = new ValidationReport(entries);

            var node1Entries = report.GetByNode("node1").ToList();

            Assert.AreEqual(2, node1Entries.Count);
            Assert.IsTrue(node1Entries.All(e => e.SourceNodeId == "node1"));
        }

        [Test]
        public void Merge_CombinesBothReports()
        {
            var report1 = new ValidationReport(new List<ValidationEntry>
            {
                new(ValidationSeverity.Error, "err1"),
            });
            var report2 = new ValidationReport(new List<ValidationEntry>
            {
                new(ValidationSeverity.Warning, "warn1"),
                new(ValidationSeverity.Info, "info1"),
            });

            var merged = report1.Merge(report2);

            Assert.AreEqual(3, merged.Entries.Count);
            Assert.IsTrue(merged.HasErrors);
            Assert.IsTrue(merged.HasWarnings);
        }

        [Test]
        public void ValidationEntry_WithCellReference_PreservesLocation()
        {
            var cell = new CellReference(5, "C");
            var entry = new ValidationEntry(
                ValidationSeverity.Error,
                "type mismatch",
                sourceNodeId: "node3",
                sourceCell: cell);

            Assert.AreEqual(5, entry.SourceCell.Value.Row);
            Assert.AreEqual("C", entry.SourceCell.Value.Column);
            Assert.AreEqual("node3", entry.SourceNodeId);
        }

        [Test]
        public void ValidationEntry_ToString_FormatsCorrectly()
        {
            var entry = new ValidationEntry(ValidationSeverity.Warning, "check this");

            Assert.AreEqual("[Warning] check this", entry.ToString());
        }

        private static ValidationReport MakeReport(ValidationSeverity severity, string message)
        {
            return new ValidationReport(new List<ValidationEntry>
            {
                new(severity, message)
            });
        }
    }
}
