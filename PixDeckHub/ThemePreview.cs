using System.Drawing.Drawing2D;

namespace PixDeckHub;

/// <summary>
/// Vistas previas aproximadas de los temas del ESP32, dibujadas en un lienzo de 320x240
/// (mismo tamaño que la pantalla ILI9341) y escaladas a la tarjeta.
/// </summary>
public static class ThemePreview
{
    public sealed record Sample(float CpuT, float GpuT, float CpuL, float GpuL, float RamL, float VramL,
                                string Name, string CpuLabel, string GpuLabel, float?[]? Values = null);

    /// <summary>Lo asigna HubService: devuelve el tema subido para un indice >= 8.</summary>
    public static Func<int, CustomTheme?>? CustomResolver { get; set; }
    public static int Warn { get; set; } = 60;
    public static int Alert { get; set; } = 70;

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public static void Draw(Graphics g, Rectangle dest, int theme, Sample s, float t)
    {
        var state = g.Save();
        g.SetClip(dest);
        g.TranslateTransform(dest.X, dest.Y);
        g.ScaleTransform(dest.Width / 320f, dest.Height / 240f);
        Ui.Hq(g);
        switch (theme)
        {
            case 0: Electronica(g, s, t); break;
            case 1: Informatica(g, s, t); break;
            case 2: Autos(g, s, t); break;
            case 3: Anime(g, s, t); break;
            case 4: Peliculas(g, s, t); break;
            case 5: PixClassic(g, s, t); break;
            case 6: PixRings(g, s, t); break;
            case 7: PixGraph(g, s, t); break;
            default: Custom(g, theme, s, t); break;
        }
        g.Restore(state);
    }

    static float? Def(Sample s, int i) => i switch
    {
        0 => s.CpuT, 1 => s.CpuL, 2 => 4850, 3 => 88, 4 => s.GpuT, 5 => s.GpuL, 6 => 1905, 7 => 210,
        8 => 32 * s.RamL / 100, 9 => 32, 10 => s.RamL, 11 => 8192 * s.VramL / 100, 12 => 8192,
        13 => 1200, 14 => 45, 15 => 3.3f, 16 => 0, _ => null,
    };

    static void Custom(Graphics g, int theme, Sample s, float t)
    {
        var ct = CustomResolver?.Invoke(theme);
        if (ct == null)
        {
            Fill(g, C(6, 8, 14), 0, 0, 320, 240);
            StrC(g, "TEMA NO ENCONTRADO", 14, Ui.Dim, 160, 110);
            return;
        }
        ct.Render(g, i => s.Values != null && i < s.Values.Length && s.Values[i] is float v ? v : Def(s, i),
                  s.Name, s.CpuLabel, s.GpuLabel, t, Warn, Alert);
    }

    // ---------------- familia PIXDECK ----------------
    static readonly Color PxCard = Color.FromArgb(16, 20, 34), PxBorder = Color.FromArgb(38, 46, 74),
        PxTrack = Color.FromArgb(30, 36, 58), PxCpu = Color.FromArgb(255, 150, 20), PxGpu = Color.FromArgb(90, 255, 120),
        PxRam = Color.FromArgb(0, 205, 255), PxVram = Color.FromArgb(185, 100, 255), PxViolet = Color.FromArgb(123, 0, 123),
        PxBg = Color.FromArgb(6, 8, 14);

    static void PxHeader(Graphics g, Sample s, bool clock)
    {
        Fill(g, PxBg, 0, 0, 320, 240);
        Fill(g, C(14, 16, 30), 0, 0, 320, 24);
        Fill(g, PxBorder, 0, 24, 320, 1);
        Str(g, s.Name, 19, PxViolet, 6, -1, FontStyle.Bold, "Arial");
        string txt = clock ? DateTime.Now.ToString("HH:mm:ss") : "ONLINE";
        StrR(g, txt, 11, clock ? C(235, 240, 255) : Ui.Ok, 312, 5);
        using var dot = new SolidBrush(Ui.Ok);
        g.FillEllipse(dot, clock ? 246 : 252, 8, 8, 8);
    }

    static void PxCardBase(Graphics g, float x, float y, float w, float h, string title, Color acc)
    {
        RFill(g, PxBorder, x, y, w, h, 9);
        RFill(g, PxCard, x + 1, y + 1, w - 2, h - 2, 8);
        Fill(g, acc, x + 9, y + 7, 3, 11);
        Str(g, title.Length > 10 ? title[..10] : title, 11, acc, x + 15, y + 4);
    }

    static void ArcP(Graphics g, float cx, float cy, float r, float th, float a0, float a1, float p, Color track, Color col)
    {
        float rr = r - th / 2;
        var rect = new RectangleF(cx - rr, cy - rr, 2 * rr, 2 * rr);
        using (var tp = new Pen(track, th) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(tp, rect, 90 + a0, a1 - a0);
        if (p > 1) { using var vp = new Pen(col, th) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawArc(vp, rect, 90 + a0, (a1 - a0) * Math.Clamp(p, 0, 100) / 100); }
    }

    static void PxBar(Graphics g, float x, float y, float w, float h, float p, Color col)
    {
        RFill(g, PxTrack, x, y, w, h, h / 2);
        RFill(g, p >= 90 ? Ui.Hot : col, x, y, Math.Max(h, w * Math.Clamp(p, 0, 100) / 100), h, h / 2);
    }

    static void PixClassic(Graphics g, Sample s, float t)
    {
        PxHeader(g, s, false);
        void Gauge(float x, string title, Color acc, float load, float temp, string tr)
        {
            PxCardBase(g, x, 27, 154, 116, title, acc);
            if (tr != "") StrR(g, tr, 10, Ui.Dim, x + 144, 31);
            ArcP(g, x + 77, 93, 44, 8, 60, 300, load, PxTrack, load >= 90 ? Ui.Hot : acc);
            StrC(g, $"{load:0}", 22, C(235, 240, 255), x + 77, 76);
            StrC(g, "%", 10, Ui.Dim, x + 77, 101);
            StrC(g, $"{temp:0} C", 20, TempCol(temp, Ui.Ok), x + 77, 114);
        }
        Gauge(4, s.CpuLabel, PxCpu, s.CpuL, s.CpuT, "4.9 GHz");
        Gauge(162, s.GpuLabel, PxGpu, s.GpuL, s.GpuT, "");
        void BarC(float x, string title, Color acc, float p, string val, string unit, string cap)
        {
            PxCardBase(g, x, 147, 154, 89, title, acc);
            StrR(g, $"{p:0} %", 10, Ui.Dim, x + 144, 151);
            Str(g, val, 22, C(235, 240, 255), x + 8, 166);
            Str(g, unit, 10, Ui.Dim, x + 12 + val.Length * 13, 177);
            PxBar(g, x + 10, 201, 134, 12, p, acc);
            Str(g, cap, 10, Ui.Dim, x + 10, 217);
        }
        BarC(4, "RAM", PxRam, s.RamL, (32 * s.RamL / 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "GB", "TOTAL 32 GB");
        BarC(162, "VRAM", PxVram, s.VramL, (8192 * s.VramL / 100).ToString("0"), "MB", "TOTAL 8192 MB");
    }

    static void PixRings(Graphics g, Sample s, float t)
    {
        PxHeader(g, s, false);
        void Ring(float x, string title, Color acc, float load, float temp, string tr, string watts)
        {
            PxCardBase(g, x, 27, 154, 160, title, acc);
            StrR(g, tr, 9, Ui.Dim, x + 144, 32, FontStyle.Regular, "Consolas");
            float cx = x + 77, cy = 119;
            ArcP(g, cx, cy, 62, 8, 45, 315, load, PxTrack, load >= 90 ? Ui.Hot : acc);
            ArcP(g, cx, cy, 49, 4, 45, 315, temp, C(24, 28, 46), TempCol(temp, Ui.Ok));
            StrC(g, $"{temp:0}°", 30, TempCol(temp, C(235, 240, 255)), cx, cy - 30, FontStyle.Bold, "Arial");
            StrC(g, $"{load:0}%", 11, acc, cx, cy + 9);
            StrC(g, "USO", 8, Ui.Dim, cx, cy + 25, FontStyle.Regular, "Consolas");
            StrC(g, watts, 10, Ui.Dim, cx, cy + 50);
        }
        Ring(4, s.CpuLabel, PxCpu, s.CpuL, s.CpuT, "4.85 GHz", "88 W");
        Ring(162, s.GpuLabel, PxGpu, s.GpuL, s.GpuT, "1905 MHz", "210 W");
        RFill(g, PxBorder, 4, 191, 312, 46, 9);
        RFill(g, PxCard, 5, 192, 310, 44, 8);
        Fill(g, PxBorder, 160, 198, 1, 32);
        Str(g, "RAM", 10, PxRam, 14, 195); StrR(g, "12.3/32G", 10, C(235, 240, 255), 152, 195);
        PxBar(g, 14, 217, 138, 9, s.RamL, PxRam);
        Str(g, "VRAM", 10, PxVram, 170, 195); StrR(g, "2.1/8G", 10, C(235, 240, 255), 306, 195);
        PxBar(g, 170, 217, 136, 9, s.VramL, PxVram);
    }

    static void PixGraph(Graphics g, Sample s, float t)
    {
        PxHeader(g, s, true);
        void GCard(float x, string title, Color acc, Color dark, float temp, float load, string tr, float seed)
        {
            PxCardBase(g, x, 27, 154, 116, title, acc);
            StrR(g, tr, 9, Ui.Dim, x + 144, 32, FontStyle.Regular, "Consolas");
            Str(g, $"{temp:0}°C", 24, TempCol(temp, C(235, 240, 255)), x + 8, 46, FontStyle.Bold, "Arial");
            StrR(g, $"{load:0}%", 11, C(235, 240, 255), x + 144, 56);
            StrR(g, "USO", 8, Ui.Dim, x + 144, 71, FontStyle.Regular, "Consolas");
            Fill(g, C(12, 15, 26), x + 8, 89, 138, 46);
            var pts = new List<PointF>();
            for (int i = 0; i < 40; i++)
            {
                float v = 0.5f + 0.28f * MathF.Sin(i * 0.3f + seed + t) + 0.1f * MathF.Sin(i * 1.2f + seed);
                pts.Add(new PointF(x + 8 + i * 138 / 39f, 89 + 46 - v * 46));
            }
            var poly = new List<PointF>(pts) { new(x + 146, 135), new(x + 8, 135) };
            using (var fb = new SolidBrush(dark)) g.FillPolygon(fb, poly.ToArray());
            using (var lp = new Pen(acc, 2)) g.DrawLines(lp, pts.ToArray());
        }
        GCard(4, s.CpuLabel, PxCpu, C(80, 46, 8), s.CpuT, s.CpuL, "4.85GHz", 0);
        GCard(162, s.GpuLabel, PxGpu, C(24, 76, 38), s.GpuT, s.GpuL, "1905MHz", 2);
        void Tile(float x, string title, Color acc, float p, string center, string cap)
        {
            RFill(g, PxBorder, x, 147, 75, 89, 8);
            RFill(g, PxCard, x + 1, 148, 73, 87, 7);
            Str(g, title, 10, acc, x + 7, 150);
            ArcP(g, x + 37, 194, 22, 5, 45, 315, p, PxTrack, acc);
            StrC(g, center, 10, C(235, 240, 255), x + 37, 187);
            StrC(g, cap, 8, Ui.Dim, x + 37, 221, FontStyle.Regular, "Consolas");
        }
        Tile(4, "RAM", PxRam, s.RamL, $"{s.RamL:0}%", "12.3/32G");
        Tile(83, "VRAM", PxVram, s.VramL, $"{s.VramL:0}%", "2.1/8G");
        Tile(162, "CPU PWR", PxCpu, 0, "88", "W");
        Tile(241, "GPU PWR", PxGpu, 0, "210", "W");
    }

    static void Fill(Graphics g, Color c, float x, float y, float w, float h)
    { using var b = new SolidBrush(c); g.FillRectangle(b, x, y, w, h); }
    static void RFill(Graphics g, Color c, float x, float y, float w, float h, float r)
    { using var b = new SolidBrush(c); using var p = Ui.Round(new RectangleF(x, y, w, h), r); g.FillPath(b, p); }
    static void Str(Graphics g, string s, float size, Color c, float x, float y, FontStyle st = FontStyle.Bold, string fam = "Segoe UI")
    { using var f = new Font(fam, size, st, GraphicsUnit.Pixel); using var b = new SolidBrush(c); g.DrawString(s, f, b, x, y); }
    static void StrR(Graphics g, string s, float size, Color c, float right, float y, FontStyle st = FontStyle.Bold, string fam = "Segoe UI")
    {
        using var f = new Font(fam, size, st, GraphicsUnit.Pixel);
        var w = g.MeasureString(s, f).Width;
        using var b = new SolidBrush(c); g.DrawString(s, f, b, right - w, y);
    }
    static void StrC(Graphics g, string s, float size, Color c, float cx, float y, FontStyle st = FontStyle.Bold, string fam = "Segoe UI")
    {
        using var f = new Font(fam, size, st, GraphicsUnit.Pixel);
        var w = g.MeasureString(s, f).Width;
        using var b = new SolidBrush(c); g.DrawString(s, f, b, cx - w / 2, y);
    }
    static Color TempCol(float t, Color ok) => t >= 70 ? Ui.Hot : t >= 60 ? Ui.Warn : ok;

    static PointF[] Wave(float x, float y, float w, float h, float seed, float t)
    {
        var pts = new PointF[40];
        for (int i = 0; i < pts.Length; i++)
        {
            float v = 0.5f + 0.32f * MathF.Sin(i * 0.45f + seed + t) + 0.12f * MathF.Sin(i * 1.3f + seed * 2);
            pts[i] = new PointF(x + i * w / (pts.Length - 1), y + h - v * h);
        }
        return pts;
    }

    // ---------------- ELECTRONICA ----------------
    static void Electronica(Graphics g, Sample s, float t)
    {
        Fill(g, C(8, 44, 26), 0, 0, 320, 240);
        Fill(g, C(4, 28, 16), 0, 0, 320, 20);
        Str(g, s.Name, 11, C(225, 235, 225), 8, 3);
        StrR(g, "PWR ●  21:45:03", 10, C(225, 235, 225), 312, 4);
        void Chip(float x, string label, float temp, float load, Color acc)
        {
            for (int i = 0; i < 7; i++) { Fill(g, C(205, 165, 70), x - 6, 40 + i * 12, 6, 4); Fill(g, C(205, 165, 70), x + 122, 40 + i * 12, 6, 4); }
            for (int i = 0; i < 8; i++) { Fill(g, C(205, 165, 70), x + 10 + i * 14, 25, 4, 5); Fill(g, C(205, 165, 70), x + 10 + i * 14, 122, 4, 5); }
            RFill(g, C(20, 20, 24), x, 30, 122, 92, 4);
            Str(g, label, 11, acc, x + 16, 33);
            StrR(g, $"{temp:0}°C", 26, TempCol(temp, C(225, 235, 225)), x + 116, 48);
            Str(g, $"LOAD {load:0}%", 10, C(170, 170, 180), x + 8, 84);
            int on = (int)(load / 10);
            for (int i = 0; i < 10; i++)
            {
                var c = i < 6 ? (i < on ? C(60, 255, 90) : C(14, 56, 24)) : i < 8 ? (i < on ? C(255, 170, 0) : C(64, 44, 6)) : (i < on ? C(255, 45, 45) : C(64, 12, 12));
                using var b = new SolidBrush(c); g.FillEllipse(b, x + 9 + i * 11, 108, 6, 6);
            }
        }
        Chip(22, s.CpuLabel, s.CpuT, s.CpuL, C(255, 225, 40));
        Chip(176, s.GpuLabel, s.GpuT, s.GpuL, C(40, 220, 255));
        RFill(g, C(48, 52, 58), 6, 132, 196, 74, 5);
        Fill(g, C(4, 18, 12), 10, 136, 188, 66);
        using (var p1 = new Pen(C(255, 225, 40), 2)) g.DrawLines(p1, Wave(12, 140, 184, 58, 0, t));
        using (var p2 = new Pen(C(40, 220, 255), 2)) g.DrawLines(p2, Wave(12, 140, 184, 58, 2, t * 1.3f));
        RFill(g, C(235, 185, 20), 208, 132, 106, 74, 8);
        RFill(g, C(150, 172, 140), 214, 138, 94, 56, 3);
        Str(g, "CPU PWR  88W", 10, C(20, 34, 22), 218, 144, FontStyle.Bold, "Consolas");
        Str(g, "GPU PWR 210W", 10, C(20, 34, 22), 218, 160, FontStyle.Bold, "Consolas");
        void Led(float x, string l, float p)
        {
            Str(g, l, 10, C(225, 235, 225), x, 208);
            int on = (int)(p * 11 / 100);
            for (int i = 0; i < 11; i++)
                Fill(g, i < 7 ? (i < on ? C(60, 255, 90) : C(14, 56, 24)) : i < 9 ? (i < on ? C(255, 170, 0) : C(64, 44, 6)) : (i < on ? C(255, 45, 45) : C(64, 12, 12)), x + i * 14, 224, 11, 9);
        }
        Led(8, "RAM", s.RamL);
        Led(164, "VRAM", s.VramL);
    }

    // ---------------- INFORMATICA ----------------
    static void Informatica(Graphics g, Sample s, float t)
    {
        Fill(g, C(6, 9, 10), 0, 0, 320, 240);
        using (var scan = new Pen(C(11, 16, 16))) for (int y = 0; y < 240; y += 3) g.DrawLine(scan, 0, y, 320, y);
        Fill(g, C(32, 36, 44), 0, 0, 320, 18);
        foreach (var (x, c) in new[] { (6, C(255, 70, 70)), (19, C(255, 180, 40)), (32, C(80, 255, 120)) })
        { using var b = new SolidBrush(c); g.FillEllipse(b, x, 5, 8, 8); }
        StrC(g, $"root@{s.Name}: ~ (top)", 10, C(205, 220, 210), 170, 3, FontStyle.Regular, "Consolas");
        var green = C(80, 255, 120);
        void Meter(float y, string n, float p, string val)
        {
            Str(g, n, 15, C(90, 210, 255), 6, y, FontStyle.Bold, "Consolas");
            int k = (int)(p * 12 / 100);
            var bar = new string('|', k).PadRight(12, '.');
            Str(g, "[" + bar + "]", 15, green, 38, y, FontStyle.Bold, "Consolas");
            StrR(g, val, 15, C(205, 220, 210), 314, y, FontStyle.Bold, "Consolas");
        }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Meter(22, "CPU", s.CpuL, s.CpuL.ToString("0.0", inv) + "%");
        Meter(42, "GPU", s.GpuL, s.GpuL.ToString("0.0", inv) + "%");
        Meter(62, "MEM", s.RamL, "12.3/32G");
        Meter(82, "VRM", s.VramL, "2.1/8G");
        Str(g, "TEMP", 15, C(255, 180, 40), 6, 118, FontStyle.Bold, "Consolas");
        Str(g, $"{s.CpuT:0}C", 15, TempCol(s.CpuT, green), 100, 118, FontStyle.Bold, "Consolas");
        Str(g, $"{s.GpuT:0}C", 15, TempCol(s.GpuT, green), 214, 118, FontStyle.Bold, "Consolas");
        Str(g, "CLK", 15, C(255, 180, 40), 6, 138, FontStyle.Bold, "Consolas");
        Str(g, "4.85G      1905M", 15, green, 100, 138, FontStyle.Bold, "Consolas");
        Str(g, "PWR", 15, C(255, 180, 40), 6, 158, FontStyle.Bold, "Consolas");
        Str(g, "88W        210W", 15, green, 100, 158, FontStyle.Bold, "Consolas");
        using (var p = new Pen(C(255, 180, 40), 1.5f)) g.DrawLines(p, Wave(8, 182, 304, 30, 1, t));
        Str(g, s.Name.ToLowerInvariant() + ":~$", 15, green, 6, 218, FontStyle.Bold, "Consolas");
        if ((int)(t * 2) % 2 == 0) Fill(g, green, 130, 222, 9, 14);
        StrR(g, "21:45:03", 15, C(36, 110, 58), 314, 218, FontStyle.Bold, "Consolas");
    }

    // ---------------- AUTOS ----------------
    static PointF P(float cx, float cy, float r, float a) =>
        new(cx - MathF.Sin(a * MathF.PI / 180) * r, cy + MathF.Cos(a * MathF.PI / 180) * r);

    static void Gauge(Graphics g, float cx, float cy, float R, float p, float temp, string label, Color glow)
    {
        using (var face = new SolidBrush(C(16, 16, 22))) g.FillEllipse(face, cx - R, cy - R, 2 * R, 2 * R);
        using (var ring = new Pen(C(120, 124, 134), 3)) g.DrawEllipse(ring, cx - R + 1.5f, cy - R + 1.5f, 2 * R - 3, 2 * R - 3);
        // GDI: angulo 0 = derecha, horario. Convertimos desde "0 = abajo".
        float GdiA(float a) => 90 + a;
        using (var red = new Pen(C(255, 40, 30), 5)) g.DrawArc(red, cx - R + 9, cy - R + 9, 2 * R - 18, 2 * R - 18, GdiA(50 + 260 * 0.85f), 260 * 0.15f);
        using (var gl = new Pen(glow, 2)) g.DrawArc(gl, cx - R + 5, cy - R + 5, 2 * R - 10, 2 * R - 10, GdiA(50), 260 * p / 100);
        for (int i = 0; i <= 20; i++)
        {
            float a = 50 + 260 * i / 20f;
            bool major = i % 2 == 0;
            using var pen = new Pen(major ? C(230, 230, 236) : C(110, 110, 120), major ? 2 : 1);
            g.DrawLine(pen, P(cx, cy, R - 7, a), P(cx, cy, major ? R - 17 : R - 12, a));
            if (major)
            {
                var q = P(cx, cy, R - 27, a);
                StrC(g, (i / 2).ToString(), 9, i >= 17 ? C(255, 40, 30) : C(230, 230, 236), q.X, q.Y - 6, FontStyle.Regular);
            }
        }
        StrC(g, label, label.Length > 6 ? 8 : 11, C(120, 205, 255), cx, cy - 32);
        StrC(g, $"{temp:0}°", 24, TempCol(temp, C(230, 230, 236)), cx, cy + 30);
        float na = 50 + 260 * p / 100;
        using (var needle = new Pen(C(255, 70, 20), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(needle, P(cx, cy, -10, na), P(cx, cy, R - 12, na));
        using (var hub = new SolidBrush(C(40, 40, 46))) g.FillEllipse(hub, cx - 8, cy - 8, 16, 16);
        using (var hub2 = new SolidBrush(C(255, 70, 20))) g.FillEllipse(hub2, cx - 4, cy - 4, 8, 8);
    }

    static void Autos(Graphics g, Sample s, float t)
    {
        Fill(g, C(6, 6, 9), 0, 0, 320, 240);
        foreach (var (x, c, on) in new[] { (5, C(255, 160, 0), true), (27, C(255, 40, 30), false), (49, C(60, 230, 90), true) })
        { using var b = new SolidBrush(on ? c : C(40, 40, 46)); g.FillEllipse(b, x, 3, 18, 18); }
        RFill(g, C(14, 14, 20), 76, 2, 166, 20, 4);
        StrC(g, "GPU PWR  210 W", 11, C(255, 160, 0), 159, 5);
        StrR(g, "21:45", 11, C(230, 230, 236), 314, 5);
        float wob = MathF.Sin(t * 2) * 3;
        Gauge(g, 82, 104, 72, s.CpuL + wob, s.CpuT, s.CpuLabel, C(120, 205, 255));
        Gauge(g, 238, 104, 72, s.GpuL - wob, s.GpuT, s.GpuLabel, C(255, 120, 40));
        void Fuel(float x, string l, float p, Color c)
        {
            RFill(g, C(14, 14, 20), x, 184, 112, 52, 6);
            Str(g, l, 10, C(120, 205, 255), x + 8, 188);
            int on = (int)(p * 8 / 100);
            for (int i = 0; i < 8; i++) Fill(g, i < on ? (i >= 7 ? C(255, 40, 30) : c) : C(40, 40, 46), x + 18 + i * 10, 210 - i, 8, 14 + i);
        }
        Fuel(4, "RAM", s.RamL, C(60, 230, 90));
        Fuel(204, "VRAM", s.VramL, C(120, 205, 255));
        RFill(g, C(14, 14, 20), 122, 184, 76, 52, 6);
        StrC(g, "GEAR", 8, C(110, 110, 120), 160, 187, FontStyle.Regular);
        StrC(g, (1 + Math.Clamp((int)(s.CpuL / 20), 0, 5)).ToString(), 26, C(230, 230, 236), 160, 196);
    }

    // ---------------- ANIME ----------------
    static void Para(Graphics g, Color c, float x, float y, float w, float h, float sk)
    {
        using var b = new SolidBrush(c);
        g.FillPolygon(b, new[] { new PointF(x + sk, y), new PointF(x + w + sk, y), new PointF(x + w, y + h), new PointF(x, y + h) });
    }

    static void Anime(Graphics g, Sample s, float t)
    {
        Fill(g, C(14, 10, 34), 0, 0, 320, 240);
        float off = (t * 25) % 24;
        using (var st = new Pen(C(22, 16, 50), 2))
            for (float x = -240 + off; x < 340; x += 24) g.DrawLine(st, x, 0, x + 120, 240);
        Para(g, C(255, 40, 140), -12, 4, 236, 30, 14);
        Str(g, s.Name, 22, Color.White, 12, 4, FontStyle.Bold, "Arial");
        Para(g, C(255, 230, 0), 230, 8, 82, 22, 8);
        StrC(g, s.CpuL >= 80 ? "TURBO!" : "NORMAL", 11, C(14, 10, 34), 274, 11);
        void Panel(float x, string label, float temp, float load, Color acc)
        {
            Para(g, C(30, 22, 64), x, 42, 140, 112, 10);
            using (var p = new Pen(acc, 2))
                g.DrawPolygon(p, new[] { new PointF(x + 10, 42), new PointF(x + 150, 42), new PointF(x + 140, 154), new PointF(x, 154) });
            Para(g, acc, x + 12, 48, 70, 14, 4);
            Str(g, label, 10, C(14, 10, 34), x + 18, 48);
            StrC(g, $"{temp:0}", 46, TempCol(temp, Color.White), x + 70, 62, FontStyle.Bold, "Arial");
            int on = (int)(load / 10);
            for (int i = 0; i < 10; i++) Para(g, i < on ? (i >= 8 ? C(255, 40, 140) : acc) : C(44, 34, 84), x + 10 + i * 12, 126, 9, 10, 4);
            Str(g, "LOAD", 10, acc, x + 10, 139);
            StrR(g, $"{load:0}%", 10, Color.White, x + 136, 139);
        }
        Panel(6, s.CpuLabel, s.CpuT, s.CpuL, C(0, 230, 255));
        Panel(164, s.GpuLabel, s.GpuT, s.GpuL, C(255, 40, 140));
        void Hp(float y, string tag, Color c, float p, string val)
        {
            Para(g, c, 4, y, 50, 16, 5);
            Str(g, tag, 10, C(14, 10, 34), 12, y + 1);
            Fill(g, Color.White, 61, y + 1, 172, 14);
            Fill(g, C(44, 34, 84), 62, y + 2, 170, 12);
            Fill(g, c, 62, y + 2, 170 * p / 100, 12);
            StrR(g, val, 10, Color.White, 316, y + 1);
        }
        Hp(164, "RAM", C(70, 240, 110), s.RamL, "12.3/32G");
        Hp(188, "VRAM", C(70, 150, 255), s.VramL, "2.1/8G");
        Str(g, "CPU PWR 88W   GPU PWR 210W", 10, C(255, 175, 205), 8, 216);
        StrR(g, "21:45", 10, C(255, 230, 0), 314, 216);
    }

    // ---------------- PELICULAS ----------------
    static void Peliculas(Graphics g, Sample s, float t)
    {
        Fill(g, C(22, 4, 8), 0, 0, 320, 240);
        void Strip(float y, int dir)
        {
            Fill(g, C(12, 12, 12), 0, y, 320, 16);
            float off = (t * 33) % 16; if (dir < 0) off = 16 - off;
            for (float x = -16 + off; x < 320; x += 16) RFill(g, C(205, 195, 175), x + 4, y + 4, 8, 8, 2);
        }
        Strip(0, 1); Strip(224, -1);
        for (int x = 0; x < 16; x += 4)
        {
            Fill(g, C(96, 10, 22), x, 16, 2, 208); Fill(g, C(52, 4, 12), x + 2, 16, 2, 208);
            Fill(g, C(96, 10, 22), 304 + x, 16, 2, 208); Fill(g, C(52, 4, 12), 306 + x, 16, 2, 208);
        }
        RFill(g, C(52, 4, 12), 18, 20, 284, 50, 6);
        using (var gold = new Pen(C(235, 185, 60), 2)) { using var p = Ui.Round(new RectangleF(18, 20, 284, 50), 6); g.DrawPath(gold, p); }
        int step = (int)(t * 7);
        int k = 0;
        for (int bx = 28; bx < 296; bx += 12, k++)
        {
            using var b1 = new SolidBrush((k + step) % 3 == 0 ? C(255, 244, 190) : C(90, 66, 26));
            using var b2 = new SolidBrush((k + 1000 - step) % 3 == 0 ? C(255, 244, 190) : C(90, 66, 26));
            g.FillEllipse(b1, bx - 2, 23, 4, 4); g.FillEllipse(b2, bx - 2, 62, 4, 4);
        }
        StrC(g, s.Name, 18, C(235, 185, 60), 160, 26, FontStyle.Bold, "Georgia");
        StrC(g, "EN CARTELERA  21:45", 9, C(240, 236, 228), 160, 51, FontStyle.Regular, "Consolas");
        void Ticket(float x, string label, int sala, float temp, float load)
        {
            RFill(g, C(246, 232, 198), x, 76, 138, 94, 6);
            using (var bg = new SolidBrush(C(22, 4, 8))) { g.FillEllipse(bg, x - 7, 116, 14, 14); g.FillEllipse(bg, x + 131, 116, 14, 14); }
            using (var dash = new Pen(C(120, 90, 30), 2) { DashPattern = new[] { 1.5f, 1.5f } }) g.DrawLine(dash, x + 108, 82, x + 108, 164);
            using (var red = new Pen(C(170, 22, 34))) g.DrawRectangle(red, x + 4, 80, 100, 86);
            Str(g, label, 10, C(170, 22, 34), x + 8, 82);
            Str(g, $"{temp:0}°C", 26, temp < 60 ? C(60, 30, 26) : C(170, 22, 34), x + 6, 98);
            Str(g, $"USO {load:0}%", 10, C(60, 30, 26), x + 8, 130);
            RFill(g, C(220, 200, 160), x + 9, 154, 88, 6, 3);
            RFill(g, C(170, 22, 34), x + 9, 154, 88 * load / 100, 6, 3);
            StrC(g, "SALA", 8, C(60, 30, 26), x + 123, 90, FontStyle.Regular);
            StrC(g, sala.ToString(), 18, C(170, 22, 34), x + 123, 102, FontStyle.Bold, "Georgia");
            StrC(g, "ADMIT", 7, C(60, 30, 26), x + 123, 136, FontStyle.Regular);
            StrC(g, "ONE", 7, C(60, 30, 26), x + 123, 146, FontStyle.Regular);
        }
        Ticket(18, s.CpuLabel, 1, s.CpuT, s.CpuL);
        Ticket(164, s.GpuLabel, 2, s.GpuT, s.GpuL);
        void Credit(float y, string l, float p, string v)
        {
            Str(g, l, 10, C(235, 185, 60), 22, y);
            RFill(g, C(52, 4, 12), 76, y + 3, 140, 8, 4);
            RFill(g, C(235, 185, 60), 76, y + 3, 140 * p / 100, 8, 4);
            StrR(g, v, 10, C(240, 236, 228), 300, y);
        }
        Credit(174, "RAM", s.RamL, "12.3/32G");
        Credit(192, "VRAM", s.VramL, "2.1/8G");
    }
}
