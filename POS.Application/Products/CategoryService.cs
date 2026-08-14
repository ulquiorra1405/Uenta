using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Products;

public class CategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public class CategoryService
{
    private readonly ICategoryRepository _categories;

    public CategoryService(ICategoryRepository categories) => _categories = categories;

    /// <summary>Todas las categorías (gestión): incluye inactivas y el conteo de productos.</summary>
    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _categories.GetAllWithProductCountAsync(ct);
        return rows.Select(r => new CategoryDto
        {
            Id = r.Category.Id,
            Name = r.Category.Name,
            IsActive = r.Category.IsActive,
            ProductCount = r.ProductCount
        }).ToList();
    }

    /// <summary>Solo categorías activas (venta y selector de la ficha de producto).</summary>
    public async Task<List<CategoryDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var categories = await _categories.GetAllActiveAsync(ct);
        return categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            IsActive = c.IsActive
        }).ToList();
    }

    public async Task<Result<CategoryDto>> CreateAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<CategoryDto>("NAME_REQUIRED", "El nombre de la categoría es obligatorio.");

        if (await _categories.ExistsByNameAsync(name, ct: ct))
            return Result.Failure<CategoryDto>("NAME_DUPLICATED", $"Ya existe una categoría llamada '{name}'.");

        var category = new Category { Name = name };
        await _categories.AddAsync(category, ct);
        return Result.Success(new CategoryDto { Id = category.Id, Name = category.Name, IsActive = true });
    }

    public async Task<Result> RenameAsync(long id, string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("NAME_REQUIRED", "El nombre de la categoría es obligatorio.");

        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("CATEGORY_NOT_FOUND", "La categoría no existe.");

        if (await _categories.ExistsByNameAsync(name, excludeId: id, ct: ct))
            return Result.Failure("NAME_DUPLICATED", $"Ya existe una categoría llamada '{name}'.");

        category.Name = name;
        await _categories.UpdateAsync(category, ct);
        await _categories.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Desactiva una categoría (soft-delete): se oculta de venta y del selector de la ficha,
    /// pero los productos asociados NO se tocan y el historial queda intacto.
    /// </summary>
    public async Task<Result> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("CATEGORY_NOT_FOUND", "La categoría no existe.");

        category.IsActive = false;
        await _categories.UpdateAsync(category, ct);
        await _categories.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(long id, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("CATEGORY_NOT_FOUND", "La categoría no existe.");

        category.IsActive = true;
        await _categories.UpdateAsync(category, ct);
        await _categories.SaveChangesAsync(ct);
        return Result.Success();
    }
}