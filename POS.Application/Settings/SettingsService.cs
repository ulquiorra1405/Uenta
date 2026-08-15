using POS.Application.Abstractions;

namespace POS.Application.Settings;

/// <summary>
/// Acceso tipado a los ajustes persistentes (clave/valor en <c>Setting</c>).
/// Cada lectura parte de un default sano; si el valor guardado no se puede
/// interpretar, se devuelve el default (nunca lanza).
/// </summary>
public class SettingsService
{
    private readonly ISettingRepository _repository;

    public SettingsService(ISettingRepository repository) => _repository = repository;

    // ─── Lectura tipada ───

    public async Task<string> GetAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        var setting = await _repository.GetByKeyAsync(key, ct);
        return string.IsNullOrEmpty(setting?.Value) ? defaultValue : setting!.Value;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, defaultValue ? "1" : "0", ct);
        return raw is "1" or "true" or "True" or "TRUE";
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, defaultValue.ToString(), ct);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    public async Task<ReceiptSettingsDto> GetReceiptSettingsAsync(CancellationToken ct = default)
    {
        var printerName = await GetAsync(SettingKeys.PrinterName, "", ct);
        var autoPrint = await GetBoolAsync(SettingKeys.AutoPrint, true, ct);
        var copies = Math.Clamp(await GetIntAsync(SettingKeys.Copies, 1, ct), 1, 9);
        var businessName = await GetAsync(SettingKeys.BusinessName, "", ct);
        var rnc = await GetAsync(SettingKeys.BusinessRnc, "", ct);
        var address = await GetAsync(SettingKeys.BusinessAddress, "", ct);
        var footer = await GetAsync(SettingKeys.ReceiptFooter, "", ct);

        return new ReceiptSettingsDto
        {
            PrinterName = printerName,
            AutoPrint = autoPrint,
            Copies = copies,
            BusinessName = businessName,
            BusinessRnc = rnc,
            BusinessAddress = address,
            ReceiptFooter = footer,
        };
    }

    // ─── Escritura ───

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await _repository.SaveAsync(new POS.Domain.Entities.Setting { Key = key, Value = value }, ct);
    }

    public async Task SetBoolAsync(string key, bool value, CancellationToken ct = default)
    {
        await SetAsync(key, value ? "1" : "0", ct);
    }

    public async Task SetIntAsync(string key, int value, CancellationToken ct = default)
    {
        await SetAsync(key, value.ToString(), ct);
    }
}