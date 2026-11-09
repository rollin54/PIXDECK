using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixDeckHub;

/// <summary>Un elemento de un tema .pxtheme (JSON). Coordenadas de la pantalla 320x240.</summary>
public sealed class PxWidget
{
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int R { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public int Radius { get; set; }
    public int Width { get; set; } = 1;
    public int Thickness { get; set; } = 8;
    public int Start { get; set; } = 60;
    public int End { get; set; } = 300;
    public bool Fill { get; set; } = true;
    public bool Outline { get; set; }
    public string Color { get; set; } = "#FFFFFF";
    public string Track { get; set; } = "#1E243A";
    public string Bg { get; set; } = "";
    public string Text { get; set; } = "";
    public string Font { get; set; } = "small";
    public int Size { get; set; } = 1;
    public string Align { get; set; } = "TL";
    public string Source { get; set; } = "";
    public int Decimals { get; set; }
    public string Suffix { get; set; } = "";
    public bool TempColor { get; set; }
    public float Min { get; set; }
    public float Max { get; set; } = 100;
}

/// <summary>Archivo de tema .pxtheme: lo que en el futuro generara el editor de temas.</summary>
public sealed class CustomTheme
{
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Background { get; set; } = "#06080E";
    public List<PxWidget> Widgets { get; set; } = new();

    public const int MaxWidgets = 96;

    public static readonly string[] Sources =
    {
        "cpu_temp", "cpu_load", "cpu_clock", "cpu_power", "gpu_temp", "gpu_load", "gpu_clock", "gpu_power",
        "ram_used", "ram_total", "ram_load", "vram_used", "vram_total", "extra1", "extra2", "extra3", "extra4",
        "vram_load",
    };
    // ---------------- fuentes ----------------
    // Una entrada por estilo; el tamaño se elige con "size" (escala 1..N).
    //   FreeFonts: escala 1-4 = 9/12/18/24 pt nativos; 5-7 = 18pt x2, 24pt x2, 24pt x3 (pixelado).
    //   horyzen / techno (tus fuentes), seg7 (7 segmentos, solo 0-9 : - .) y glcd (pixel) = multiplicador.
    static readonly string[] Families = { "sans", "serif", "mono" };
    static readonly string[] StyleKeys = { "regular", "bold", "italic", "bolditalic" };
    static readonly int[] Points = { 9, 12, 18, 24 };
    static readonly (int Pt, int Mult)[] FreeSteps = { (0, 1), (1, 1), (2, 1), (3, 1), (2, 2), (3, 2), (3, 3) };

    public static readonly string[] Fonts = BuildFonts();
    static string[] BuildFonts()
    {
        var l = new List<string>();
        foreach (var f in Families)
            foreach (var s in StyleKeys) l.Add($"{f}-{s}");
        if (GfxFontData.Horyzen != null) l.Add("horyzen");   // fuentes propias: solo si estan incluidas
        if (GfxFontData.Techno != null) l.Add("techno");
        l.AddRange(new[] { "seg7", "glcd" });
        return l.ToArray();
    }

    static (int Fam, int Style) ParseFree(string k)
    {
        var p = k.Split('-');
        if (p.Length != 2) return (-1, -1);
        return (Array.IndexOf(Families, p[0]), Array.IndexOf(StyleKeys, p[1]));
    }

    public static int MaxScale(string font) => CanonFont(font) switch
    {
        "horyzen" => 4, "techno" => 3, "seg7" => 2, "glcd" => 4, _ => FreeSteps.Length,
    };

    public static string CanonFont(string f) => (f ?? "").Trim().ToLowerInvariant();

    /// <summary>Codigo de fuente + multiplicador que entiende el ESP32 (ver theme_custom.h). Codigo -1 = no existe.</summary>
    public static (int Code, int Mult) Resolve(string font, int scale)
    {
        var k = CanonFont(font);
        int s = Math.Clamp(scale, 1, MaxScale(k));
        switch (k)
        {
            case "glcd": return (0, s);
            case "seg7": return (63, s);
            case "horyzen": return (70, s);
            case "techno": return (71, s);
        }
        var (fa, st) = ParseFree(k);
        if (fa < 0 || st < 0) return (-1, 1);
        var (pt, mult) = FreeSteps[s - 1];
        return (10 + fa * 16 + st * 4 + pt, mult);
    }

    public static int FontCode(string f) => Resolve(f, 1).Code;

    public static string FontLabel(string f)
    {
        var k = CanonFont(f);
        switch (k)
        {
            case "glcd": return "Pixel";
            case "seg7": return "7 segmentos (solo 0-9 : - .)";
            case "horyzen": return "Horyzen";
            case "techno": return "Techno";
        }
        var (fa, st) = ParseFree(k);
        if (fa < 0 || st < 0) return k;
        return $"{char.ToUpperInvariant(Families[fa][0])}{Families[fa][1..]}" +
               st switch { 1 => " negrita", 2 => " cursiva", 3 => " negrita cursiva", _ => "" };
    }

    /// <summary>Convierte nombres de fuente de versiones anteriores ("small", "sans18b"...) al formato fuente + escala.</summary>
    public static void NormalizeFont(PxWidget w)
    {
        var k = CanonFont(w.Font);
        int old = Math.Max(1, w.Size);
        (string Font, int Scale)? m = k switch
        {
            "small" => ("sans-bold", 1), "sans" => ("sans-bold", 2), "big" => ("sans-bold", 3), "huge" => ("sans-bold", 4),
            "mono" => ("mono-bold", 1), "serif" => ("serif-bold", 2), "font2" => ("sans-regular", 2),
            "font4" => ("sans-regular", 3), "num48" => ("sans-regular", 6), "num75" => ("sans-regular", 7),
            _ => null,
        };
        var rx = System.Text.RegularExpressions.Regex.Match(k, @"^(sans|serif|mono)(9|12|18|24)(b|i|bi)?$");
        if (rx.Success)
        {
            var style = rx.Groups[3].Value switch { "b" => "bold", "i" => "italic", "bi" => "bolditalic", _ => "regular" };
            m = ($"{rx.Groups[1].Value}-{style}", Array.IndexOf(Points, int.Parse(rx.Groups[2].Value)) + 1);
        }
        if (m is { } mm)
        {
            w.Font = mm.Font;
            w.Size = mm.Scale + (old > 1 ? 2 * (old - 1) : 0);
        }
        else w.Font = k;
        w.Size = Math.Clamp(w.Size, 1, MaxScale(w.Font));
    }
    public static readonly string[] Aligns = { "TL", "TC", "TR", "ML", "MC", "MR", "BL", "BC", "BR" };
    public static readonly string[] Types = { "rect", "circle", "line", "text", "value", "arc", "bar", "graph" };
    public static readonly string[] GraphSources = { "cpu_temp", "cpu_load", "gpu_temp", "gpu_load" };

    /// <summary>Campos que usa cada tipo (en orden de edicion). Sirve al editor y al guardar.</summary>
    public static readonly Dictionary<string, string[]> Fields = new()
    {
        ["rect"]   = new[] { "X", "Y", "W", "H", "Radius", "Color", "Outline" },
        ["circle"] = new[] { "X", "Y", "R", "Color", "Fill" },
        ["line"]   = new[] { "X", "Y", "X2", "Y2", "Width", "Color" },
        ["text"]   = new[] { "X", "Y", "Text", "Font", "Size", "Align", "Color" },
        ["value"]  = new[] { "X", "Y", "Source", "Decimals", "Suffix", "Font", "Size", "Align", "Color", "TempColor" },
        ["arc"]    = new[] { "X", "Y", "R", "Thickness", "Start", "End", "Source", "Max", "Color", "Track" },
        ["bar"]    = new[] { "X", "Y", "W", "H", "Source", "Max", "Color", "Track" },
        ["graph"]  = new[] { "X", "Y", "W", "H", "Source", "Min", "Max", "Color" },
    };

    // ---------------- guardar (solo los campos de cada tipo, JSON legible) ----------------
    public void Save(string path)
    {
        Validate();
        var props = typeof(PxWidget).GetProperties().ToDictionary(p => p.Name);
        var widgets = new List<Dictionary<string, object?>>();
        foreach (var w in Widgets)
        {
            var type = w.Type.ToLowerInvariant();
            var d = new Dictionary<string, object?> { ["type"] = type };
            foreach (var f in Fields[type])
                d[char.ToLowerInvariant(f[0]) + f[1..]] = props[f].GetValue(w);
            widgets.Add(d);
        }
        var doc = new Dictionary<string, object?>
        {
            ["name"] = Name, ["author"] = Author, ["description"] = Description,
            ["background"] = Background, ["widgets"] = widgets,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(doc, Json));
    }

    public CustomTheme Clone() => JsonSerializer.Deserialize<CustomTheme>(JsonSerializer.Serialize(this, Json), Json)!;

    public static PxWidget CloneWidget(PxWidget w) =>
        JsonSerializer.Deserialize<PxWidget>(JsonSerializer.Serialize(w, Json), Json)!;

    /// <summary>Valida y devuelve el motivo del error, o null si esta bien.</summary>
    public string? Problem()
    {
        try { Validate(); return null; } catch (InvalidDataException ex) { return ex.Message; }
    }

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // ---------------- carga y validacion ----------------
    public static CustomTheme Load(string path)
    {
        CustomTheme? t;
        try { t = JsonSerializer.Deserialize<CustomTheme>(File.ReadAllText(path), Json); }
        catch (JsonException ex) { throw new InvalidDataException($"El archivo no es un JSON válido: {ex.Message}"); }
        if (t == null) throw new InvalidDataException("El archivo está vacío.");
        foreach (var w in t.Widgets)
            if (w.Type.ToLowerInvariant() is "text" or "value") NormalizeFont(w);   // nombres de versiones anteriores
        t.Validate();
        return t;
    }

    void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Falta el nombre del tema (\"name\").");
        if (Widgets.Count == 0) throw new InvalidDataException("El tema no tiene elementos (\"widgets\").");
        if (Widgets.Count > MaxWidgets) throw new InvalidDataException($"Máximo {MaxWidgets} elementos por tema (tiene {Widgets.Count}).");
        ParseColor(Background, "background");
        for (int i = 0; i < Widgets.Count; i++)
        {
            var w = Widgets[i];
            string where = $"elemento {i + 1} ({w.Type})";
            var type = w.Type.ToLowerInvariant();
            if (!Types.Contains(type)) throw new InvalidDataException($"{where}: tipo desconocido. Usa: {string.Join(", ", Types)}.");
            ParseColor(w.Color, where);
            if (type is "arc" or "bar") ParseColor(w.Track, where);
            if (type is "value" or "arc" or "bar" or "graph" && SourceIndex(w.Source) < 0)
                throw new InvalidDataException($"{where}: \"source\" inválido. Usa: {string.Join(", ", Sources)}.");
            if (type is "text" or "value")
            {
                if (FontCode(w.Font) < 0) throw new InvalidDataException($"{where}: fuente inválida \"{w.Font}\". Usa: {string.Join(", ", Fonts)}.");
                if (Array.IndexOf(Aligns, w.Align.ToUpperInvariant()) < 0) throw new InvalidDataException($"{where}: \"align\" inválido. Usa: {string.Join(", ", Aligns)}.");
            }
            if (type == "graph" && !(w.Source is "cpu_temp" or "cpu_load" or "gpu_temp" or "gpu_load"))
                throw new InvalidDataException($"{where}: las gráficas solo admiten cpu_temp, cpu_load, gpu_temp o gpu_load.");
        }
    }

    public static int SourceIndex(string s) => Array.IndexOf(Sources, s.ToLowerInvariant());

    public static Color ParseColor(string hex, string where = "")
    {
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return System.Drawing.Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        throw new InvalidDataException($"{where}: color inválido \"{hex}\" (usa #RRGGBB).");
    }

    static int To565(string hex)
    {
        var c = ParseColor(hex);
        return ((c.R & 0xF8) << 8) | ((c.G & 0xFC) << 3) | (c.B >> 3);
    }

    // ---------------- codificacion para el ESP32 ----------------
    public (List<string> Lines, uint Hash) Encode()
    {
        var inv = CultureInfo.InvariantCulture;
        string I(float f) => ((int)MathF.Round(f)).ToString(inv);
        int bg = To565(Background);
        var lines = new List<string> { $"B,{bg}" };
        foreach (var w in Widgets)
        {
            int col = To565(w.Color);
            int wbg = string.IsNullOrWhiteSpace(w.Bg) ? bg : To565(w.Bg);
            var (font, size) = Resolve(w.Font, w.Size);   // fuente + escala => punto + multiplicador
            int align = Array.IndexOf(Aligns, w.Align.ToUpperInvariant());
            int src = SourceIndex(w.Source);
            lines.Add(w.Type.ToLowerInvariant() switch
            {
                "rect" => $"{(w.Outline ? 'O' : 'R')},{w.X},{w.Y},{w.W},{w.H},{Math.Max(0, w.Radius)},{col}",
                "circle" => $"C,{w.X},{w.Y},{Math.Max(1, w.R)},{col},{(w.Fill ? 1 : 0)}",
                "line" => $"L,{w.X},{w.Y},{w.X2},{w.Y2},{Math.Max(1, w.Width)},{col}",
                "text" => $"T,{w.X},{w.Y},{font},{size},{align},{col},{HubService.Clean(w.Text, 31)}",
                "value" => $"V,{w.X},{w.Y},{font},{size},{align},{col},{src},{Math.Clamp(w.Decimals, 0, 2)},{(w.TempColor ? 1 : 0)},{HubService.Clean(w.Suffix, 8)}",
                "arc" => $"A,{w.X},{w.Y},{Math.Max(2, w.R)},{Math.Max(1, w.Thickness)},{Math.Clamp(w.Start, 0, 360)},{Math.Clamp(w.End, 0, 360)},{src},{I(w.Max)},{col},{To565(w.Track)},{wbg}",
                "bar" => $"P,{w.X},{w.Y},{w.W},{w.H},{src},{I(w.Max)},{col},{To565(w.Track)},{wbg}",
                "graph" => $"G,{w.X},{w.Y},{w.W},{w.H},{src},{I(w.Min)},{I(w.Max)},{col}",
                _ => throw new InvalidDataException(w.Type),
            });
        }
        // FNV-1a para saber si el equipo ya tiene esta version
        uint hash = 2166136261;
        foreach (var ch in HubService.Clean(Name, 16) + "\n" + string.Join("\n", lines))
        {
            hash ^= ch;
            hash *= 16777619;
        }
        return (lines, hash);
    }

    // ---------------- vista previa (GDI, imita al firmware) ----------------
    // FreeSans ~ Arial, FreeSerif ~ Times New Roman, FreeMono ~ Courier New. Horyzen/Techno se dibujan exactas.
    static Font GdiFont(string font, int size)
    {
        var k = CanonFont(font);
        if (k == "glcd") return new Font("Consolas", 8 * Math.Clamp(size, 1, 4), FontStyle.Regular, GraphicsUnit.Pixel);
        var (fa, st) = ParseFree(k);
        if (fa < 0 || st < 0) return new Font("Arial", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        var (pt, mult) = FreeSteps[Math.Clamp(size, 1, FreeSteps.Length) - 1];
        var family = Families[fa] switch { "serif" => "Times New Roman", "mono" => "Courier New", _ => "Arial" };
        var style = st switch { 1 => FontStyle.Bold, 2 => FontStyle.Italic, 3 => FontStyle.Bold | FontStyle.Italic, _ => FontStyle.Regular };
        return new Font(family, Points[pt] * 1.85f * mult, style, GraphicsUnit.Pixel);
    }

    static GfxFont? Bitmap(string font) => CanonFont(font) switch
    {
        "horyzen" => GfxFontData.Horyzen, "techno" => GfxFontData.Techno, _ => null,
    };

    // La fuente 7 (7 segmentos) del ESP32 solo trae numeros y ": - ."; el resto no se dibuja.
    static string ForFont(string text, string font) =>
        CanonFont(font) == "seg7" ? new string(text.Where(ch => char.IsDigit(ch) || ch is ':' or '-' or '.' or ' ').ToArray()) : text;

    // ---------------- 7 segmentos (imita la fuente 7 del ESP32) ----------------
    const float SegW = 30, SegH = 46, SegT = 5;
    static readonly byte[] SegMap = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };   // gfedcba

    static float SegAdvance(char ch) => ch is ':' or '.' ? 12 : 32;

    static SizeF MeasureText(Graphics g, string text, string font, int size)
    {
        int s = Math.Clamp(size, 1, MaxScale(font));
        if (CanonFont(font) == "seg7")
            return new SizeF(Math.Max(4, text.Sum(SegAdvance) * s), SegH * s);
        if (Bitmap(font) is { } bf)
            return new SizeF(Math.Max(4, bf.Width(text, s)), bf.Height(s));
        using var f = GdiFont(font, size);
        return g.MeasureString(text.Length == 0 ? " " : text, f, PointF.Empty, StringFormat.GenericTypographic);
    }

    static void DrawSeg7(Graphics g, string text, Color c, float x, float y, float s)
    {
        using var b = new SolidBrush(c);
        foreach (var ch in text)
        {
            if (char.IsDigit(ch) || ch == '-')
            {
                int m = ch == '-' ? 0x40 : SegMap[ch - '0'];
                float w = SegW * s, h = SegH * s, t = SegT * s, half = h / 2;
                void Seg(int bit, float sx, float sy, float sw, float sh) { if ((m & (1 << bit)) != 0) g.FillRectangle(b, x + sx, y + sy, sw, sh); }
                Seg(0, t, 0, w - 2 * t, t);                     // a
                Seg(1, w - t, t, t, half - t * 1.5f);           // b
                Seg(2, w - t, half + t / 2, t, half - t * 1.5f);// c
                Seg(3, t, h - t, w - 2 * t, t);                 // d
                Seg(4, 0, half + t / 2, t, half - t * 1.5f);    // e
                Seg(5, 0, t, t, half - t * 1.5f);               // f
                Seg(6, t, half - t / 2, w - 2 * t, t);          // g
            }
            else if (ch == ':')
            {
                g.FillRectangle(b, x + 3 * s, y + 14 * s, 5 * s, 5 * s);
                g.FillRectangle(b, x + 3 * s, y + 30 * s, 5 * s, 5 * s);
            }
            else if (ch == '.') g.FillRectangle(b, x + 3 * s, y + (SegH - 5) * s, 5 * s, 5 * s);
            x += SegAdvance(ch) * s;
        }
    }

    static PointF AlignOffset(SizeF sz, string align)
    {
        var a = align.ToUpperInvariant();
        float dx = a[1] == 'L' ? 0 : a[1] == 'C' ? -sz.Width / 2 : -sz.Width;
        float dy = a[0] == 'T' ? 0 : a[0] == 'M' ? -sz.Height / 2 : -sz.Height;
        return new PointF(dx, dy);
    }

    static void DrawAligned(Graphics g, string text, string font, int size, Color c, float x, float y, string align)
    {
        text = ForFont(text, font);
        int s = Math.Clamp(size, 1, MaxScale(font));
        var sz = MeasureText(g, text, font, size);
        var o = AlignOffset(sz, align);
        if (CanonFont(font) == "seg7") { DrawSeg7(g, text, c, x + o.X, y + o.Y, s); return; }
        using var b = new SolidBrush(c);
        if (Bitmap(font) is { } bf) { bf.Draw(g, text, b, x + o.X, y + o.Y, s); return; }
        using var f = GdiFont(font, size);
        g.DrawString(text, f, b, x + o.X, y + o.Y, StringFormat.GenericTypographic);
    }

    static float? SourceValue(string src, Func<int, float?> value)
    {
        int i = SourceIndex(src);
        if (i == 17) return value(11) is float u && value(12) is float tot && tot > 0 ? u * 100 / tot : null;
        return i >= 0 ? value(i) : null;
    }

    /// <summary>Texto que muestra un elemento "text" o "value" (igual que en el equipo).</summary>
    public static string TextOf(PxWidget w, Func<int, float?> value, string name, string cpu, string gpu)
    {
        if (w.Type.Equals("text", StringComparison.OrdinalIgnoreCase))
            return HubService.Clean(w.Text.Replace("{name}", name).Replace("{cpu}", cpu).Replace("{gpu}", gpu)
                .Replace("{time}", DateTime.Now.ToString("HH:mm:ss")).Replace("{hm}", DateTime.Now.ToString("HH:mm"))
                .Replace("{status}", "ONLINE"), 31);
        var v = SourceValue(w.Source, value);
        return (v is float f ? f.ToString("F" + Math.Clamp(w.Decimals, 0, 2), CultureInfo.InvariantCulture) : "--")
               + HubService.Clean(w.Suffix, 8);
    }

    /// <summary>Rectangulo que ocupa un elemento (para seleccionarlo con el mouse en el creador).</summary>
    public static RectangleF Bounds(PxWidget w, Graphics g, Func<int, float?> value, string name, string cpu, string gpu)
    {
        switch (w.Type.ToLowerInvariant())
        {
            case "rect": case "bar": case "graph":
                return new RectangleF(w.X, w.Y, Math.Max(2, w.W), Math.Max(2, w.H));
            case "circle": case "arc":
                return new RectangleF(w.X - w.R, w.Y - w.R, 2 * w.R, 2 * w.R);
            case "line":
                var r = RectangleF.FromLTRB(Math.Min(w.X, w.X2), Math.Min(w.Y, w.Y2), Math.Max(w.X, w.X2), Math.Max(w.Y, w.Y2));
                r.Inflate(3 + w.Width / 2f, 3 + w.Width / 2f);
                return r;
            default:
            {
                var s = ForFont(TextOf(w, value, name, cpu, gpu), w.Font);
                var sz = MeasureText(g, s, w.Font, w.Size);
                var o = AlignOffset(sz, w.Align);
                return new RectangleF(w.X + o.X, w.Y + o.Y, Math.Max(4, sz.Width), Math.Max(4, sz.Height));
            }
        }
    }

    public void Render(Graphics g, Func<int, float?> value, string name, string cpu, string gpu, float t, int warn, int alert)
    {
        using (var bg = new SolidBrush(ParseColor(Background))) g.FillRectangle(bg, 0, 0, 320, 240);
        float? Val(string src)
        {
            int i = SourceIndex(src);
            if (i == 17) return value(11) is float u && value(12) is float tot && tot > 0 ? u * 100 / tot : null;
            return i >= 0 ? value(i) : null;
        }
        Color TempCol(float? v, Color ok) => v is float f ? f >= alert ? Ui.Hot : f >= warn ? Ui.Warn : ok : Ui.Dim;
        var inv = CultureInfo.InvariantCulture;

        foreach (var w in Widgets)
        {
            var col = ParseColor(w.Color);
            switch (w.Type.ToLowerInvariant())
            {
                case "rect":
                    using (var path = w.Radius > 0 ? Ui.Round(new RectangleF(w.X, w.Y, w.W, w.H), w.Radius) : null)
                    {
                        if (w.Outline)
                        {
                            using var p = new Pen(col);
                            if (path != null) g.DrawPath(p, path); else g.DrawRectangle(p, w.X, w.Y, w.W - 1, w.H - 1);
                        }
                        else
                        {
                            using var b = new SolidBrush(col);
                            if (path != null) g.FillPath(b, path); else g.FillRectangle(b, w.X, w.Y, w.W, w.H);
                        }
                    }
                    break;
                case "circle":
                    if (w.Fill) { using var b = new SolidBrush(col); g.FillEllipse(b, w.X - w.R, w.Y - w.R, 2 * w.R, 2 * w.R); }
                    else { using var p = new Pen(col); g.DrawEllipse(p, w.X - w.R, w.Y - w.R, 2 * w.R, 2 * w.R); }
                    break;
                case "line":
                    using (var p = new Pen(col, Math.Max(1, w.Width))) g.DrawLine(p, w.X, w.Y, w.X2, w.Y2);
                    break;
                case "text":
                    DrawAligned(g, TextOf(w, value, name, cpu, gpu), w.Font, w.Size, col, w.X, w.Y, w.Align);
                    break;
                case "value":
                {
                    var v = Val(w.Source);
                    string s = TextOf(w, value, name, cpu, gpu);
                    DrawAligned(g, s, w.Font, w.Size, w.TempColor ? TempCol(v, col) : col, w.X, w.Y, w.Align);
                    break;
                }
                case "arc":
                {
                    float th = Math.Max(1, w.Thickness), rr = w.R - th / 2;
                    var rect = new RectangleF(w.X - rr, w.Y - rr, 2 * rr, 2 * rr);
                    float a0 = Math.Clamp(w.Start, 0, 360), a1 = Math.Clamp(w.End, 0, 360);
                    using (var tp = new Pen(ParseColor(w.Track), th) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(tp, rect, 90 + a0, a1 - a0);
                    float p = w.Max > 0 && Val(w.Source) is float vv ? Math.Clamp(vv * 100 / w.Max, 0, 100) : 0;
                    if (p > 1)
                    {
                        using var vp = new Pen(col, th) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        g.DrawArc(vp, rect, 90 + a0, (a1 - a0) * p / 100);
                    }
                    break;
                }
                case "bar":
                {
                    float p = w.Max > 0 && Val(w.Source) is float vv ? Math.Clamp(vv * 100 / w.Max, 0, 100) : 0;
                    float rad = w.H / 2f;
                    using (var tb = new SolidBrush(ParseColor(w.Track)))
                    using (var tpth = Ui.Round(new RectangleF(w.X, w.Y, w.W, w.H), Math.Max(0.5f, rad))) g.FillPath(tb, tpth);
                    float fw = w.W * p / 100;
                    if (fw > 0)
                    {
                        fw = Math.Max(fw, w.H);
                        using var fb = new SolidBrush(col);
                        using var fp = Ui.Round(new RectangleF(w.X, w.Y, fw, w.H), Math.Max(0.5f, rad));
                        g.FillPath(fb, fp);
                    }
                    break;
                }
                case "graph":
                {
                    var pts = new PointF[48];
                    float seed = SourceIndex(w.Source);
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float v = 0.5f + 0.3f * MathF.Sin(i * 0.35f + seed + t) + 0.12f * MathF.Sin(i * 1.1f + seed);
                        pts[i] = new PointF(w.X + i * w.W / (pts.Length - 1f), w.Y + w.H - v * w.H);
                    }
                    using var p = new Pen(col, 2);
                    g.DrawLines(p, pts);
                    break;
                }
            }
        }
    }
}
