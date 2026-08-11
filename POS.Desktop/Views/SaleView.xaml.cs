using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

/// <summary>
/// Code-behind DELGADO (regla del proyecto): solo presentación pura —
/// reloj en vivo y devolución de foco al buscador. Toda la lógica vive en
/// <see cref="SaleViewModel"/>.
/// </summary>
public partial class SaleView : UserControl
{
    private readonly DispatcherTimer _clockTimer;

    public SaleView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        Loaded += (_, _) =>
        {
            if (DataContext is SaleViewModel vm)
            {
                vm.FocusSearchRequested += FocusSearch;
                vm.CatalogFocusRequested += FocusCatalogSearch;
                FocusSearch();
            }
        };

        Unloaded += (_, _) =>
        {
            _clockTimer.Stop();
            if (DataContext is SaleViewModel vm)
            {
                vm.FocusSearchRequested -= FocusSearch;
                vm.CatalogFocusRequested -= FocusCatalogSearch;
            }
        };
    }

    private void UpdateClock() => ClockText.Text = DateTime.Now.ToString("HH:mm");

    private void FocusSearch() => SearchBox.Focus();

    private void FocusCatalogSearch() => CatalogSearchBox.Focus();
}
