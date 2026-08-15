using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Application.Settings;
using POS.Infrastructure.Printing;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Pantalla de Ajustes (Fase 1A, P1.3): impresora + auto-impresión + copias +
/// datos del negocio para el recibo. Persiste en <c>Setting</c> (clave/valor).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    /// <summary>Opción "sin impresora" del selector (dejar de imprimir).</summary>
    public const string NoPrinterOption = "(Sin impresora)";

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        PrinterOptions = RawPrinterHelper.GetInstalledPrinters().Prepend(NoPrinterOption).ToList();
    }

    public List<string> PrinterOptions { get; }

    [ObservableProperty]
    private string _printerName = NoPrinterOption;

    [ObservableProperty]
    private bool _autoPrint = true;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string _businessRnc = string.Empty;

    [ObservableProperty]
    private string _businessAddress = string.Empty;

    [ObservableProperty]
    private string _receiptFooter = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _saveMessage;

    public override async Task OnNavigatedToAsync()
    {
        try
        {
            var dto = await _settings.GetReceiptSettingsAsync();
            PrinterName = string.IsNullOrWhiteSpace(dto.PrinterName) ? NoPrinterOption : dto.PrinterName;
            AutoPrint = dto.AutoPrint;
            Copies = dto.Copies;
            BusinessName = dto.BusinessName;
            BusinessRnc = dto.BusinessRnc;
            BusinessAddress = dto.BusinessAddress;
            ReceiptFooter = dto.ReceiptFooter;
        }
        catch
        {
            SaveMessage = "No se pudieron cargar los ajustes.";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        SaveMessage = null;
        try
        {
            await _settings.SetAsync(SettingKeys.PrinterName, PrinterName == NoPrinterOption ? "" : PrinterName);
            await _settings.SetBoolAsync(SettingKeys.AutoPrint, AutoPrint);
            await _settings.SetIntAsync(SettingKeys.Copies, Math.Clamp(Copies, 1, 9));
            await _settings.SetAsync(SettingKeys.BusinessName, BusinessName.Trim());
            await _settings.SetAsync(SettingKeys.BusinessRnc, BusinessRnc.Trim());
            await _settings.SetAsync(SettingKeys.BusinessAddress, BusinessAddress.Trim());
            await _settings.SetAsync(SettingKeys.ReceiptFooter, ReceiptFooter.Trim());
            SaveMessage = "Ajustes guardados.";
        }
        catch
        {
            SaveMessage = "No se pudieron guardar los ajustes.";
        }
        finally
        {
            IsSaving = false;
        }
    }
}