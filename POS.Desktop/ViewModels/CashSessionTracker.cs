using CommunityToolkit.Mvvm.ComponentModel;
using POS.Application.Cash;

namespace POS.Desktop.ViewModels;

/// <summary>
/// Estado de caja COMPARTIDO (P2.2c): singleton observable que el header
/// (MainWindowViewModel) y la pantalla de venta consultan/actualizan. La caja es
/// global al usuario autenticado: el badge del header y el bloqueo de COBRAR
/// reaccionan al mismo estado sin acoplar los VMs.
/// </summary>
public partial class CashSessionTracker : ObservableObject
{
    [ObservableProperty]
    private CashSessionDto? _current;

    /// <summary>True si hay caja abierta (COBRAR habilitado / badge verde).</summary>
    public bool HasOpen => Current is not null;

    public string BadgeText => Current is null
        ? "Caja cerrada"
        : $"Caja #{Current.Id} · abierta";

    public string StatusText => Current is null
        ? "Abra la caja para cobrar"
        : $"Fondo RD$ {Current.InitialCash:N2} · Efectivo RD$ {Current.CashSalesTotal:N2}";

    public void Set(CashSessionDto? session)
    {
        Current = session;
        OnPropertyChanged(nameof(HasOpen));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(StatusText));
    }
}