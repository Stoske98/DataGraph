using System;
using NUnit.Framework;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.Domain;

namespace DataGraph.Tests.Editor
{
    [TestFixture]
    internal class TypeMapperTests
    {
        // ==================== GetSOType ====================

        [TestCase(FieldValueType.String, "string")]
        [TestCase(FieldValueType.Int, "int")]
        [TestCase(FieldValueType.Float, "float")]
        [TestCase(FieldValueType.Double, "double")]
        [TestCase(FieldValueType.Bool, "bool")]
        [TestCase(FieldValueType.Vector2, "Vector2")]
        [TestCase(FieldValueType.Vector3, "Vector3")]
        [TestCase(FieldValueType.Color, "Color")]
        internal void GetSOType_PrimitivesAndStructs_ReturnsCSharpName(FieldValueType vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetSOType(vt));
        }

        [Test]
        internal void GetSOType_EnumWithType_ReturnsEnumName()
        {
            Assert.AreEqual("DayOfWeek",
                TypeMapper.GetSOType(FieldValueType.Enum, typeof(DayOfWeek)));
        }

        [Test]
        internal void GetSOType_EnumWithoutType_FallsBackToInt()
        {
            Assert.AreEqual("int", TypeMapper.GetSOType(FieldValueType.Enum));
        }

        [Test]
        internal void GetSOType_AllEnumValuesCovered()
        {
            // Exhaustiveness guard: any new FieldValueType must be added explicitly.
            foreach (FieldValueType vt in Enum.GetValues(typeof(FieldValueType)))
            {
                Assert.DoesNotThrow(() =>
                    TypeMapper.GetSOType(vt, typeof(DayOfWeek)),
                    $"GetSOType missing case for {vt}");
            }
        }

        // ==================== GetBlobType ====================

        [TestCase(FieldValueType.String, "BlobString")]
        [TestCase(FieldValueType.Int, "int")]
        [TestCase(FieldValueType.Float, "float")]
        [TestCase(FieldValueType.Double, "double")]
        [TestCase(FieldValueType.Bool, "bool")]
        [TestCase(FieldValueType.Vector2, "UnityEngine.Vector2")]
        [TestCase(FieldValueType.Vector3, "UnityEngine.Vector3")]
        [TestCase(FieldValueType.Color, "UnityEngine.Color")]
        [TestCase(FieldValueType.Enum, "int")]
        internal void GetBlobType_AllValues_ReturnsExpected(FieldValueType vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetBlobType(vt));
        }

        [Test]
        internal void GetBlobType_AllEnumValuesCovered()
        {
            foreach (FieldValueType vt in Enum.GetValues(typeof(FieldValueType)))
            {
                Assert.DoesNotThrow(() => TypeMapper.GetBlobType(vt),
                    $"GetBlobType missing case for {vt}");
            }
        }

        // ==================== GetQuantumSimType ====================

        [TestCase(FieldValueType.Int, "int")]
        [TestCase(FieldValueType.Float, "FP")]
        [TestCase(FieldValueType.Double, "FP")]
        [TestCase(FieldValueType.Bool, "bool")]
        [TestCase(FieldValueType.Vector2, "FPVector2")]
        [TestCase(FieldValueType.Vector3, "FPVector3")]
        internal void GetQuantumSimType_SimSafe_ReturnsFPType(FieldValueType vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetQuantumSimType(vt));
        }

        [TestCase(FieldValueType.String)]
        [TestCase(FieldValueType.Color)]
        [TestCase(FieldValueType.Enum)]
        internal void GetQuantumSimType_NonSimSafe_Throws(FieldValueType vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetQuantumSimType(vt));
        }

        // ==================== GetQuantumViewType ====================

        [TestCase(FieldValueType.String, "string")]
        [TestCase(FieldValueType.Color, "UnityEngine.Color")]
        internal void GetQuantumViewType_ViewOnly_ReturnsType(FieldValueType vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetQuantumViewType(vt));
        }

        [TestCase(FieldValueType.Int)]
        [TestCase(FieldValueType.Float)]
        [TestCase(FieldValueType.Double)]
        [TestCase(FieldValueType.Bool)]
        [TestCase(FieldValueType.Vector2)]
        [TestCase(FieldValueType.Vector3)]
        [TestCase(FieldValueType.Enum)]
        internal void GetQuantumViewType_SimSafeOrUnknown_Throws(FieldValueType vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetQuantumViewType(vt));
        }

        // ==================== GetJsonSchemaType ====================

        [TestCase(FieldValueType.String, "string")]
        [TestCase(FieldValueType.Int, "integer")]
        [TestCase(FieldValueType.Float, "number")]
        [TestCase(FieldValueType.Double, "number")]
        [TestCase(FieldValueType.Bool, "boolean")]
        [TestCase(FieldValueType.Enum, "string")]
        internal void GetJsonSchemaType_Primitives_ReturnsSchemaName(FieldValueType vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetJsonSchemaType(vt));
        }

        [TestCase(FieldValueType.Vector2)]
        [TestCase(FieldValueType.Vector3)]
        [TestCase(FieldValueType.Color)]
        internal void GetJsonSchemaType_StructuredTypes_Throw(FieldValueType vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetJsonSchemaType(vt));
        }

        // ==================== GetKeyTypeName ====================

        [TestCase(KeyType.Int, "int")]
        [TestCase(KeyType.String, "string")]
        internal void GetKeyTypeName_Known_ReturnsCSharpName(KeyType kt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetKeyTypeName(kt));
        }
    }
}
