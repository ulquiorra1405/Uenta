using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

/// <summary>
/// Línea de entrada del ticket (modelo B). Code-behind DELGADO: devolución de foco
/// (loop de escáner) y ruteo de teclas que WPF no expone vía KeyBinding (Enter se lo
/// traga el TextBox). Toda la lógica vive en <see cref="SaleViewModel"/>.
/// </summary>
public partial class TicketEntryView : UserControl
{
    public TicketEntryView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is SaleViewModel vm)
            {
                vm.FocusSearchRequested += FocusSearch;
                FocusSearch();
            }
        };

        Unloaded += (_, _) =>
        {
            if (DataContext is SaleViewModel vm)
                vm.FocusSearchRequested -= FocusSearch;
        };
    }

    private void FocusSearch() => SearchBox.Focus();

    /// <summary>
    /// Clic en cualquier parte de la línea de entrada VACÍA: el caret SIEMPRE al inicio
    /// (posición 0), nunca en medio del hint superpuesto. e.Handled=true impide el
    /// posicionamiento por clic del TextBox.
    /// </summary>
    private void SearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SearchBox.Text.Length == 0)
        {
            SearchBox.Focus();
            SearchBox.CaretIndex = 0;
            e.Handled = true;
        }
    }

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
}