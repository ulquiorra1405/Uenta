using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Application.Abstractions;
using POS.Application.Settings;
using POS.Infrastructure.Printing;
using System.Windows;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Pantalla de Ajustes (Fase 1A, P1.3): impresora + auto-impresión + copias +
/// datos del negocio para el recibo. Persiste en <c>Setting</c> (clave/valor).
/// P4.3: backup/restore de la base de datos desde la misma pantalla.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDatabaseBackupService _backup;

    /// <summary>Opción "sin impresora" del selector (dejar de imprimir).</summary>
    public const string NoPrinterOption = "(Sin impresora)";

    public SettingsViewModel(SettingsService settings, IDatabaseBackupService backup)
    {
        _settings = settings;
        _backup = backup;
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

    [ObservableProperty]
    private string? _backupMessage;

    [ObservableProperty]
    private bool _isBackupBusy;

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

    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        if (IsBackupBusy) return;
        BackupMessage = null;

        var dialog = new SaveFileDialog
        {
            Title = "Exportar copia de seguridad",
            Filter = "Base de datos SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
            FileName = $"Uenta-backup-{DateTime.Now:yyyyMMdd-HHmm}.db",
            DefaultExt = ".db",
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true) return;

        IsBackupBusy = true;
        try
        {
            var result = await _backup.ExportAsync(dialog.FileName);
            BackupMessage = result.IsSuccess
                ? $"Copia de seguridad creada en {dialog.FileName}"
                : $"No se pudo exportar: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            BackupMessage = $"No se pudo exportar: {ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsBackupBusy) return;
        BackupMessage = null;

        var dialog = new OpenFileDialog
        {
            Title = "Restaurar copia de seguridad",
            Filter = "Base de datos SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            "Restaurar reemplaza TODOS los datos actuales con el contenido de la copia.\n\n" +
            "Se creará una copia de seguridad automática de la base actual antes de continuar.\n\n" +
            "¿Desea continuar?",
            "Restaurar base de datos",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBackupBusy = true;
        try
        {
            var result = await _backup.RestoreAsync(dialog.FileName);
            BackupMessage = result.IsSuccess
                ? "Base de datos restaurada correctamente. Reinicie la aplicación para cargar los datos."
                : $"No se pudo restaurar: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            BackupMessage = $"No se pudo restaurar: {ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }
}