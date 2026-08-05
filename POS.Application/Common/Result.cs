namespace POS.Application.Common;

/// <summary>
/// Contrato de retorno de los casos de uso.
/// Los errores de negocio ESPERADOS (stock, caja cerrada, validaciones) NO usan
/// excepciones: se devuelven como Result.Failure. La UI decide cómo mostrarlo;
/// la API futura lo serializa como HTTP 400 con el código de error.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);

    public static Result<T> Success<T>(T value) => new(value, true, null, null);

    public static Result<T> Failure<T>(string errorCode, string errorMessage) =>
        new(default, false, errorCode, errorMessage);
}

public class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, string? errorCode, string? errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        Value = value;
    }

    public T? Value { get; }
}
