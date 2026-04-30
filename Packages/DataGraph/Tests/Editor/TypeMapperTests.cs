using System;
using NUnit.Framework;
using DataGraph.Editor.CodeGen;
using DataGraph.Editor.Domain;

namespace DataGraph.Tests.Editor
{
    /// <summary>
    /// TestCase parameters are passed as strings and parsed inside each test,
    /// not typed FieldValueType / KeyType, because those enums are internal
    /// and cannot appear in a public method signature (CS0051) — and NUnit
    /// only discovers public test methods. See feedback_internal_testcase.md.
    /// </summary>
    [TestFixture]
    public class TypeMapperTests
    {
        private static FieldValueType Vt(string name) =>
            (FieldValueType)Enum.Parse(typeof(FieldValueType), name);

        private static KeyType Kt(string name) =>
            (KeyType)Enum.Parse(typeof(KeyType), name);

        // ==================== GetSOType ====================

        [TestCase("String", "string")]
        [TestCase("Int", "int")]
        [TestCase("Float", "float")]
        [TestCase("Double", "double")]
        [TestCase("Bool", "bool")]
        [TestCase("Vector2", "Vector2")]
        [TestCase("Vector3", "Vector3")]
        [TestCase("Color", "Color")]
        public void GetSOType_PrimitivesAndStructs_ReturnsCSharpName(string vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetSOType(Vt(vt)));
        }

        [Test]
        public void GetSOType_EnumWithType_ReturnsEnumName()
        {
            Assert.AreEqual("DayOfWeek",
                TypeMapper.GetSOType(FieldValueType.Enum, typeof(DayOfWeek)));
        }

        [Test]
        public void GetSOType_EnumWithoutType_FallsBackToInt()
        {
            Assert.AreEqual("int", TypeMapper.GetSOType(FieldValueType.Enum));
        }

        [Test]
        public void GetSOType_AllEnumValuesCovered()
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

        [TestCase("String", "BlobString")]
        [TestCase("Int", "int")]
        [TestCase("Float", "float")]
        [TestCase("Double", "double")]
        [TestCase("Bool", "bool")]
        [TestCase("Vector2", "UnityEngine.Vector2")]
        [TestCase("Vector3", "UnityEngine.Vector3")]
        [TestCase("Color", "UnityEngine.Color")]
        [TestCase("Enum", "int")]
        public void GetBlobType_AllValues_ReturnsExpected(string vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetBlobType(Vt(vt)));
        }

        [Test]
        public void GetBlobType_AllEnumValuesCovered()
        {
            foreach (FieldValueType vt in Enum.GetValues(typeof(FieldValueType)))
            {
                Assert.DoesNotThrow(() => TypeMapper.GetBlobType(vt),
                    $"GetBlobType missing case for {vt}");
            }
        }

        // ==================== GetQuantumSimType ====================

        [TestCase("Int", "int")]
        [TestCase("Float", "FP")]
        [TestCase("Double", "FP")]
        [TestCase("Bool", "bool")]
        [TestCase("Vector2", "FPVector2")]
        [TestCase("Vector3", "FPVector3")]
        public void GetQuantumSimType_SimSafe_ReturnsFPType(string vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetQuantumSimType(Vt(vt)));
        }

        [TestCase("String")]
        [TestCase("Color")]
        [TestCase("Enum")]
        public void GetQuantumSimType_NonSimSafe_Throws(string vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetQuantumSimType(Vt(vt)));
        }

        // ==================== GetQuantumViewType ====================

        [TestCase("String", "string")]
        [TestCase("Color", "UnityEngine.Color")]
        public void GetQuantumViewType_ViewOnly_ReturnsType(string vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetQuantumViewType(Vt(vt)));
        }

        [TestCase("Int")]
        [TestCase("Float")]
        [TestCase("Double")]
        [TestCase("Bool")]
        [TestCase("Vector2")]
        [TestCase("Vector3")]
        [TestCase("Enum")]
        public void GetQuantumViewType_SimSafeOrUnknown_Throws(string vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetQuantumViewType(Vt(vt)));
        }

        // ==================== GetJsonSchemaType ====================

        [TestCase("String", "string")]
        [TestCase("Int", "integer")]
        [TestCase("Float", "number")]
        [TestCase("Double", "number")]
        [TestCase("Bool", "boolean")]
        [TestCase("Enum", "string")]
        public void GetJsonSchemaType_Primitives_ReturnsSchemaName(string vt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetJsonSchemaType(Vt(vt)));
        }

        [TestCase("Vector2")]
        [TestCase("Vector3")]
        [TestCase("Color")]
        public void GetJsonSchemaType_StructuredTypes_Throw(string vt)
        {
            Assert.Throws<InvalidOperationException>(() => TypeMapper.GetJsonSchemaType(Vt(vt)));
        }

        // ==================== GetKeyTypeName ====================

        [TestCase("Int", "int")]
        [TestCase("String", "string")]
        public void GetKeyTypeName_Known_ReturnsCSharpName(string kt, string expected)
        {
            Assert.AreEqual(expected, TypeMapper.GetKeyTypeName(Kt(kt)));
        }
    }
}
