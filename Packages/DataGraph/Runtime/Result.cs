using System;

namespace DataGraph.Runtime
{
    /// <summary>
    /// Represents the outcome of an operation that can either succeed with a value
    /// or fail with an error message. Used instead of exceptions for flow control.
    /// </summary>
    public readonly struct Result<T>
    {
        private readonly T _value;
        private readonly string _error;

        private Result(T value, string error, bool isSuccess)
        {
            _value = value;
            _error = error;
            IsSuccess = isSuccess;
        }

        /// <summary>
        /// True if the operation succeeded and Value is available.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// True if the operation failed and Error is available.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// The success value. Throws if the result is a failure.
        /// </summary>
        public T Value => IsSuccess
            ? _value
            : throw new InvalidOperationException(
                $"Cannot access Value on a failed Result. Error: {_error}");

        /// <summary>
        /// The error message. Throws if the result is a success.
        /// </summary>
        public string Error => IsFailure
            ? _error
            : throw new InvalidOperationException(
                "Cannot access Error on a successful Result.");

        /// <summary>
        /// Creates a successful result with the given value.
        /// </summary>
        public static Result<T> Success(T value) => new(value, null, true);

        /// <summary>
        /// Creates a failed result with the given error message.
        /// </summary>
        public static Result<T> Failure(string error) => new(default, error, false);

        /// <summary>
        /// Attempts to get the value. Returns true if successful.
        /// </summary>
        public bool TryGetValue(out T value)
        {
            value = _value;
            return IsSuccess;
        }

        /// <summary>
        /// Transforms the success value using the given function.
        /// If this result is a failure, propagates the error without calling the function.
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> map)
        {
            return IsSuccess
                ? Result<TOut>.Success(map(_value))
                : Result<TOut>.Failure(_error);
        }

        /// <summary>
        /// Chains another operation that itself returns a Result.
        /// If this result is a failure, propagates the error without calling the function.
        /// </summary>
        public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
        {
            return IsSuccess
                ? bind(_value)
                : Result<TOut>.Failure(_error);
        }

        /// <summary>
        /// Returns the value if successful, or the provided fallback if failed.
        /// </summary>
        public T ValueOr(T fallback) => IsSuccess ? _value : fallback;

        /// <summary>
        /// Returns the value if successful, or invokes the factory if failed.
        /// </summary>
        public T ValueOr(Func<string, T> fallbackFactory) =>
            IsSuccess ? _value : fallbackFactory(_error);

        public override string ToString() =>
            IsSuccess ? $"Success({_value})" : $"Failure({_error})";
    }
}
