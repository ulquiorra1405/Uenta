using POS.Application.Common;

namespace POS.Tests;

public class ResultTests
{
    [Fact]
    public void Success_CarriesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_CarriesErrorCodeAndMessage()
    {
        var result = Result.Failure<int>("STOCK_INSUFFICIENT", "No hay stock.");

        Assert.True(result.IsFailure);
        Assert.Equal("STOCK_INSUFFICIENT", result.ErrorCode);
        Assert.Equal("No hay stock.", result.ErrorMessage);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void PlainFailure_HasNoValue()
    {
        var result = Result.Failure("CAJA_CERRADA", "La caja está cerrada.");

        Assert.True(result.IsFailure);
        Assert.Equal("CAJA_CERRADA", result.ErrorCode);
    }
}
