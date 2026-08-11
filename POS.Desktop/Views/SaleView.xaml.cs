using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

/// <summary>
/// Code-behind DELGADO (regla del proyecto): solo presentación pura —
/// reloj en vivo, devolución de foco y ruteo de teclas que WPF no expone
/// vía KeyBinding (Enter se lo traga el TextBox). Toda la lógica vive en
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

    /// <summary>
    /// Enter en la línea de entrada: el TextBox marca la tecla como manejada
    /// (AcceptsReturn=false) y el KeyBinding nunca se evalúa; el PreviewKeyDown
    /// la intercepta antes y delega en el comando del ViewModel.
    /// </summary>
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SaleViewModel vm)
            vm.AddFromSearchCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Enter en el buscador del catálogo (mismo motivo que arriba).</summary>
    private void CatalogSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SaleViewModel vm)
            vm.AddFromCatalogSearchCommand.Execute(null);
        e.Handled = true;
    }
}
