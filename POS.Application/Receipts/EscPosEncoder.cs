namespace POS.Application.Receipts;

/// <summary>Página de códigos para el encoder ESC/POS.</summary>
public enum EscPosCodePage
{
    /// <summary>PC437 (EE.UU.) — <c>ESC t 0</c>.</summary>
    Cp437,

    /// <summary>PC850 (Latinoamérica / español) — <c>ESC t 2</c>. Predeterminado.</summary>
    Cp850,
}

/// <summary>Tipo de corte de papel al final del recibo.</summary>
public enum EscPosCut
{
    /// <summary>Corte parcial (muesca) — <c>GS V 65</c>. Predeterminado.</summary>
    Partial,

    /// <summary>Corte completo — <c>GS V 66</c>.</summary>
    Full,
}

/// <summary>Opciones del encoder ESC/POS.</summary>
public sealed record EscPosOptions
{
    /// <summary>Opciones por defecto: CP850, corte parcial, 3 líneas antes del corte.</summary>
    public static EscPosOptions Default { get; } = new();

    /// <summary>Página de códigos (por defecto CP850, adecuada para español).</summary>
    public EscPosCodePage CodePage { get; init; } = EscPosCodePage.Cp850;

    /// <summary>Tipo de corte (por defecto parcial).</summary>
    public EscPosCut Cut { get; init; } = EscPosCut.Partial;

    /// <summary>Líneas en blanco a avanzar antes del corte (por defecto 3).</summary>
    public int FeedLinesBeforeCut { get; init; } = 3;

    /// <summary>Emitir secuencia de inicialización (ESC @ + ESC t + ESC 2).</summary>
    public bool Initialize { get; init; } = true;
}

/// <summary>
/// Encoder ESC/POS — función pura: texto del recibo (ancho 42, generado por
/// <see cref="ReceiptContentBuilder"/>) → bytes listos para la impresora térmica.
/// Emite inicialización, alineación por línea (ESC a), negrita para el TOTAL
/// (ESC E), avance de papel y corte (GS V). Convierte a CP437/CP850 con tablas
/// propias (cero dependencias) + best-fit para caracteres comunes fuera de tabla.
/// </summary>
public static class EscPosEncoder
{
    private const byte Esc = 0x1B;
    private const byte Lf = 0x0A;
    private const byte Gs = 0x1D;

    /// <summary>Tabla alta (bytes 0x80-0xFF) de PC437 — generada con .NET CodePages (autoritativo).</summary>
    private const string Cp437High =
        "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00A2\u00A3\u00A5\u20A7\u0192\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u2310\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB\u2591\u2592\u2593\u2502\u2524\u2561\u2562\u2556\u2555\u2563\u2551\u2557\u255D\u255C\u255B\u2510\u2514\u2534\u252C\u251C\u2500\u253C\u255E\u255F\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u2567\u2568\u2564\u2565\u2559\u2558\u2552\u2553\u256B\u256A\u2518\u250C\u2588\u2584\u258C\u2590\u2580\u03B1\u00DF\u0393\u03C0\u03A3\u03C3\u00B5\u03C4\u03A6\u0398\u03A9\u03B4\u221E\u03C6\u03B5\u2229\u2261\u00B1\u2265\u2264\u2320\u2321\u00F7\u2248\u00B0\u2219\u00B7\u221A\u207F\u00B2\u25A0\u00A0";

    /// <summary>Tabla alta (bytes 0x80-0xFF) de PC850.</summary>
    private const string Cp850High =
        "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00F8\u00A3\u00D8\u00D7\u0192\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u00AE\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB\u2591\u2592\u2593\u2502\u2524\u00C1\u00C2\u00C0\u00A9\u2563\u2551\u2557\u255D\u00A2\u00A5\u2510\u2514\u2534\u252C\u251C\u2500\u253C\u00E3\u00C3\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u00A4\u00F0\u00D0\u00CA\u00CB\u00C8\u0131\u00CD\u00CE\u00CF\u2518\u250C\u2588\u2584\u00A6\u00CC\u2580\u00D3\u00DF\u00D4\u00D2\u00F5\u00D5\u00B5\u00FE\u00DE\u00DA\u00DB\u00D9\u00FD\u00DD\u00AF\u00B4\u00AD\u00B1\u2017\u00BE\u00B6\u00A7\u00F7\u00B8\u00B0\u00A8\u00B7\u00B9\u00B3\u00B2\u25A0\u00A0";

    /// <summary>
    /// Codifica el texto de un recibo a bytes ESC/POS.
    /// </summary>
    /// <param name="receiptText">Salida de <see cref="ReceiptContentBuilder.Build"/> (líneas de ≤42 chars).</param>
    /// <param name="options">Opciones (página de códigos, corte, avance).</param>
    public static byte[] Encode(string receiptText, EscPosOptions? options = null)
    {
        var o = options ?? EscPosOptions.Default;
        var table = o.CodePage == EscPosCodePage.Cp850 ? Cp850High : Cp437High;
        var bytes = new List<byte>(receiptText.Length + 32);

        if (o.Initialize)
        {
            bytes.AddRange([Esc, 0x40]); // ESC @ — inicializa la impresora
            bytes.AddRange([Esc, 0x74, o.CodePage == EscPosCodePage.Cp850 ? (byte)2 : (byte)0]); // ESC t n — tabla de códigos
            bytes.AddRange([Esc, 0x32]); // ESC 2 — interlineado por defecto
        }

        int align = -1; // 0 = izquierda, 1 = centro; solo se emite ESC a al cambiar
        var lines = receiptText.Split('\n');
        var lineCount = lines.Length;
        // El '\n' final del builder produce un último elemento vacío (artefacto del split): ignorarlo.
        if (lineCount > 0 && lines[lineCount - 1].Length == 0)
            lineCount--;

        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                bytes.Add(Lf);
                continue;
            }

            var centered = IsCenteredLine(line);
            var target = centered ? 1 : 0;
            if (target != align)
            {
                bytes.AddRange([Esc, 0x61, (byte)target]); // ESC a n — alineación
                align = target;
            }

            var text = centered ? line.Trim() : line.TrimEnd();
            var bold = text.TrimStart().StartsWith("TOTAL", StringComparison.Ordinal);
            if (bold)
                bytes.AddRange([Esc, 0x45, 1]); // ESC E 1 — negrita on

            foreach (var c in text)
                bytes.Add(EncodeChar(c, table));

            if (bold)
                bytes.AddRange([Esc, 0x45, 0]); // ESC E 0 — negrita off
            bytes.Add(Lf);
        }

        var feed = Math.Clamp(o.FeedLinesBeforeCut, 0, 255);
        if (feed > 0)
            bytes.AddRange([Esc, 0x64, (byte)feed]); // ESC d n — avanza n líneas
        bytes.AddRange([Gs, 0x56, o.Cut == EscPosCut.Full ? (byte)66 : (byte)65]); // GS V m — corte

        return bytes.ToArray();
    }

    /// <summary>
    /// ¿La línea fue centrada por el builder? Misma fuente de verdad: re-centrar el
    /// texto recortado debe reproducir la línea exacta (funciona con 42, 80, etc.).
    /// </summary>
    private static bool IsCenteredLine(string line)
        => !string.IsNullOrWhiteSpace(line) && line == ReceiptContentBuilder.Center(line.Trim());

    private static byte EncodeChar(char c, string highTable)
    {
        if (c < 0x80)
            return (byte)c; // ASCII idéntico en CP437 y CP850
        var idx = highTable.IndexOf(c);
        if (idx >= 0)
            return (byte)(0x80 + idx);
        return BestFit(c);
    }

    /// <summary>Best-fit para caracteres comunes fuera de la tabla activa (equivalente al de .NET CodePages).</summary>
    private static byte BestFit(char c) => c switch
    {
        '—' or '–' => (byte)'-',
        '“' or '”' => (byte)'"',
        '‘' or '’' => (byte)'\'',
        '…' => (byte)'.',
        'Á' => (byte)'A', // ausente en CP437
        'Í' => (byte)'I',
        'Ó' => (byte)'O',
        'Ú' => (byte)'U',
        _ => (byte)'?',
    };
}
