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
    public async Task Create_DuplicatedBarcode_Fails()
    {
        var first = ValidRequest("Café");
        first.Barcode = "7501000100101";
        await _productService.CreateAsync(first);

        var second = ValidRequest("Otro café");
        second.Sku = "CAF-002";
        second.Barcode = "7501000100101";

        var result = await _productService.CreateAsync(second);

        Assert.True(result.IsFailure);
        Assert.Equal("BARCODE_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_NormalizesSkuToUpper()
    {
        var request = ValidRequest();
        request.Sku = "  cafe-001  ";

        var result = await _productService.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("CAFE-001", result.Value!.Sku);
    }

    [Fact]
    public async Task Create_SameSkuDifferentCase_Fails()
    {
        var first = ValidRequest("Café");
        first.Sku = "CAFE-001";
        await _productService.CreateAsync(first);

        var second = ValidRequest("Otro café");
        second.Sku = "cafe-001";

        var result = await _productService.CreateAsync(second);

        Assert.True(result.IsFailure);
        Assert.Equal("SKU_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_NullSkuAndBarcode_Allowed()
    {
        var request = ValidRequest();
        request.Sku = "   ";
        request.Barcode = null;

        var result = await _productService.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Sku);
        Assert.Null(result.Value.Barcode);
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

        var results = await _productService.SearchAllAsync("café");

        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.Contains("Café", p.Name));
    }

    [Fact]
    public async Task Deactivate_HidesFromActiveSearch_ButShowsInAll()
    {
        var created = (await _productService.CreateAsync(ValidRequest())).Value!;

        var result = await _productService.DeactivateAsync(created.Id);

        Assert.True(result.IsSuccess);
        var active = await _productService.SearchActiveAsync("Café");
        Assert.Empty(active);

        // Gestión: el inactivo sigue visible (para poder reactivarlo).
        var all = await _productService.SearchAllAsync("Café");
        Assert.Single(all);
        Assert.False(all[0].IsActive);
    }

    [Fact]
    public async Task Reactivate_RestoresProductToActiveSearch()
    {
        var created = (await _productService.CreateAsync(ValidRequest())).Value!;
        await _productService.DeactivateAsync(created.Id);

        var result = await _productService.ReactivateAsync(created.Id);

        Assert.True(result.IsSuccess);
        var active = await _productService.SearchActiveAsync("Café");
        Assert.Single(active);
        Assert.True(active[0].IsActive);
    }

    [Fact]
    public async Task DuplicatePreview_AppliesRuleP9()
    {
        var source = (await _productService.CreateAsync(ValidRequest())).Value!;

        var copy = _productService.CreateDuplicatePreview(source);

        Assert.Equal("Café con leche (copia)", copy.Name);
        Assert.Null(copy.Sku);      // SKU propio, lo define el usuario
        Assert.Null(copy.Barcode);  // código propio
        Assert.Equal(0, copy.Stock); // stock propio, no se duplica inventario
        Assert.True(copy.IsActive);
        Assert.Equal(source.Price.Amount, copy.Price.Amount);
    }

    [Fact]
    public async Task Category_CreateAndRename()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();

        var created = await categoryService.CreateAsync("  Bebidas  ");
        Assert.True(created.IsSuccess);
        Assert.Equal("Bebidas", created.Value!.Name);

        var renamed = await categoryService.RenameAsync(created.Value.Id, "Bebidas frías");
        Assert.True(renamed.IsSuccess);

        var all = await categoryService.GetAllAsync();
        Assert.Contains(all, c => c.Id == created.Value.Id && c.Name == "Bebidas frías");
    }

    [Fact]
    public async Task Category_RenameEmptyName_Fails()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        var created = await categoryService.CreateAsync("Bebidas");

        var result = await categoryService.RenameAsync(created.Value!.Id, "   ");

        Assert.True(result.IsFailure);
        Assert.Equal("NAME_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Category_CreateDuplicateName_Fails()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        await categoryService.CreateAsync("Bebidas");

        // Mismo nombre con distinta capitalización: normalizado, no debe pasar.
        var result = await categoryService.CreateAsync("  bebidas ");

        Assert.True(result.IsFailure);
        Assert.Equal("NAME_DUPLICATED", result.ErrorCode);
    }

    [Fact]
    public async Task Category_RenameToExistingName_Fails()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        var a = (await categoryService.CreateAsync("Bebidas")).Value!;
        var b = (await categoryService.CreateAsync("Snacks")).Value!;

        var result = await categoryService.RenameAsync(b.Id, "BEBIDAS");

        Assert.True(result.IsFailure);
        Assert.Equal("NAME_DUPLICATED", result.ErrorCode);
        // El nombre original se conserva.
        var all = await categoryService.GetAllAsync();
        Assert.Contains(all, c => c.Id == b.Id && c.Name == "Snacks");
    }

    [Fact]
    public async Task Category_Deactivate_HidesFromActiveList_ButShowsInAll()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        var created = (await categoryService.CreateAsync("Bebidas")).Value!;

        var result = await categoryService.DeactivateAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(await categoryService.GetAllActiveAsync(), c => c.Id == created.Id);
        var all = await categoryService.GetAllAsync();
        Assert.Contains(all, c => c.Id == created.Id && !c.IsActive);
    }

    [Fact]
    public async Task Category_Reactivate_RestoresToActiveList()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        var created = (await categoryService.CreateAsync("Bebidas")).Value!;
        await categoryService.DeactivateAsync(created.Id);

        var result = await categoryService.ReactivateAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Contains(await categoryService.GetAllActiveAsync(), c => c.Id == created.Id);
    }

    [Fact]
    public async Task Category_Deactivate_NotFound_Fails()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();

        var result = await categoryService.DeactivateAsync(99999);

        Assert.True(result.IsFailure);
        Assert.Equal("CATEGORY_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Category_ProductCount_ReflectsAssignedProducts()
    {
        var categoryService = _services.GetRequiredService<CategoryService>();
        var bebidas = (await categoryService.CreateAsync("Bebidas")).Value!;
        var snacks = (await categoryService.CreateAsync("Snacks")).Value!;

        var p1 = ValidRequest("Jugo");
        p1.Sku = "JGO-T1";
        p1.CategoryId = bebidas.Id;
        var p2 = ValidRequest("Refresco");
        p2.Sku = "REF-T1";
        p2.CategoryId = bebidas.Id;
        var p3 = ValidRequest("Empanada");
        p3.Sku = "EMP-T1";
        p3.CategoryId = snacks.Id;
        await _productService.CreateAsync(p1);
        await _productService.CreateAsync(p2);
        await _productService.CreateAsync(p3);

        var all = await categoryService.GetAllAsync();
        Assert.Equal(2, all.Single(c => c.Id == bebidas.Id).ProductCount);
        Assert.Equal(1, all.Single(c => c.Id == snacks.Id).ProductCount);

        // Sin productos → 0.
        var sinCategoria = (await categoryService.CreateAsync("Sin usar")).Value!;
        var updated = await categoryService.GetAllAsync();
        Assert.Equal(0, updated.Single(c => c.Id == sinCategoria.Id).ProductCount);
    }
}
