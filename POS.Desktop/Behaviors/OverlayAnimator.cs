using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace POS.Desktop.Behaviors
{
    /// <summary>
    /// Animación de entrada/salida para overlays modales (scrim + popup).
    /// Uso: en el Grid overlay (el que tiene el ScrimBrush) → attach:
    ///   beh:OverlayAnimator.IsOpen="{Binding IsXxxOpen}"
    /// El grid controla su propia Visibility; al abrir hace fade del scrim y
    /// zoom suave del popup (0.96 → 1), al cerrar invierte y colapsa.
    /// </summary>
    public static class OverlayAnimator
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.RegisterAttached(
                "IsOpen", typeof(bool), typeof(OverlayAnimator),
                new PropertyMetadata(false, OnIsOpenChanged));

        public static void SetIsOpen(DependencyObject element, bool value) =>
            element.SetValue(IsOpenProperty, value);

        public static bool GetIsOpen(DependencyObject element) =>
            (bool)element.GetValue(IsOpenProperty);

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe)
                return;

            if ((bool)e.NewValue)
                Show(fe);
            else
                Hide(fe);
        }

        private static void Show(FrameworkElement fe)
        {
            // Limpiar cualquier animación de cierre pendiente y asegurar estado visible.
            fe.BeginAnimation(UIElement.OpacityProperty, null);
            ResetPopupScale(fe, 1.0);

            fe.Opacity = 0.0;
            fe.Visibility = Visibility.Visible;

            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            fe.BeginAnimation(UIElement.OpacityProperty, fade);

            AnimatePopupScale(fe, 0.96, 1.0, TimeSpan.FromMilliseconds(200));
        }

        private static void Hide(FrameworkElement fe)
        {
            if (fe.Visibility != Visibility.Visible)
                return;

            fe.BeginAnimation(UIElement.OpacityProperty, null);

            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.HoldEnd,
            };
            fade.Completed += (_, _) =>
            {
                fe.BeginAnimation(UIElement.OpacityProperty, null);
                fe.Visibility = Visibility.Collapsed;
                ResetPopupScale(fe, 1.0);
            };
            fe.BeginAnimation(UIElement.OpacityProperty, fade);

            AnimatePopupScale(fe, 1.0, 0.97, TimeSpan.FromMilliseconds(120));
        }

        /// <summary>Busca el Border del popup (hijo directo del grid overlay) y le anima la escala.</summary>
        private static void AnimatePopupScale(FrameworkElement fe, double from, double to, TimeSpan duration)
        {
            if (VisualTreeHelper.GetChildrenCount(fe) == 0)
                return;

            if (VisualTreeHelper.GetChild(fe, 0) is not Border popup)
                return;

            popup.RenderTransformOrigin = new Point(0.5, 0.5);
            var scale = new ScaleTransform(from, from);
            popup.RenderTransform = scale;

            var animX = new DoubleAnimation(from, to, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            var animY = new DoubleAnimation(from, to, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd,
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        }

        private static void ResetPopupScale(FrameworkElement fe, double value)
        {
            if (VisualTreeHelper.GetChildrenCount(fe) == 0)
                return;

            if (VisualTreeHelper.GetChild(fe, 0) is not Border popup)
                return;

            if (popup.RenderTransform is ScaleTransform st)
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        }
    }
}