namespace PixDeckHub;

/// <summary>Plantillas para empezar un tema en el creador sin partir de cero.</summary>
public static class ThemeTemplates
{
    public static readonly string[] Names = { "En blanco", "PIXDECK clásico", "Dos anillos", "Números gigantes" };

    const string Card = "#101422", Border = "#262E4A", Track = "#1E243A", Text = "#EBF0FF", Dim = "#7882A5",
                 Cpu = "#FF9614", Gpu = "#5AFF78", Ram = "#00CDFF", Vram = "#B964FF", Violet = "#7B007B";

    static PxWidget Rect(int x, int y, int w, int h, string c, int r = 0) => new() { Type = "rect", X = x, Y = y, W = w, H = h, Radius = r, Color = c };
    static PxWidget Line(int x, int y, int x2, int y2, string c, int width = 1) => new() { Type = "line", X = x, Y = y, X2 = x2, Y2 = y2, Width = width, Color = c };
    // Los nombres cortos (small/sans/big/huge) se convierten a fuente + escala.
    static PxWidget Txt(int x, int y, string t, string font, string align, string c)
    {
        var w = new PxWidget { Type = "text", X = x, Y = y, Text = t, Font = font, Align = align, Color = c };
        CustomTheme.NormalizeFont(w);
        return w;
    }
    static PxWidget Val(int x, int y, string src, string font, string align, string c, string suffix = "", int dec = 0, bool temp = false)
    {
        var w = new PxWidget { Type = "value", X = x, Y = y, Source = src, Font = font, Align = align, Color = c, Suffix = suffix, Decimals = dec, TempColor = temp };
        CustomTheme.NormalizeFont(w);
        return w;
    }
    static PxWidget Arc(int x, int y, int r, int th, int a0, int a1, string src, string c) =>
        new() { Type = "arc", X = x, Y = y, R = r, Thickness = th, Start = a0, End = a1, Source = src, Max = 100, Color = c, Track = Track };
    static PxWidget Bar(int x, int y, int w, int h, string src, string c) => new() { Type = "bar", X = x, Y = y, W = w, H = h, Source = src, Max = 100, Color = c, Track = Track };
    static PxWidget Graph(int x, int y, int w, int h, string src, string c) => new() { Type = "graph", X = x, Y = y, W = w, H = h, Source = src, Min = 20, Max = 100, Color = c };

    public static CustomTheme Create(int i)
    {
        var t = new CustomTheme { Name = "MI TEMA", Author = Environment.UserName, Description = "Hecho con el creador de temas", Background = "#06080E" };
        var w = t.Widgets;
        switch (i)
        {
            case 1:   // PIXDECK clasico, armado con elementos
                t.Name = "MI PIXDECK";
                w.Add(Rect(0, 0, 320, 24, "#0E101E"));
                w.Add(Line(0, 24, 320, 24, Border));
                w.Add(Txt(8, 12, "{name}", GfxFontData.Techno != null ? "techno" : "sans", "ML", Violet));
                w.Add(Txt(312, 12, "{status}", GfxFontData.Horyzen != null ? "horyzen" : "small", "MR", "#46E182"));
                foreach (var (x, label, acc, load, temp) in new[] { (4, "{cpu}", Cpu, "cpu_load", "cpu_temp"), (162, "{gpu}", Gpu, "gpu_load", "gpu_temp") })
                {
                    w.Add(Rect(x, 27, 154, 116, Border, 9));
                    w.Add(Rect(x + 1, 28, 152, 114, Card, 8));
                    w.Add(Rect(x + 9, 34, 3, 11, acc));
                    w.Add(Txt(x + 17, 33, label, "small", "TL", acc));
                    w.Add(Arc(x + 77, 93, 44, 8, 60, 300, load, acc));
                    w.Add(Val(x + 77, 89, load, "sans", "MC", Text));
                    w.Add(Txt(x + 77, 109, "%", "small", "MC", Dim));
                    w.Add(Val(x + 77, 128, temp, "sans", "MC", "#46E182", " C", 0, true));
                }
                foreach (var (x, label, acc, used, load, suffix, dec) in new[] { (4, "RAM", Ram, "ram_used", "ram_load", " GB", 1), (162, "VRAM", Vram, "vram_used", "vram_load", " MB", 0) })
                {
                    w.Add(Rect(x, 147, 154, 89, Border, 9));
                    w.Add(Rect(x + 1, 148, 152, 87, Card, 8));
                    w.Add(Rect(x + 9, 154, 3, 11, acc));
                    w.Add(Txt(x + 17, 153, label, "small", "TL", acc));
                    w.Add(Val(x + 144, 153, load, "small", "TR", Dim, " %"));
                    w.Add(Val(x + 10, 174, used, "sans", "TL", Text, suffix, dec));
                    w.Add(Bar(x + 10, 201, 134, 12, load, acc));
                }
                break;

            case 2:   // dos anillos
                t.Name = "DOS ANILLOS";
                t.Background = "#05060C";
                w.Add(Rect(0, 0, 320, 26, "#10122A"));
                w.Add(Txt(10, 13, "{name}", "sans", "ML", "#B060FF"));
                w.Add(Txt(312, 13, "{time}", "small", "MR", Text));
                foreach (var (x, label, acc, load, temp) in new[] { (80, "{cpu}", Cpu, "cpu_load", "cpu_temp"), (240, "{gpu}", Gpu, "gpu_load", "gpu_temp") })
                {
                    w.Add(Arc(x, 112, 62, 10, 45, 315, load, acc));
                    w.Add(Val(x, 104, temp, "big", "MC", "#FFFFFF", "", 0, true));
                    w.Add(Txt(x, 132, label, "glcd", "MC", acc));
                    w.Add(Val(x, 152, load, "small", "MC", Dim, "%"));
                }
                w.Add(Txt(20, 186, "RAM", "glcd", "TL", Ram));
                w.Add(Val(150, 186, "ram_used", "glcd", "TR", Text, " GB", 1));
                w.Add(Bar(20, 197, 130, 9, "ram_load", Ram));
                w.Add(Txt(170, 186, "VRAM", "glcd", "TL", Vram));
                w.Add(Val(300, 186, "vram_used", "glcd", "TR", Text, " MB"));
                w.Add(Bar(170, 197, 130, 9, "vram_load", Vram));
                w.Add(Rect(20, 214, 280, 22, "#0C0F1C", 4));
                w.Add(Graph(22, 216, 276, 18, "cpu_temp", Cpu));
                w.Add(Graph(22, 216, 276, 18, "gpu_temp", Gpu));
                break;

            case 3:   // numeros gigantes
                t.Name = "GIGANTE";
                t.Background = "#000000";
                w.Add(Txt(8, 6, "{name}", "small", "TL", Dim));
                w.Add(Txt(312, 6, "{hm}", "small", "TR", Dim));
                w.Add(Line(160, 34, 160, 170, Border));
                foreach (var (x, label, acc, load, temp) in new[] { (80, "{cpu}", Cpu, "cpu_load", "cpu_temp"), (240, "{gpu}", Gpu, "gpu_load", "gpu_temp") })
                {
                    w.Add(Txt(x, 40, label, "small", "TC", acc));
                    w.Add(Val(x, 104, temp, "huge", "MC", "#FFFFFF", "", 0, true));
                    w.Add(Val(x, 150, load, "sans", "MC", acc, "%"));
                }
                w.Add(Rect(10, 184, 300, 48, "#0A0C14", 6));
                w.Add(Graph(14, 188, 292, 40, "cpu_temp", Cpu));
                w.Add(Graph(14, 188, 292, 40, "gpu_temp", Gpu));
                break;

            default:  // en blanco
                w.Add(Rect(0, 0, 320, 24, "#0E101E"));
                w.Add(Txt(8, 12, "{name}", "sans", "ML", "#B060FF"));
                break;
        }
        return t;
    }

    /// <summary>Elemento nuevo con valores razonables, en el centro de la pantalla.</summary>
    public static PxWidget NewWidget(string type) => type switch
    {
        "rect" => Rect(110, 90, 100, 60, "#1C2236", 8),
        "circle" => new PxWidget { Type = "circle", X = 160, Y = 120, R = 20, Color = "#8B5CF6", Fill = true },
        "line" => Line(100, 120, 220, 120, "#3A4466", 2),
        "text" => Txt(160, 120, "TEXTO", "small", "MC", Text),
        "value" => Val(160, 120, "cpu_temp", "big", "MC", "#FFFFFF", " C", 0, true),
        "arc" => Arc(160, 120, 50, 8, 60, 300, "cpu_load", Cpu),
        "bar" => Bar(60, 180, 200, 12, "ram_load", Ram),
        "graph" => Graph(40, 170, 240, 50, "cpu_temp", Cpu),
        _ => throw new ArgumentException(type),
    };
}
