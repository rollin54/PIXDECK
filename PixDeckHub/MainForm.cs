using System.Drawing.Drawing2D;

namespace PixDeckHub;

/// <summary>Panel que se pinta con un delegado (para zonas dibujadas a mano).</summary>
public sealed class PaintPanel : Panel
{
    public Action<Graphics, Rectangle>? Painter { get; set; }
    public PaintPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Ui.Bg;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Ui.Hq(e.Graphics);
        Painter?.Invoke(e.Graphics, ClientRectangle);
    }
}

/// <summary>Ventana principal: vista en vivo del equipo, botones SETTINGS y UI.</summary>
public sealed class MainForm : DarkForm
{
    readonly HubService _hub;
    readonly PaintPanel _header, _preview, _stats;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 60 };
    readonly DateTime _t0 = DateTime.Now;
    public bool AllowClose { get; set; }
    readonly ToggleSwitch _autostart;

    public MainForm(HubService hub)
    {
        _hub = hub;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "PixDeck Hub";
        ClientSize = new Size(900, 610);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _header = new PaintPanel { Left = 0, Top = 0, Width = 900, Height = 78, Painter = PaintHeader };
        _preview = new PaintPanel { Left = 24, Top = 96, Width = 400, Height = 360, Painter = PaintPreview };
        _stats = new PaintPanel { Left = 448, Top = 360, Width = 428, Height = 96, Painter = PaintStats };

        var btnSettings = new NeonButton
        {
            Left = 448, Top = 96, Width = 428, Height = 80, Title = "⚙  SETTINGS", TitleSize = 15,
            Subtitle = "Nombre, sensores, datos extra, alertas y puerto", Accent = Ui.Violet, Accent2 = Ui.Cyan,
        };
        btnSettings.Click += (_, _) =>
        {
            using var f = new SettingsForm(_hub);
            f.ShowDialog(this);
            _autostart!.Checked = Autostart.IsEnabled();   // por si se cambio desde Settings
        };

        var btnUi = new NeonButton
        {
            Left = 448, Top = 186, Width = 428, Height = 80, Title = "◆  UI", TitleSize = 15,
            Subtitle = "8 temas + los que subas · vista previa · rotación", Accent = Ui.Orange, Accent2 = Color.FromArgb(255, 40, 140),
        };
        btnUi.Click += (_, _) => { using var f = new UiForm(_hub); f.ShowDialog(this); };

        var btnEditor = new NeonButton
        {
            Left = 448, Top = 276, Width = 428, Height = 74, Title = "✎  CREADOR DE TEMAS", TitleSize = 14,
            Subtitle = "Diseña tu propia pantalla y pruébala en el equipo", Accent = Ui.Cyan, Accent2 = Ui.Ok,
        };
        btnEditor.Click += (_, _) => { using var f = new ThemeEditorForm(_hub); f.ShowDialog(this); };

        // Inicio con Windows (tarea programada con privilegios, sin aviso de UAC)
        _autostart = new ToggleSwitch
        {
            Left = 24, Top = 472, Width = 852, Height = 50,
            Title = "Iniciar PixDeck Hub con Windows",
            Subtitle = "Arranca en segundo plano (solo el icono en la bandeja) y conecta el equipo solo",
        };
        _autostart.Toggled += (_, _) =>
        {
            bool want = _autostart.Checked;
            if (!Autostart.Set(want))
            {
                _autostart.Checked = Autostart.IsEnabled();
                MessageBox.Show(this, "No se pudo cambiar el inicio con Windows. ¿El programa corre como administrador?",
                                "PixDeck Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        HandleCreated += (_, _) => _autostart.Checked = Autostart.IsEnabled();

        var bNext = SmallButton("Tema siguiente", 24, 538, () => _hub.SetTheme(_hub.NextTheme()));
        var bBeep = SmallButton("Probar buzzer", 196, 538, _hub.TestBuzzer);
        var bRec = SmallButton("Reconectar USB", 368, 538, _hub.Reconnect);
        var bSetup = SmallButton("Elegir sensores", 540, 538, () => { using var f = new SetupForm(_hub); f.ShowDialog(this); _autostart.Checked = Autostart.IsEnabled(); });
        var bHide = SmallButton("Ocultar en bandeja", 712, 538, Hide);
        bHide.Width = 164;

        Controls.AddRange(new Control[] { _header, _preview, _stats, btnSettings, btnUi, btnEditor, _autostart, bNext, bBeep, bRec, bSetup, bHide });
        ResumeLayout(false);

        _timer.Tick += (_, _) =>
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            _preview.Invalidate();
            if ((DateTime.Now - _t0).TotalMilliseconds % 500 < 70) { _header.Invalidate(); _stats.Invalidate(); }
        };
        _timer.Start();
    }

    NeonButton SmallButton(string text, int x, int y, Action a)
    {
        var b = new NeonButton { Left = x, Top = y, Width = 160, Height = 50, Title = text, TitleSize = 10.5f };
        b.Click += (_, _) => a();
        return b;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();   // sigue funcionando en la bandeja
            return;
        }
        base.OnFormClosing(e);
    }

    // ---------------- pintura ----------------
    void PaintHeader(Graphics g, Rectangle r)
    {
        float s = DeviceDpi / 96f;
        using (var bg = new LinearGradientBrush(r, Color.FromArgb(22, 16, 46), Ui.Bg, 0f)) g.FillRectangle(bg, r);
        using (var f = new Font("Segoe UI Black", 24, FontStyle.Italic))
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, 300 * s, 50 * s), Ui.Violet, Ui.Cyan, 0f))
            g.DrawString("PIXDECK", f, b, 20 * s, 12 * s);
        using (var f2 = Ui.Semi(12))
        using (var b2 = new SolidBrush(Ui.Dim))
            g.DrawString("HUB", f2, b2, 200 * s, 30 * s);

        bool on = _hub.Link.Connected;
        var col = on ? Ui.Ok : Ui.Warn;
        string txt = _hub.Status;
        using var sf = Ui.Font(10);
        var sz = g.MeasureString(txt, sf);
        float pw = sz.Width + 40 * s, ph = 32 * s, px = r.Width - pw - 24 * s, py = (r.Height - ph) / 2;
        using (var pill = Ui.Round(new RectangleF(px, py, pw, ph), ph / 2))
        {
            using var pb = new SolidBrush(Ui.Panel);
            g.FillPath(pb, pill);
            using var pp = new Pen(Color.FromArgb(120, col), 1.2f);
            g.DrawPath(pp, pill);
        }
        using (var dot = new SolidBrush(col)) g.FillEllipse(dot, px + 14 * s, py + ph / 2 - 5 * s, 10 * s, 10 * s);
        using (var tb = new SolidBrush(Ui.Text)) g.DrawString(txt, sf, tb, px + 30 * s, py + (ph - sz.Height) / 2);
        using var line = new Pen(Ui.Border);
        g.DrawLine(line, 0, r.Height - 1, r.Width, r.Height - 1);
    }

    void PaintPreview(Graphics g, Rectangle r)
    {
        float s = DeviceDpi / 96f;
        using (var card = Ui.Round(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), 14 * s))
        {
            using var b = new SolidBrush(Ui.Panel);
            g.FillPath(b, card);
            using var p = new Pen(Ui.Border);
            g.DrawPath(p, card);
        }
        var c = _hub.Config;
        using (var f = Ui.Semi(11))
        using (var tb = new SolidBrush(Ui.Text))
            g.DrawString("Vista del equipo", f, tb, 16 * s, 12 * s);
        using (var f = Ui.Font(9))
        using (var db = new SolidBrush(Ui.Dim))
        {
            string tema = $"Tema: {_hub.ThemeName(c.Theme)}" + (c.RotateSec > 0 ? $"  ·  rota cada {c.RotateSec}s" : "");
            g.DrawString(tema, f, db, 16 * s, 34 * s);
        }

        // "pantalla" 4:3 con marco
        float sw = r.Width - 40 * s, sh = sw * 0.75f, sx = 20 * s, sy = 62 * s;
        using (var bezel = Ui.Round(new RectangleF(sx - 6 * s, sy - 6 * s, sw + 12 * s, sh + 12 * s), 8 * s))
        using (var bb = new SolidBrush(Color.FromArgb(4, 4, 8))) g.FillPath(bb, bezel);
        var v = _hub.CurrentValues();
        float V(int i, float d) => v[i] ?? d;
        float vram = v[11] is float vu && v[12] is float vt && vt > 0 ? vu * 100 / vt : 0;
        var sample = new ThemePreview.Sample(V(0, 0), V(4, 0), V(1, 0), V(5, 0), V(10, 0), vram,
            HubService.Clean(c.DeviceName, 16), HubService.Clean(c.CpuLabel, 16), HubService.Clean(c.GpuLabel, 16), v);
        float t = (float)(DateTime.Now - _t0).TotalSeconds;
        ThemePreview.Draw(g, Rectangle.Round(new RectangleF(sx, sy, sw, sh)), c.Theme, sample, t);
    }

    void PaintStats(Graphics g, Rectangle r)
    {
        float s = DeviceDpi / 96f;
        using (var card = Ui.Round(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), 12 * s))
        {
            using var b = new SolidBrush(Ui.Panel);
            g.FillPath(b, card);
            using var p = new Pen(Ui.Border);
            g.DrawPath(p, card);
        }
        var v = _hub.CurrentValues();
        string F(float? x, string u, string fmt = "0") => x is float f ? f.ToString(fmt) + u : "--";
        var items = new (string L, string V, Color C)[]
        {
            ("CPU", F(v[0], "°C"), Ui.Orange), ("USO", F(v[1], "%"), Ui.Orange),
            ("GPU", F(v[4], "°C"), Ui.Ok), ("USO", F(v[5], "%"), Ui.Ok),
            ("RAM", F(v[8], " GB", "0.0"), Ui.Cyan), ("VRAM", v[11] is float m ? (m / 1024).ToString("0.0") + " GB" : "--", Ui.Violet),
        };
        using var lf = Ui.Font(8.5f);
        using var vf = Ui.Semi(13);
        float cw = r.Width / 3f;
        for (int i = 0; i < items.Length; i++)
        {
            float x = (i % 3) * cw + 16 * s, y = (i / 3) * 44 * s + 8 * s;
            using var lb = new SolidBrush(items[i].C);
            using var vb = new SolidBrush(Ui.Text);
            g.DrawString(items[i].L, lf, lb, x, y);
            g.DrawString(items[i].V, vf, vb, x, y + 13 * s);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
