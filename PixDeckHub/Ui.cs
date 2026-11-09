using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PixDeckHub;

/// <summary>Colores, fuentes y controles propios (tema oscuro "neon").</summary>
public static class Ui
{
    public static readonly Color Bg      = Color.FromArgb(10, 12, 20);
    public static readonly Color Panel   = Color.FromArgb(20, 24, 38);
    public static readonly Color Panel2  = Color.FromArgb(30, 36, 56);
    public static readonly Color Border  = Color.FromArgb(48, 56, 86);
    public static readonly Color Text    = Color.FromArgb(235, 240, 255);
    public static readonly Color Dim     = Color.FromArgb(130, 140, 175);
    public static readonly Color Violet  = Color.FromArgb(150, 90, 255);
    public static readonly Color Cyan    = Color.FromArgb(0, 205, 255);
    public static readonly Color Orange  = Color.FromArgb(255, 150, 20);
    public static readonly Color Ok      = Color.FromArgb(70, 225, 130);
    public static readonly Color Warn    = Color.FromArgb(255, 195, 40);
    public static readonly Color Hot     = Color.FromArgb(255, 70, 70);

    public static Font Font(float size, FontStyle st = FontStyle.Regular) => new("Segoe UI", size, st);
    public static Font Semi(float size) => new("Segoe UI Semibold", size);
    public static Font Black(float size) => new("Segoe UI Black", size);

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Hq(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    static Icon? _icon;
    /// <summary>Icono generado por codigo (no hace falta un .ico).</summary>
    public static Icon AppIcon
    {
        get
        {
            if (_icon != null) return _icon;
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                Hq(g);
                using var br = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Violet, Cyan, 45f);
                using var path = Round(new RectangleF(2, 2, 60, 60), 14);
                g.FillPath(br, path);
                using var inner = Round(new RectangleF(10, 14, 44, 32), 5);
                using var dark = new SolidBrush(Color.FromArgb(14, 16, 30));
                g.FillPath(dark, inner);
                using var bar1 = new SolidBrush(Ok);
                using var bar2 = new SolidBrush(Orange);
                g.FillRectangle(bar1, 16, 32, 8, 10);
                g.FillRectangle(bar2, 28, 24, 8, 18);
                g.FillRectangle(bar1, 40, 20, 8, 22);
                g.FillRectangle(dark, 24, 48, 16, 8);
            }
            _icon = Icon.FromHandle(bmp.GetHicon());
            return _icon;
        }
    }

    // ---------------- estilo para controles estandar ----------------
    public static void Style(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox t:
                    t.BackColor = Panel2; t.ForeColor = Text; t.BorderStyle = BorderStyle.FixedSingle;
                    t.Font = Font(10);
                    break;
                case ComboBox cb:
                    cb.BackColor = Panel2; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Flat;
                    cb.Font = Font(9.5f);
                    break;
                case NumericUpDown n:
                    n.BackColor = Panel2; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle;
                    n.Font = Font(10);
                    break;
                case CheckBox ch:
                    ch.ForeColor = Text; ch.Font = Font(10);
                    break;
                case Label l when l.ForeColor == SystemColors.ControlText:
                    l.ForeColor = Text;
                    break;
            }
            if (c.HasChildren) Style(c);
        }
    }

    public static Label Label(string text, int x, int y, Color? color = null, Font? font = null, int w = 0)
    {
        var l = new Label
        {
            Text = text, Left = x, Top = y, AutoSize = w == 0, Width = w == 0 ? 100 : w,
            ForeColor = color ?? Text, Font = font ?? Font(10), BackColor = Color.Transparent,
        };
        return l;
    }
}

/// <summary>Boton redondeado con borde degradado, titulo y subtitulo.</summary>
public sealed class NeonButton : Control
{
    bool _hover, _down;
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public Color Accent { get; set; } = Ui.Violet;
    public Color Accent2 { get; set; } = Ui.Cyan;
    public float TitleSize { get; set; } = 12f;
    public bool Selected { get; set; }

    public NeonButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        ForeColor = Ui.Text;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.Hq(g);
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        float rad = Math.Min(14, Height / 3f);
        using var path = Ui.Round(r, rad);
        var fill = _down ? Ui.Panel2 : _hover || Selected ? Color.FromArgb(34, 40, 64) : Ui.Panel;
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (_hover || Selected)
        {
            using var glow = new LinearGradientBrush(new RectangleF(0, 0, Width, Height),
                Color.FromArgb(40, Accent), Color.FromArgb(10, Accent2), 0f);
            g.FillPath(glow, path);
        }
        using (var pen = new Pen(new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Accent, Accent2, 0f),
                                 _hover || Selected ? 2.2f : 1.4f))
            g.DrawPath(pen, path);

        using var tf = Ui.Semi(TitleSize);
        using var sf = Ui.Font(9);
        var tsz = g.MeasureString(Title, tf);
        bool hasSub = !string.IsNullOrEmpty(Subtitle);
        float total = tsz.Height + (hasSub ? 18 : 0);
        float ty = (Height - total) / 2;
        using (var tb = new SolidBrush(Enabled ? Ui.Text : Ui.Dim))
            g.DrawString(Title, tf, tb, (Width - tsz.Width) / 2, ty);
        if (hasSub)
        {
            var ssz = g.MeasureString(Subtitle, sf);
            using var sb = new SolidBrush(Ui.Dim);
            g.DrawString(Subtitle, sf, sb, (Width - ssz.Width) / 2, ty + tsz.Height);
        }
    }
}

/// <summary>Fila con titulo, subtitulo e interruptor ON/OFF.</summary>
public sealed class ToggleSwitch : Control
{
    bool _checked, _hover;
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public bool Checked { get => _checked; set { _checked = value; Invalidate(); } }
    public event EventHandler? Toggled;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        BackColor = Ui.Bg;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e)
    {
        if (!Enabled) return;
        Checked = !Checked;
        Toggled?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.Hq(g);
        float s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var card = Ui.Round(r, Math.Min(12 * s, Height / 2f)))
        {
            using var b = new SolidBrush(_hover ? Color.FromArgb(28, 32, 52) : Ui.Panel);
            g.FillPath(b, card);
            using var p = new Pen(_checked ? Color.FromArgb(140, Ui.Ok) : Ui.Border);
            g.DrawPath(p, card);
        }
        using var tf = Ui.Semi(10.5f);
        using var sf = Ui.Font(8.5f);
        using var tb = new SolidBrush(Ui.Text);
        using var db = new SolidBrush(Ui.Dim);
        var tsz = g.MeasureString(Title, tf);
        bool sub = !string.IsNullOrEmpty(Subtitle);
        float ty = (Height - tsz.Height - (sub ? 15 * s : 0)) / 2;
        g.DrawString(Title, tf, tb, 14 * s, ty);
        if (sub) g.DrawString(Subtitle, sf, db, 14 * s, ty + tsz.Height - 2 * s);

        // interruptor
        float tw = 46 * s, th = 24 * s, tx = Width - tw - 14 * s, tyy = (Height - th) / 2;
        using (var track = Ui.Round(new RectangleF(tx, tyy, tw, th), th / 2))
        using (var trb = new SolidBrush(_checked ? Ui.Ok : Ui.Panel2))
            g.FillPath(trb, track);
        float k = th - 6 * s, kx = _checked ? tx + tw - k - 3 * s : tx + 3 * s;
        using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, kx, tyy + 3 * s, k, k);
        using var lf = Ui.Semi(8.5f);
        string lbl = _checked ? "SÍ" : "NO";
        var lsz = g.MeasureString(lbl, lf);
        using var lb = new SolidBrush(_checked ? Ui.Ok : Ui.Dim);
        g.DrawString(lbl, lf, lb, tx - lsz.Width - 6 * s, (Height - lsz.Height) / 2);
    }
}

/// <summary>Formulario base oscuro con barra de titulo nativa en modo oscuro.</summary>
public class DarkForm : Form
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public DarkForm()
    {
        BackColor = Ui.Bg;
        ForeColor = Ui.Text;
        Font = Ui.Font(10);
        Icon = Ui.AppIcon;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); } catch { }
    }
}
