namespace Pragma.Application.Common;

/// <summary>
/// Əməliyyatın nəticəsini ifadə edən Result pattern sinfi.
/// Biznes xətaları üçün Exception fırlatmağın qarşısını alır (High Performance & Clean Code).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Xəta statusu və nəticə uyğun gəlmir.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>
/// Dəyər qaytaran əməliyyatlar üçün tipləşdirilmiş Result pattern sinfi.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Uğursuz nəticənin dəyəri əldə edilə bilməz.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public static new Result<TValue> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
