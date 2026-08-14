using System.Windows.Controls;

namespace POS.Desktop.Views;

/// <summary>
/// Cascarón de la pantalla de venta (P0.2): cada zona vive en su UserControl
/// (TicketLinesView, TicketEntryView, TotalsPanelView, CatalogPopupView,
/// PaymentModalView, ResultModalView). La lógica vive en el ViewModel;
/// los code-behind de las zonas manejan solo presentación pura.
/// </summary>
public partial class SaleView : UserControl
{
    public SaleView() => InitializeComponent();
}