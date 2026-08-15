using POS.Domain.Entities;

namespace POS.Application.Abstractions;

/// <summary>Repositorio de ajustes (clave/valor). El valor vive como string; el
/// <see cref="POS.Application.Settings.SettingsService"/> tipa la lectura.</summary>
public interface ISettingRepository
{
    Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<List<Setting>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(Setting setting, CancellationToken ct = default);
}