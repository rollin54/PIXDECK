namespace PixDeckHub;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool autostart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

        // Una sola instancia: si ya corre (p. ej. arranco con Windows), se le pide que muestre su ventana.
        using var mutex = new Mutex(true, "PixDeckHub_SingleInstance", out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting("PixDeckHub_Show").Set(); } catch { }
            return;
        }
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "PixDeckHub_Show");

        ApplicationConfiguration.Initialize();
        Application.Run(new HubContext(autostart, showEvent));
    }
}

/// <summary>Vive en la bandeja del sistema. La ventana se abre a mano o desde el icono.</summary>
sealed class HubContext : ApplicationContext
{
    readonly HubService _hub = new();
    readonly NotifyIcon _tray;
    readonly Control _invoker = new();
    readonly System.Windows.Forms.Timer _trayTimer = new() { Interval = 2000 };
    MainForm? _main;
    bool _ready, _showWhenReady;

    public HubContext(bool autostart, EventWaitHandle showEvent)
    {
        _ = _invoker.Handle;   // fuerza el handle para poder usar BeginInvoke desde otros hilos

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir PixDeck Hub", null, (_, _) => ShowMain());
        menu.Items.Add("Tema siguiente", null, (_, _) => _hub.SetTheme(_hub.NextTheme()));
        menu.Items.Add("Probar buzzer", null, (_, _) => _hub.TestBuzzer());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Exit());

        _tray = new NotifyIcon
        {
            Icon = Ui.AppIcon,
            Text = "PixDeck Hub",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
        _trayTimer.Tick += (_, _) =>
        {
            var t = "PixDeck Hub · " + _hub.Status;
            _tray.Text = t.Length > 63 ? t[..63] : t;
        };
        _trayTimer.Start();

        SplashForm? splash = null;
        if (!autostart)
        {
            splash = new SplashForm();
            splash.FormClosed += (_, _) => ShowMain();
            splash.Show();
            _hub.StatusChanged += () => _invoker.BeginInvoke(() => { if (!splash.IsDisposed) splash.Status = _hub.Status; });
        }

        Task.Run(() =>
        {
            _hub.InitHardware();
            _invoker.BeginInvoke(() =>
            {
                _hub.Start();
                _ready = true;
                splash?.MarkReady();
                if (_showWhenReady) ShowMain();
            });
        });

        // Segunda instancia abierta a mano => mostrar la ventana de esta
        var waiter = new Thread(() =>
        {
            while (showEvent.WaitOne())
            {
                try { _invoker.BeginInvoke(ShowMain); } catch { return; }
            }
        }) { IsBackground = true };
        waiter.Start();
    }

    void ShowMain()
    {
        if (!_ready) { _showWhenReady = true; return; }
        _main ??= new MainForm(_hub);
        _main.Show();
        if (_main.WindowState == FormWindowState.Minimized) _main.WindowState = FormWindowState.Normal;
        _main.Activate();
        _main.BringToFront();

        // Primera vez: asistente para elegir los sensores
        if (_hub.NeedsSetup && _hub.Hardware.IsOpen && !_setupShown)
        {
            _setupShown = true;
            _invoker.BeginInvoke(() => { using var f = new SetupForm(_hub); f.ShowDialog(_main); });
        }
    }
    bool _setupShown;

    void Exit()
    {
        _tray.Visible = false;
        _trayTimer.Stop();
        if (_main != null) { _main.AllowClose = true; _main.Close(); }
        _hub.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _trayTimer.Dispose();
            _invoker.Dispose();
        }
        base.Dispose(disposing);
    }
}
