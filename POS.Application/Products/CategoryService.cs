using POS.Application.Abstractions;
using POS.Domain.Entities;

namespace POS.Application.Products;

public class CategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CategoryService
{
    private readonly ICategoryRepository _categories;

    public CategoryService(ICategoryRepository categories) => _categories = categories;

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _categories.GetAllAsync(ct);
        return categories.Select(c => new CategoryDto { Id = c.Id, Name = c.Name }).ToList();
    }

    public async Task<long> CreateAsync(string name, CancellationToken ct = default)
    {
        var category = new Category { Name = name.Trim() };
        await _categories.AddAsync(category, ct);
        return category.Id;
    }
}
