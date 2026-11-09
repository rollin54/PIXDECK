using System.Drawing.Drawing2D;

namespace PixDeckHub;

/// <summary>Pantalla de bienvenida (solo al abrir el programa a mano).</summary>
public sealed class SplashForm : Form
{
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    readonly DateTime _start = DateTime.Now;
    float _progress;
    bool _ready, _closing;
    public string Status { get; set; } = "Iniciando...";

    public SplashForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 340);
        ShowInTaskbar = true;
        Text = "PixDeck Hub";
        Icon = Ui.AppIcon;
        BackColor = Ui.Bg;
        DoubleBuffered = true;
        Opacity = 0;
        TopMost = true;
        ResumeLayout(false);

        _timer.Tick += (_, _) =>
        {
            var el = (DateTime.Now - _start).TotalSeconds;
            if (_closing)
            {
                Opacity = Math.Max(0, Opacity - 0.08);
                if (Opacity <= 0) { _timer.Stop(); Close(); }
            }
            else if (Opacity < 1) Opacity = Math.Min(1, Opacity + 0.06);

            float target = _ready ? 100 : (float)Math.Min(85, el * 30);
            _progress += (target - _progress) * 0.08f;
            if (_ready && _progress > 99 && el > 2.4) _closing = true;
            Invalidate();
        };
        _timer.Start();
    }

    public void MarkReady() { _ready = true; Status = "¡Listo!"; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Ui.Hq(g);
        float s = DeviceDpi / 96f;
        var r = ClientRectangle;
        float t = (float)(DateTime.Now - _start).TotalSeconds;

        // fondo con brillo violeta/cian que se mueve
        using (var bg = new LinearGradientBrush(r, Color.FromArgb(14, 12, 32), Color.FromArgb(6, 10, 20), 60f))
            g.FillRectangle(bg, r);
        for (int i = 0; i < 3; i++)
        {
            float cx = r.Width * (0.25f + 0.5f * i / 2f) + MathF.Sin(t * 0.8f + i * 2) * 40 * s;
            float cy = r.Height * 0.45f + MathF.Cos(t * 0.6f + i) * 30 * s;
            float rad = 170 * s;
            using var path = new GraphicsPath();
            path.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
            using var pg = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(46, i == 1 ? Ui.Cyan : Ui.Violet),
                SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) },
            };
            g.FillPath(pg, path);
        }
        // rejilla sutil
        using (var grid = new Pen(Color.FromArgb(14, 255, 255, 255)))
            for (float x = (t * 12 * s) % (24 * s); x < r.Width; x += 24 * s) g.DrawLine(grid, x, 0, x, r.Height);

        // logo
        using var titleFont = new Font("Segoe UI Black", 44, FontStyle.Italic);
        const string title = "PIXDECK";
        var tsz = g.MeasureString(title, titleFont);
        var tp = new PointF((r.Width - tsz.Width) / 2, 70 * s);
        using (var tb = new LinearGradientBrush(new RectangleF(tp, tsz), Ui.Violet, Ui.Cyan, 0f))
            g.DrawString(title, titleFont, tb, tp);
        using var subFont = Ui.Semi(13);
        const string sub = "H U B   ·   monitor de hardware por USB";
        var ssz = g.MeasureString(sub, subFont);
        using (var sb = new SolidBrush(Ui.Dim)) g.DrawString(sub, subFont, sb, (r.Width - ssz.Width) / 2, tp.Y + tsz.Height - 6 * s);

        // barra de progreso
        float bw = 380 * s, bh = 10 * s, bx = (r.Width - bw) / 2, by = 236 * s;
        using (var track = Ui.Round(new RectangleF(bx, by, bw, bh), bh / 2))
        using (var tbr = new SolidBrush(Ui.Panel2)) g.FillPath(tbr, track);
        float fw = Math.Max(bh, bw * _progress / 100f);
        using (var fill = Ui.Round(new RectangleF(bx, by, fw, bh), bh / 2))
        using (var fbr = new LinearGradientBrush(new RectangleF(bx, by, bw, bh), Ui.Violet, Ui.Cyan, 0f)) g.FillPath(fbr, fill);

        using var stFont = Ui.Font(10);
        var stsz = g.MeasureString(Status, stFont);
        using (var stb = new SolidBrush(Ui.Text)) g.DrawString(Status, stFont, stb, (r.Width - stsz.Width) / 2, by + 20 * s);

        using var verFont = Ui.Font(8.5f);
        using (var vb = new SolidBrush(Color.FromArgb(90, 100, 130)))
            g.DrawString("v3.0  ·  LibreHardwareMonitor", verFont, vb, 14 * s, r.Height - 26 * s);

        using var border = new Pen(Color.FromArgb(70, Ui.Violet), 1.5f);
        g.DrawRectangle(border, 0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
