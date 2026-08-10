using System.Windows;

namespace POS.Desktop;

/// <summary>
/// Marca un botón de navegación del sidebar como "activo".
/// El template del botón (SidebarNavButton en MainWindow.xaml) usa esta propiedad
/// para pintar el indicador de barra izquierda (3px, PrimaryBrush) — estilo Swiss.
/// </summary>
public static class SidebarNav
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive", typeof(bool), typeof(SidebarNav), new PropertyMetadata(false));

    public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);

    public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);
}
