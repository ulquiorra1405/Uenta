using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>Categoría con el número de productos asociados (para el gestor).</summary>
public record CategoryWithCount(Category Category, int ProductCount);

public interface ICategoryRepository
{
    /// <summary>Solo categorías activas (venta y selector de la ficha).</summary>
    Task<List<Category>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>Todas las categorías (gestión) con su conteo de productos.</summary>
    Task<List<CategoryWithCount>> GetAllWithProductCountAsync(CancellationToken ct = default);

    Task<Category?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Nombre único (case-insensitive); <paramref name="excludeId"/> excluye una categoría (renombrar).</summary>
    Task<bool> ExistsByNameAsync(string name, long? excludeId = null, CancellationToken ct = default);

    Task AddAsync(Category category, CancellationToken ct = default);

    /// <summary>Marca la categoría para actualizar. NO persiste: lo hace el caso de uso.</summary>
    Task UpdateAsync(Category category, CancellationToken ct = default);

    /// <summary>Persiste los cambios pendientes del contexto.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}