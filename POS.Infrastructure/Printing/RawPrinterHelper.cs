using System.Runtime.InteropServices;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Envío de bytes crudos a una impresora por nombre vía P/Invoke a winspool.drv
/// (clásico "RawPrinterHelper"). Sin dependencias de terceros; suficiente para
/// térmicas ESC/POS que reciben el flujo de bytes tal cual.
/// </summary>
public static class RawPrinterHelper
{
    // ─── winspool.drv ───
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pDatatype;
    }

    /// <summary>Impresoras instaladas en el sistema (para el selector de Ajustes).</summary>
    public static string[] GetInstalledPrinters()
        => System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToArray();

    /// <summary>
    /// Envía bytes crudos a la impresora. Lanza <see cref="InvalidOperationException"/>
    /// con mensaje claro si el nombre no existe o la impresora no acepta el trabajo.
    /// </summary>
    public static void SendBytesToPrinter(string printerName, byte[] data, string docName = "Uenta")
    {
        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            throw new InvalidOperationException(
                $"No se pudo abrir la impresora '{printerName}'. Verifique el nombre en Ajustes.");

        try
        {
            var di = new DOC_INFO_1
            {
                pDocName = docName,
                pOutputFile = null,
                pDatatype = "RAW",
            };

            if (!StartDocPrinter(hPrinter, 1, ref di))
                throw new InvalidOperationException(
                    $"La impresora '{printerName}' no aceptó el trabajo de impresión.");

            try
            {
                if (!StartPagePrinter(hPrinter))
                    throw new InvalidOperationException($"No se pudo iniciar la página en '{printerName}'.");

                try
                {
                    var unmanaged = Marshal.AllocCoTaskMem(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, unmanaged, data.Length);
                        if (!WritePrinter(hPrinter, unmanaged, data.Length, out _))
                            throw new InvalidOperationException(
                                $"No se pudo escribir el recibo en '{printerName}'.");
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(unmanaged);
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }
}