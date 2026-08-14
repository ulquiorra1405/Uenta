using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Desktop.ViewModels;

namespace POS.Desktop.Views;

/// <summary>
/// Popup de catálogo visual (modelo B, F2). Code-behind DELGADO: devolución de foco
/// al buscador y ruteo de teclas que WPF no expone vía KeyBinding (Enter del TextBox).
/// Vive en el MainWindow (overlay global): DataContext = Current (el VM activo).
/// </summary>
public partial class CatalogPopupView : UserControl
{
    public CatalogPopupView()
    {
        InitializeComponent();

        // El popup siempre está cargado (vive en MainWindow). Se suscribe al VM
        // cuando llega (navegación a Venta) y se desuscribe al cambiar de pantalla.
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SaleViewModel oldVm)
                oldVm.CatalogFocusRequested -= FocusCatalogSearch;

            if (DataContext is SaleViewModel vm)
                vm.CatalogFocusRequested += FocusCatalogSearch;
        };
    }

    private void FocusCatalogSearch() => CatalogSearchBox.Focus();

    /// <summary>Clic en el buscador VACÍO: caret al inicio (consistente con la línea de entrada).</summary>
    private void CatalogSearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (CatalogSearchBox.Text.Length == 0)
        {
            CatalogSearchBox.Focus();
            CatalogSearchBox.CaretIndex = 0;
            e.Handled = true;
        }
    }

    /// <summary>Enter en el buscador del catálogo: el TextBox se lo traga (ver TicketEntryView).</summary>
    private void CatalogSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is SaleViewModel vm)
            vm.AddFromCatalogSearchCommand.Execute(null);
        e.Handled = true;
    }
}
