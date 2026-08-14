using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using POS.Desktop.ViewModels;

namespace POS.Desktop;

public partial class MainWindow : Window
{
    // WM_GETMINMAXINFO: Windows lo envía antes de maximizar/redimensionar.
    // Sin este handler, la ventana con WindowChrome se maximiza al monitor COMPLETO
    // (bordes de resize invisibles + posibles píxeles cortados en las 4 direcciones),
    // en vez del área de trabajo (excluye la barra de tareas). Con chrome nativo
    // esto lo hace el sistema; con WindowChrome propio hay que recortarlo aquí.
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += (_, _) => UpdateMaxRestoreIcon();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    // Maximizar exactamente al área de trabajo del monitor
                    // (pantalla menos barra de tareas): ni bordes fuera ni píxeles cortados.
                    mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                    mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                    mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
                }
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ─── Botones de control de la barra de título ───
    // El drag y el doble clic (max/restaurar) los maneja WindowChrome vía CaptionHeight;
    // aquí solo respondemos a los clics explícitos de los botones.

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Icono del botón max/restaurar según el estado de la ventana.</summary>
    private void UpdateMaxRestoreIcon()
    {
        if (MaxRestoreIcon is Path path)
        {
            path.Data = WindowState == WindowState.Maximized
                ? (System.Windows.Media.Geometry)FindResource("RestoreIcon")
                : (System.Windows.Media.Geometry)FindResource("MaximizeIcon");
        }
        MaxRestoreButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
    }
}
