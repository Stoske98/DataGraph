using System;
using NUnit.Framework;
using DataGraph.Runtime;

namespace DataGraph.Tests.Runtime
{
    [TestFixture]
    public class ResultTests
    {
        [Test]
        public void Success_IsSuccess_ReturnsTrue()
        {
            var result = Result<int>.Success(42);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsFailure);
        }

        [Test]
        public void Success_Value_ReturnsValue()
        {
            var result = Result<string>.Success("hello");

            Assert.AreEqual("hello", result.Value);
        }

        [Test]
        public void Success_AccessError_Throws()
        {
            var result = Result<int>.Success(1);

            Assert.Throws<InvalidOperationException>(() => _ = result.Error);
        }

        [Test]
        public void Failure_IsFailure_ReturnsTrue()
        {
            var result = Result<int>.Failure("something went wrong");

            Assert.IsTrue(result.IsFailure);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Failure_Error_ReturnsMessage()
        {
            var result = Result<int>.Failure("bad input");

            Assert.AreEqual("bad input", result.Error);
        }

        [Test]
        public void Failure_AccessValue_Throws()
        {
            var result = Result<int>.Failure("error");

            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        [Test]
        public void TryGetValue_Success_ReturnsTrueAndValue()
        {
            var result = Result<int>.Success(99);

            bool got = result.TryGetValue(out int value);

            Assert.IsTrue(got);
            Assert.AreEqual(99, value);
        }

        [Test]
        public void TryGetValue_Failure_ReturnsFalse()
        {
            var result = Result<int>.Failure("nope");

            bool got = result.TryGetValue(out _);

            Assert.IsFalse(got);
        }

        [Test]
        public void Map_Success_TransformsValue()
        {
            var result = Result<int>.Success(5);

            var mapped = result.Map(x => x * 2);

            Assert.IsTrue(mapped.IsSuccess);
            Assert.AreEqual(10, mapped.Value);
        }

        [Test]
        public void Map_Failure_PropagatesError()
        {
            var result = Result<int>.Failure("err");

            var mapped = result.Map(x => x * 2);

            Assert.IsTrue(mapped.IsFailure);
            Assert.AreEqual("err", mapped.Error);
        }

        [Test]
        public void Bind_Success_ChainsOperation()
        {
            var result = Result<int>.Success(10);

            var bound = result.Bind(x =>
                x > 0
                    ? Result<string>.Success($"positive: {x}")
                    : Result<string>.Failure("not positive"));

            Assert.IsTrue(bound.IsSuccess);
            Assert.AreEqual("positive: 10", bound.Value);
        }

        [Test]
        public void Bind_Failure_PropagatesOriginalError()
        {
            var result = Result<int>.Failure("original error");

            var bound = result.Bind(x => Result<string>.Success("should not reach"));

            Assert.IsTrue(bound.IsFailure);
            Assert.AreEqual("original error", bound.Error);
        }

        [Test]
        public void Bind_ChainedFailure_ReturnsChainedError()
        {
            var result = Result<int>.Success(-1);

            var bound = result.Bind(x =>
                x > 0
                    ? Result<string>.Success("ok")
                    : Result<string>.Failure("chained failure"));

            Assert.IsTrue(bound.IsFailure);
            Assert.AreEqual("chained failure", bound.Error);
        }

        [Test]
        public void ValueOr_Success_ReturnsValue()
        {
            var result = Result<int>.Success(42);

            Assert.AreEqual(42, result.ValueOr(0));
        }

        [Test]
        public void ValueOr_Failure_ReturnsFallback()
        {
            var result = Result<int>.Failure("err");

            Assert.AreEqual(0, result.ValueOr(0));
        }

        [Test]
        public void ValueOrFactory_Failure_InvokesFactory()
        {
            var result = Result<string>.Failure("missing");

            string value = result.ValueOr(err => $"fallback: {err}");

            Assert.AreEqual("fallback: missing", value);
        }

        [Test]
        public void ToString_Success_FormatsCorrectly()
        {
            var result = Result<int>.Success(7);

            Assert.AreEqual("Success(7)", result.ToString());
        }

        [Test]
        public void ToString_Failure_FormatsCorrectly()
        {
            var result = Result<int>.Failure("oops");

            Assert.AreEqual("Failure(oops)", result.ToString());
        }
    }
}
