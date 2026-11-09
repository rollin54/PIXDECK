using System.Diagnostics;
using System.Drawing.Drawing2D;
using PixDeckHub;

namespace PixDeckSetup;

/// <summary>Ventana del instalador: opciones -> progreso -> listo.</summary>
public sealed class SetupForm : DarkForm
{
    readonly Panel _pageOptions, _pageProgress, _pageDone;
    readonly TextBox _dir;
    readonly ToggleSwitch _autostart, _desktop, _launch;
    readonly Label _status, _doneText;
    readonly PaintPanel _bar;
    float _progress;
    readonly string? _existing;

    public SetupForm()
    {
        _existing = Installer.InstalledDir();
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"Instalar {Installer.AppName} {Installer.Version}";
        ClientSize = new Size(640, 470);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var header = new PaintPanel { Left = 0, Top = 0, Width = 640, Height = 110, Painter = PaintHeader };
        Controls.Add(header);

        // ---------- pagina 1: opciones ----------
        _pageOptions = new Panel { Left = 0, Top = 110, Width = 640, Height = 360, BackColor = Ui.Bg };
        _pageOptions.Controls.Add(Ui.Label(_existing != null
                ? "Ya tienes PixDeck Hub instalado: se actualizará y se conserva tu configuración."
                : "Monitor de hardware para tu pantalla PIXDECK por USB.", 28, 10, _existing != null ? Ui.Ok : Ui.Dim, Ui.Font(9.5f)));
        _pageOptions.Controls.Add(Ui.Label("Carpeta de instalación", 28, 42, Ui.Text, Ui.Semi(10)));
        _dir = new TextBox { Left = 28, Top = 66, Width = 456, Text = _existing ?? Installer.DefaultDir };
        var browse = new NeonButton { Left = 494, Top = 62, Width = 118, Height = 32, Title = "Cambiar...", TitleSize = 9.5f };
        browse.Click += (_, _) => Browse();
        _pageOptions.Controls.Add(_dir);
        _pageOptions.Controls.Add(browse);

        _autostart = new ToggleSwitch { Left = 28, Top = 112, Width = 584, Height = 50, Checked = true,
            Title = "Iniciar con Windows", Subtitle = "Arranca en segundo plano (solo el icono en la bandeja) y conecta el equipo solo" };
        _desktop = new ToggleSwitch { Left = 28, Top = 170, Width = 584, Height = 46, Checked = true,
            Title = "Acceso directo en el escritorio" };
        _launch = new ToggleSwitch { Left = 28, Top = 224, Width = 584, Height = 46, Checked = true,
            Title = "Abrir PixDeck Hub al terminar" };
        _pageOptions.Controls.AddRange(new Control[] { _autostart, _desktop, _launch });

        var cancel = new NeonButton { Left = 336, Top = 292, Width = 130, Height = 48, Title = "Cancelar", TitleSize = 10.5f, Accent = Ui.Border, Accent2 = Ui.Dim };
        var install = new NeonButton { Left = 476, Top = 292, Width = 136, Height = 48, Title = _existing != null ? "Actualizar" : "Instalar",
            TitleSize = 12, Accent = Ui.Ok, Accent2 = Ui.Cyan };
        cancel.Click += (_, _) => Close();
        install.Click += (_, _) => StartInstall();
        _pageOptions.Controls.Add(cancel);
        _pageOptions.Controls.Add(install);
        if (!Installer.HasPayload)
        {
            install.Enabled = false;
            _pageOptions.Controls.Add(Ui.Label("Este instalador se compiló sin los archivos del programa (falta payload.zip).", 28, 300, Ui.Hot, Ui.Font(9)));
        }
        Controls.Add(_pageOptions);

        // ---------- pagina 2: progreso ----------
        _pageProgress = new Panel { Left = 0, Top = 110, Width = 640, Height = 360, BackColor = Ui.Bg, Visible = false };
        _pageProgress.Controls.Add(Ui.Label("Instalando...", 28, 40, Ui.Text, Ui.Semi(13)));
        _bar = new PaintPanel { Left = 28, Top = 90, Width = 584, Height = 18, Painter = PaintBar };
        _status = new Label { Left = 28, Top = 120, Width = 584, Height = 40, ForeColor = Ui.Dim, Font = Ui.Font(9), AutoEllipsis = true };
        _pageProgress.Controls.Add(_bar);
        _pageProgress.Controls.Add(_status);
        Controls.Add(_pageProgress);

        // ---------- pagina 3: listo ----------
        _pageDone = new Panel { Left = 0, Top = 110, Width = 640, Height = 360, BackColor = Ui.Bg, Visible = false };
        _pageDone.Controls.Add(Ui.Label("¡Listo!", 28, 24, Ui.Ok, Ui.Black(20)));
        _doneText = new Label { Left = 30, Top = 76, Width = 580, Height = 190, ForeColor = Ui.Text, Font = Ui.Font(10) };
        _pageDone.Controls.Add(_doneText);
        var finish = new NeonButton { Left = 476, Top = 292, Width = 136, Height = 48, Title = "Terminar", TitleSize = 12, Accent = Ui.Ok, Accent2 = Ui.Cyan };
        finish.Click += (_, _) => Close();
        _pageDone.Controls.Add(finish);
        Controls.Add(_pageDone);

        Ui.Style(this);
        ResumeLayout(false);
    }

    void PaintHeader(Graphics g, Rectangle r)
    {
        float s = DeviceDpi / 96f;
        using (var bg = new LinearGradientBrush(r, Color.FromArgb(24, 16, 52), Ui.Bg, 0f)) g.FillRectangle(bg, r);
        g.DrawIcon(Ui.AppIcon, new Rectangle((int)(26 * s), (int)(24 * s), (int)(56 * s), (int)(56 * s)));
        using (var f = new Font("Segoe UI Black", 26, FontStyle.Italic))
        using (var b = new LinearGradientBrush(new RectangleF(96 * s, 0, 260 * s, 60 * s), Ui.Violet, Ui.Cyan, 0f))
            g.DrawString("PIXDECK", f, b, 92 * s, 14 * s);
        using (var f2 = Ui.Semi(11))
        using (var b2 = new SolidBrush(Ui.Dim))
            g.DrawString($"HUB  ·  instalador v{Installer.Version}", f2, b2, 98 * s, 66 * s);
        using var line = new Pen(Ui.Border);
        g.DrawLine(line, 0, r.Height - 1, r.Width, r.Height - 1);
    }

    void PaintBar(Graphics g, Rectangle r)
    {
        using (var track = Ui.Round(new RectangleF(0, 0, r.Width - 1, r.Height - 1), r.Height / 2f))
        using (var tb = new SolidBrush(Ui.Panel2)) g.FillPath(tb, track);
        float w = Math.Max(r.Height, (r.Width - 1) * _progress / 100f);
        using var fill = Ui.Round(new RectangleF(0, 0, w, r.Height - 1), r.Height / 2f);
        using var fb = new LinearGradientBrush(new RectangleF(0, 0, r.Width, r.Height), Ui.Violet, Ui.Cyan, 0f);
        g.FillPath(fb, fill);
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog { Description = "Elige dónde instalar PixDeck Hub", UseDescriptionForTitle = true, SelectedPath = _dir.Text };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var p = dlg.SelectedPath;
        // siempre en su propia subcarpeta, para que desinstalar nunca borre otra cosa
        if (!Path.GetFileName(p.TrimEnd('\\')).Equals(Installer.AppName, StringComparison.OrdinalIgnoreCase))
            p = Path.Combine(p, Installer.AppName);
        _dir.Text = p;
    }

    void StartInstall()
    {
        string dir;
        try { dir = Path.GetFullPath(_dir.Text.Trim()); }
        catch { MessageBox.Show(this, "La carpeta no es válida.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!Path.GetFileName(dir.TrimEnd('\\')).Equals(Installer.AppName, StringComparison.OrdinalIgnoreCase))
            dir = Path.Combine(dir, Installer.AppName);

        var opts = new InstallOptions(dir, _autostart.Checked, _desktop.Checked);
        bool launch = _launch.Checked;
        _pageOptions.Visible = false;
        _pageProgress.Visible = true;
        ControlBox = false;

        Task.Run(() => Installer.Install(opts, (p, msg) => BeginInvoke(() =>
        {
            _progress = p;
            _status.Text = msg;
            _bar.Invalidate();
        }))).ContinueWith(t => BeginInvoke(() =>
        {
            ControlBox = true;
            _pageProgress.Visible = false;
            if (t.Exception != null)
            {
                MessageBox.Show(this, "No se pudo completar la instalación:\n\n" + t.Exception.GetBaseException().Message,
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _pageOptions.Visible = true;
                return;
            }
            _pageDone.Visible = true;
            _doneText.Text =
                $"PixDeck Hub {Installer.Version} quedó instalado en:\n{dir}\n\n" +
                "• Lo encuentras en el menú Inicio" + (opts.Desktop ? " y en el escritorio" : "") + ".\n" +
                (opts.Autostart ? "• Se iniciará solo con Windows (en la bandeja).\n" : "• No se inicia con Windows (lo puedes activar desde el programa).\n") +
                "• Para quitarlo: Configuración › Aplicaciones › PixDeck Hub › Desinstalar.";
            if (launch)
            {
                try { Process.Start(new ProcessStartInfo(Path.Combine(dir, Installer.ExeName)) { UseShellExecute = true, WorkingDirectory = dir }); }
                catch { }
            }
        }));
    }
}

/// <summary>Panel que se pinta con un delegado.</summary>
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

/// <summary>Confirmacion del desinstalador.</summary>
public sealed class UninstallForm : DarkForm
{
    readonly ToggleSwitch _purge;
    public bool PurgeData => _purge.Checked;

    public UninstallForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"Desinstalar {Installer.AppName}";
        ClientSize = new Size(560, 250);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(Ui.Label("¿Desinstalar PixDeck Hub?", 24, 20, Ui.Text, Ui.Black(16)));
        Controls.Add(Ui.Label("Se quitan el programa, los accesos directos y el inicio con Windows.", 26, 64, Ui.Dim, Ui.Font(9.5f)));
        _purge = new ToggleSwitch { Left = 24, Top = 100, Width = 512, Height = 54, Checked = false,
            Title = "Borrar también mi configuración", Subtitle = "Sensores, nombres y temas subidos (Documentos no se toca)" };
        Controls.Add(_purge);
        var cancel = new NeonButton { Left = 266, Top = 180, Width = 126, Height = 46, Title = "Cancelar", TitleSize = 10.5f, Accent = Ui.Border, Accent2 = Ui.Dim };
        var ok = new NeonButton { Left = 402, Top = 180, Width = 134, Height = 46, Title = "Desinstalar", TitleSize = 11, Accent = Ui.Hot, Accent2 = Ui.Orange };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.AddRange(new Control[] { cancel, ok });
        ResumeLayout(false);
    }
}
