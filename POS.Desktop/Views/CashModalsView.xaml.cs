using System.Windows.Controls;

namespace POS.Desktop.Views;

/// <summary>
/// Modales de caja (P2.2c): apertura con efectivo inicial, retiro con motivo
/// y cierre con conteo físico. Vive a nivel MainWindow (DataContext =
/// MainWindowViewModel) porque el header de caja es global.
/// </summary>
public partial class CashModalsView : UserControl
{
    public CashModalsView()
    {
        InitializeComponent();
    }
}