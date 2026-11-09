namespace PixDeckHub;

/// <summary>
/// UI: lista de temas (incluidos + subidos) y vista previa grande animada.
/// Pasar el mouse muestra el tema; hacer clic lo aplica al instante en el equipo.
/// "Subir tema" importa un archivo .pxtheme y lo guarda en la flash del equipo.
/// </summary>
public sealed class UiForm : DarkForm
{
    readonly HubService _hub;
    readonly Panel _list;
    readonly List<(int Index, NeonButton Button)> _items = new();
    readonly PaintPanel _preview;
    readonly CheckBox _rotate;
    readonly NumericUpDown _rotateSec;
    readonly NeonButton _delete;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    readonly DateTime _t0 = DateTime.Now;
    int _hover = -1;
    bool _loading = true;

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string app, string? idList);   // barra de scroll oscura

    static readonly (Color A, Color B)[] Accents =
    {
        (Color.FromArgb(60, 255, 90),  Color.FromArgb(255, 225, 40)),   // electronica
        (Color.FromArgb(80, 255, 120), Color.FromArgb(90, 210, 255)),   // informatica
        (Color.FromArgb(255, 70, 20),  Color.FromArgb(120, 205, 255)),  // autos
        (Color.FromArgb(255, 40, 140), Color.FromArgb(0, 230, 255)),    // anime
        (Color.FromArgb(235, 185, 60), Color.FromArgb(170, 22, 34)),    // peliculas
        (Color.FromArgb(255, 150, 20), Color.FromArgb(90, 255, 120)),   // pixdeck
        (Color.FromArgb(150, 90, 255), Color.FromArgb(255, 150, 20)),   // rings
        (Color.FromArgb(0, 205, 255),  Color.FromArgb(185, 100, 255)),  // graph
    };

    public UiForm(HubService hub)
    {
        _hub = hub;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "PixDeck Hub · UI";
        ClientSize = new Size(1000, 610);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(Ui.Label("UI · TEMAS", 24, 16, Ui.Text, Ui.Black(20)));
        Controls.Add(Ui.Label("Pasa el mouse para ver el tema; haz clic para ponerlo en el equipo. El botón BOOT de la placa también los cambia.",
                              26, 56, Ui.Dim, Ui.Font(9.5f)));

        _list = new Panel { Left = 24, Top = 92, Width = 356, Height = 422, AutoScroll = true, BackColor = Ui.Bg };
        _list.HandleCreated += (_, _) => { try { SetWindowTheme(_list.Handle, "DarkMode_Explorer", null); } catch { } };
        Controls.Add(_list);

        _preview = new PaintPanel { Left = 392, Top = 92, Width = 584, Height = 422, Painter = PaintPreview };
        Controls.Add(_preview);

        int by = 540;
        _rotate = new CheckBox { Left = 24, Top = by + 10, Width = 186, Text = "Rotar temas cada", Checked = hub.Config.RotateSec > 0 };
        _rotateSec = new NumericUpDown
        {
            Left = 212, Top = by + 8, Width = 70, Minimum = 5, Maximum = 3600, Increment = 5,
            Value = Math.Clamp(hub.Config.RotateSec > 0 ? hub.Config.RotateSec : 30, 5, 3600),
        };
        var secs = Ui.Label("seg.", 288, by + 11, Ui.Dim);
        _rotate.CheckedChanged += (_, _) => SaveRotation();
        _rotateSec.ValueChanged += (_, _) => { if (_rotate.Checked) SaveRotation(); };

        var upload = new NeonButton
        {
            Left = 470, Top = by, Width = 190, Height = 44, Title = "⬆  Subir tema...", TitleSize = 10.5f,
            Accent = Ui.Violet, Accent2 = Ui.Cyan,
        };
        upload.Click += (_, _) => UploadTheme();
        _delete = new NeonButton
        {
            Left = 668, Top = by, Width = 172, Height = 44, Title = "Eliminar tema subido", TitleSize = 10,
            Accent = Ui.Hot, Accent2 = Ui.Orange,
        };
        _delete.Click += (_, _) => DeleteTheme();
        var close = new NeonButton { Left = 850, Top = by, Width = 126, Height = 44, Title = "Cerrar", TitleSize = 10, Accent = Ui.Border, Accent2 = Ui.Dim };
        close.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { _rotate, _rotateSec, secs, upload, _delete, close });

        Ui.Style(this);
        BuildList();
        ResumeLayout(false);
        _loading = false;

        int lastTheme = hub.Config.Theme;
        _timer.Tick += (_, _) =>
        {
            if (_hub.Config.Theme != lastTheme) { lastTheme = _hub.Config.Theme; Sync(); }   // cambio con el boton BOOT
            _preview.Invalidate();
        };
        _timer.Start();
    }

    void BuildList()
    {
        _list.SuspendLayout();
        foreach (var (_, b) in _items) b.Dispose();
        _items.Clear();
        _list.Controls.Clear();
        int y = 0;
        foreach (var th in _hub.ThemeList())
        {
            var acc = th.Custom ? (Ui.Violet, Ui.Cyan) : Accents[th.Index % Accents.Length];
            var b = new NeonButton
            {
                Left = 0, Top = y, Width = 330, Height = 64, TitleSize = 11.5f,
                Title = th.Custom ? $"★  {th.Name}" : $"{th.Index + 1}.  {th.Name}",
                Subtitle = th.Description, Accent = acc.Item1, Accent2 = acc.Item2,
            };
            int idx = th.Index;
            b.Click += (_, _) => { _hub.SetTheme(idx); Sync(); };
            b.MouseEnter += (_, _) => _hover = idx;
            b.MouseLeave += (_, _) => { if (_hover == idx) _hover = -1; };
            _items.Add((idx, b));
            _list.Controls.Add(b);
            y += 70;
        }
        _list.ResumeLayout();
        Sync();
    }

    void Sync()
    {
        foreach (var (i, b) in _items) { b.Selected = i == _hub.Config.Theme; b.Invalidate(); }
        _delete.Enabled = _hub.Config.Theme >= ThemeCatalog.Count;
        _delete.Invalidate();
    }

    void SaveRotation()
    {
        if (_loading) return;
        var c = _hub.Config.Clone();
        c.RotateSec = _rotate.Checked ? (int)_rotateSec.Value : 0;
        _hub.ApplyConfig(c);
    }

    void UploadTheme()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Subir un tema al equipo",
            Filter = "Temas PixDeck (*.pxtheme)|*.pxtheme|JSON (*.json)|*.json|Todos (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int idx = _hub.ImportTheme(dlg.FileName);
            _hub.SetTheme(idx);
            BuildList();
            string when = _hub.Link.Connected ? "Se está subiendo al equipo; en unos segundos aparece en pantalla."
                                              : "Se subirá al equipo cuando se conecte por USB.";
            MessageBox.Show(this, $"Tema \"{_hub.ThemeName(idx)}\" agregado.\n\n{when}", "PixDeck Hub",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            MessageBox.Show(this, $"No se pudo subir el tema:\n\n{ex.Message}", "PixDeck Hub",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void DeleteTheme()
    {
        int t = _hub.Config.Theme;
        if (t < ThemeCatalog.Count) return;
        if (MessageBox.Show(this, $"¿Eliminar el tema \"{_hub.ThemeName(t)}\" del equipo y de la biblioteca?", "PixDeck Hub",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _hub.DeleteTheme(t);
        _hover = -1;
        BuildList();
    }

    ThemePreview.Sample Sample()
    {
        var v = _hub.CurrentValues();
        float V(int i, float d) => v[i] ?? d;
        float vram = v[11] is float vu && v[12] is float vt && vt > 0 ? vu * 100 / vt : 26;
        var c = _hub.Config;
        bool any = v.Any(x => x.HasValue);
        return new ThemePreview.Sample(V(0, 62), V(4, 55), V(1, 34), V(5, 48), V(10, 41), vram,
            HubService.Clean(c.DeviceName, 16), HubService.Clean(c.CpuLabel, 16), HubService.Clean(c.GpuLabel, 16),
            any ? v : null);
    }

    void PaintPreview(Graphics g, Rectangle r)
    {
        float s = DeviceDpi / 96f;
        int theme = _hover >= 0 ? _hover : _hub.Config.Theme;
        using (var card = Ui.Round(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), 14 * s))
        {
            using var b = new SolidBrush(Ui.Panel);
            g.FillPath(b, card);
            using var p = new Pen(Ui.Border);
            g.DrawPath(p, card);
        }
        using (var f = Ui.Semi(11))
        using (var tb = new SolidBrush(Ui.Text))
            g.DrawString($"Vista previa: {_hub.ThemeName(theme)}", f, tb, 16 * s, 10 * s);
        if (theme == _hub.Config.Theme)
        {
            using var bf = Ui.Semi(8.5f);
            const string tag = "EN PANTALLA";
            var sz = g.MeasureString(tag, bf);
            var br = new RectangleF(r.Width - sz.Width - 34 * s, 11 * s, sz.Width + 16 * s, sz.Height + 3 * s);
            using var bp = Ui.Round(br, br.Height / 2);
            using var bb = new SolidBrush(Ui.Violet);
            g.FillPath(bb, bp);
            using var wb = new SolidBrush(Color.White);
            g.DrawString(tag, bf, wb, br.X + 8 * s, br.Y + 1.5f * s);
        }

        float sh = r.Height - 62 * s, sw = sh * 4 / 3f;
        float sx = (r.Width - sw) / 2, sy = 44 * s;
        using (var bezel = Ui.Round(new RectangleF(sx - 7 * s, sy - 7 * s, sw + 14 * s, sh + 14 * s), 9 * s))
        using (var bb = new SolidBrush(Color.FromArgb(4, 4, 8))) g.FillPath(bb, bezel);
        float t = (float)(DateTime.Now - _t0).TotalSeconds;
        ThemePreview.Draw(g, Rectangle.Round(new RectangleF(sx, sy, sw, sh)), theme, Sample(), t);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
