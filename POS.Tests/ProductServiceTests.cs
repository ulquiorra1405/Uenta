using Microsoft.Extensions.DependencyInjection;
using POS.Application;
using POS.Application.Products;
using POS.Infrastructure;
using POS.Infrastructure.Data;

namespace POS.Tests;

/// <summary>Casos de uso del catálogo con SQLite real (archivo temporal).</summary>
public class ProductServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pos-cat-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_dbPath};Pooling=False");
        _services = services.BuildServiceProvider();

        var db = _services.GetRequiredService<PosDbContext>();
        db.Database.EnsureCreated();

        _productService = _services.GetRequiredService<ProductService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private CreateProductRequest ValidRequest(string name = "Café con leche") => new()
    {
        Name = name,
        Sku = "CAF-001",
        Price = 100m,
        Cost = 35m,
        Stock = 10,
        MinStock = 5
    };

    [Fact]
    public async Task Create_ValidProduct_ReturnsDto()
    {
        var result = await _productService.CreateAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("Café con leche", result.Value!.Name);
        Assert.Equal(100m, result.Value.Price.Amount);
        Assert.Equal(10m, result.Value.Stock);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Create_MissingName_Fails()
    {
        var request = ValidRequest();
        request.Name = "   ";

        var result = await _productService.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("NAME_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_ZeroPrice_Fails()
    {
        var request = ValidRequest();
        request.Price = 0;

        var result = await _productService.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_PRICE", result.ErrorCode);
    }

    [Fact]
    public async Task Create_DuplicatedSku_Fails()
    {
        await _productService.CreateAsync(ValidRequest("Café"));

        var result = await _productService.CreateAsync(ValidRequest("Otro café"));

        Assert.True(result.IsFailure);
        Assert.Equal("SKU_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Update_ExistingProduct_ChangesFields()
    {
        var created = (await _productService.CreateAsync(ValidRequest())).Value!;

        var result = await _productService.UpdateAsync(new UpdateProductRequest
        {
            Id = created.Id,
            Name = "Café con leche grande",
            Sku = created.Sku,
            Price = 120m,
            Cost = 40m,
            Stock = 7,
            MinStock = 3,
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Café con leche grande", result.Value!.Name);
        Assert.Equal(120m, result.Value.Price.Amount);
    }

    [Fact]
    public async Task Update_NonexistentProduct_Fails()
    {
        var result = await _productService.UpdateAsync(new UpdateProductRequest
        {
            Id = 999,
            Name = "X",
            Price = 10m
        });

        Assert.True(result.IsFailure);
        Assert.Equal("PRODUCT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Search_ByTerm_FiltersResults()
    {
        await _productService.CreateAsync(ValidRequest("Café con leche"));
        await _productService.CreateAsync(ValidRequest("Pan de agua"));
        var cafeNegro = ValidRequest("Café negro");
        cafeNegro.Sku = "CAF-002";
        await _productService.CreateAsync(cafeNegro);

        var results = await _productService.SearchAsync("café");

        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.Contains("Café", p.Name));
    }

    [Fact]
    public async Task Deactivate_HidesFromSearch()
    {
        var created = (await _productService.CreateAsync(ValidRequest())).Value!;

        var result = await _productService.DeactivateAsync(created.Id);

        Assert.True(result.IsSuccess);
        var search = await _productService.SearchAsync("Café");
        Assert.Empty(search);
    }
}
