namespace NileChain.Application.Common
{
    public class Result
    {
        protected Result(bool isSuccess, Error? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error? Error { get; }

        public static Result Success()
            => new(true, null);

        public static Result Failure(Error error)
            => new(false, error);
    }

    public class Result<T> : Result
    {
        private readonly T? _value;

        private Result(
            T value)
            : base(true, null)
        {
            _value = value;
        }

        private Result(
            Error error)
            : base(false, error)
        {
        }

        public T Value =>
            IsSuccess
                ? _value!
                : throw new InvalidOperationException(
                    "No value for failed result.");

        public static Result<T> Success(T value)
            => new(value);

        public new static Result<T> Failure(Error error)
            => new(error);
    }
}
