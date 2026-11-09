using LibreHardwareMonitor.Hardware;

namespace PixDeckHub;

/// <summary>
/// Asistente de primer inicio: casillas para elegir qué sensores se ven en el equipo.
/// Se muestra una sola vez (y se puede volver a abrir desde Settings, Sensores).
/// </summary>
public sealed class SetupForm : DarkForm
{
    readonly HubService _hub;
    readonly List<SensorInfo> _sensors;
    readonly Dictionary<string, string> _roles;
    readonly List<(RoleDef Role, CheckBox Box, Label Value, string? Id)> _roleRows = new();
    readonly List<(SensorInfo Sensor, CheckBox Box)> _extraRows = new();
    readonly TextBox _name, _cpu, _gpu;
    readonly Label _extraCount;
    readonly ToggleSwitch _autostart;
    readonly bool _autostartWas;
    readonly bool _keep;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    const int MaxExtras = 4;

    public SetupForm(HubService hub)
    {
        _hub = hub;
        _sensors = hub.Hardware.Snapshot();
        // Si ya se configuro antes, lo elegido manda: lo desmarcado sigue desmarcado y los nombres no se tocan.
        // La deteccion automatica solo completa que sensor usaria cada dato si lo vuelves a marcar.
        _keep = hub.Config.SetupDone;
        _roles = hub.Hardware.AutoDetect();
        foreach (var (k, v) in hub.Config.Roles) _roles[k] = v;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "PixDeck Hub · Bienvenido";
        ClientSize = new Size(960, 660);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(Ui.Label("¡BIENVENIDO!", 24, 14, Ui.Text, Ui.Black(20)));
        Controls.Add(Ui.Label("Marca los sensores que quieres ver en tu PIXDECK. Lo puedes cambiar cuando quieras en Settings.",
                              26, 54, Ui.Dim, Ui.Font(9.5f)));

        // Nombres
        var c = hub.Config;
        Controls.Add(Ui.Label("Nombre del equipo", 26, 90, Ui.Dim, Ui.Font(9)));
        _name = new TextBox { Left = 26, Top = 110, Width = 200, MaxLength = 16, Text = c.DeviceName };
        Controls.Add(Ui.Label("Etiqueta CPU", 246, 90, Ui.Dim, Ui.Font(9)));
        _cpu = new TextBox { Left = 246, Top = 110, Width = 200, MaxLength = 16, Text = !_keep && c.CpuLabel == "CPU" ? Suggest(HardwareType.Cpu) ?? "CPU" : c.CpuLabel };
        Controls.Add(Ui.Label("Etiqueta GPU", 466, 90, Ui.Dim, Ui.Font(9)));
        _gpu = new TextBox { Left = 466, Top = 110, Width = 200, MaxLength = 16, Text = !_keep && c.GpuLabel == "GPU" ? SuggestGpu() ?? "GPU" : c.GpuLabel };
        Controls.AddRange(new Control[] { _name, _cpu, _gpu });

        // ---- Datos principales ----
        Controls.Add(Ui.Label("DATOS PRINCIPALES", 26, 156, Ui.Violet, Ui.Semi(11)));
        var left = new Panel { Left = 24, Top = 182, Width = 446, Height = 392, AutoScroll = true, BackColor = Ui.Panel };
        int y = 10;
        foreach (var role in Roles.All)
        {
            _roles.TryGetValue(role.Key, out var id);
            var s = id == null ? null : _sensors.FirstOrDefault(z => z.Id == id);
            bool configured = c.Roles.ContainsKey(role.Key);
            var box = new CheckBox
            {
                Left = 12, Top = y, Width = 190, Text = role.Title.Split(" (")[0],
                Checked = _keep ? configured : s != null,
                Enabled = s != null || configured,
            };
            string info = s != null ? $"{s.HardwareName} › {s.Name}"
                        : configured ? "no disponible ahora (se mantiene)" : "no detectado";
            var val = Ui.Label(info, 206, y + 3, s == null ? Color.FromArgb(90, 96, 120) : Ui.Dim, Ui.Font(8.5f), 220);
            val.AutoEllipsis = true;
            left.Controls.Add(box);
            left.Controls.Add(val);
            _roleRows.Add((role, box, val, s?.Id ?? (configured ? id : null)));
            y += 29;
        }
        Controls.Add(left);

        // ---- Datos extra ----
        Controls.Add(Ui.Label($"DATOS EXTRA (hasta {MaxExtras})", 492, 156, Ui.Cyan, Ui.Semi(11)));
        _extraCount = Ui.Label("", 700, 159, Ui.Dim, Ui.Font(9));
        Controls.Add(_extraCount);
        var right = new Panel { Left = 490, Top = 182, Width = 446, Height = 392, AutoScroll = true, BackColor = Ui.Panel };
        BuildExtras(right);
        Controls.Add(right);

        // ---- Botones ----
        var skip = new NeonButton { Left = 490, Top = 594, Width = 230, Height = 48, Title = "Omitir (detección automática)", TitleSize = 10, Accent = Ui.Border, Accent2 = Ui.Dim };
        var save = new NeonButton { Left = 730, Top = 594, Width = 206, Height = 48, Title = "Guardar y empezar", TitleSize = 11.5f, Accent = Ui.Ok, Accent2 = Ui.Cyan };
        _autostartWas = Autostart.IsEnabled();
        _autostart = new ToggleSwitch
        {
            Left = 24, Top = 594, Width = 446, Height = 48, Title = "Iniciar con Windows",
            Subtitle = "En segundo plano, solo el icono en la bandeja",
            Checked = _autostartWas || !hub.Config.SetupDone,   // la primera vez viene activado
        };
        Controls.Add(_autostart);
        skip.Click += (_, _) => Finish(false);
        save.Click += (_, _) => Finish(true);
        Controls.Add(skip);
        Controls.Add(save);

        Ui.Style(this);
        ResumeLayout(false);
        foreach (var p in new[] { left, right })
            p.HandleCreated += (_, _) => { try { SetWindowTheme(p.Handle, "DarkMode_Explorer", null); } catch { } };
        UpdateExtraCount();

        _timer.Tick += (_, _) => RefreshValues();
        _timer.Start();
        RefreshValues();
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string app, string? idList);

    // Sensores interesantes para "extra": ventiladores, temperaturas (discos, placa...), potencia y red.
    void BuildExtras(Panel p)
    {
        var used = _roles.Values.ToHashSet();
        // Los extras ya elegidos se muestran SIEMPRE (aunque ahora no tengan valor), marcados y arriba.
        var chosen = _hub.Config.Extras.Where(e => !string.IsNullOrEmpty(e.Label) && !string.IsNullOrEmpty(e.SensorId)).ToList();
        var chosenIds = chosen.Select(e => e.SensorId).ToHashSet();
        var chosenSensors = chosen.Select(e => _sensors.FirstOrDefault(s => s.Id == e.SensorId)
                                       ?? new SensorInfo(e.SensorId, e.Label, SensorType.Factor, "", HardwareType.Motherboard, null))
                                  .ToList();
        var candidates = _sensors.Where(s =>
                !chosenIds.Contains(s.Id) && !used.Contains(s.Id) && s.Value.HasValue &&
                (s.Type is SensorType.Fan && s.Value > 0 ||
                 s.Type is SensorType.Temperature or SensorType.Throughput ||
                 s.Type is SensorType.Power && s.HardwareType is not (HardwareType.Cpu)))
            .GroupBy(s => s.HardwareName)
            .Select(g => (Title: g.Key, Items: g.ToList()))
            .ToList();
        if (chosenSensors.Count > 0) candidates.Insert(0, ("YA ELEGIDOS", chosenSensors));

        int y = 8;
        foreach (var (title, items) in candidates)
        {
            var head = Ui.Label(title, 10, y, Ui.Violet, Ui.Semi(9), 400);
            head.AutoEllipsis = true;
            p.Controls.Add(head);
            y += 22;
            foreach (var s in items)
            {
                var box = new CheckBox { Left = 16, Top = y, Width = 400, Text = $"{s.Name}  ·  {s.ValueText}", Checked = chosenIds.Contains(s.Id) };
                box.CheckedChanged += (_, _) => UpdateExtraCount();
                p.Controls.Add(box);
                _extraRows.Add((s, box));
                y += 26;
            }
            y += 6;
        }
        if (_extraRows.Count == 0)
            p.Controls.Add(Ui.Label("No se encontraron sensores extra (ventiladores, discos, red...).", 12, 12, Ui.Dim, Ui.Font(9)));
    }

    void UpdateExtraCount()
    {
        int n = _extraRows.Count(r => r.Box.Checked);
        _extraCount.Text = $"{n} / {MaxExtras} elegidos";
        foreach (var (_, box) in _extraRows) box.Enabled = box.Checked || n < MaxExtras;
    }

    void RefreshValues()
    {
        foreach (var r in _roleRows)
        {
            if (r.Id == null) continue;
            var s = _sensors.FirstOrDefault(z => z.Id == r.Id);
            if (s == null) continue;   // sensor no disponible ahora: se mantiene igual
            var v = _hub.Hardware.Value(r.Id);
            r.Value.Text = $"{s.HardwareName} › {s.Name}  ·  {(v is float f ? $"{f:0.#} {s.Unit}" : "--")}";
        }
        foreach (var (s, box) in _extraRows)
        {
            var v = _hub.Hardware.Value(s.Id);
            var unit = s.Type == SensorType.Factor ? "" : s.Unit;
            box.Text = $"{s.Name}  ·  {(v is float f ? $"{f:0.#} {unit}" : "--")}";
        }
    }

    // ---------------- sugerencias de nombre ----------------
    string? Suggest(HardwareType t) => Shorten(_sensors.FirstOrDefault(s => s.HardwareType == t)?.HardwareName);

    string? SuggestGpu()
    {
        var name = _sensors.Where(s => s.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                           .OrderBy(s => s.HardwareType == HardwareType.GpuIntel ? 1 : 0)
                           .Select(s => s.HardwareName).FirstOrDefault();
        return Shorten(name);
    }

    static string? Shorten(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return null;
        foreach (var junk in new[] { "NVIDIA", "GeForce", "AMD", "Radeon", "Intel(R)", "Intel", "Core(TM)", "(R)", "(TM)", "Processor", "CPU", "Graphics" })
            n = n.Replace(junk, " ", StringComparison.OrdinalIgnoreCase);
        var words = n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.Contains("-Core", StringComparison.OrdinalIgnoreCase));
        var r = HubService.Clean(string.Join(' ', words), 16).ToUpperInvariant();
        return r.Length == 0 ? null : r;
    }

    // ---------------- guardar ----------------
    void Finish(bool apply)
    {
        var c = _hub.Config.Clone();
        c.SetupDone = true;
        if (apply)
        {
            c.DeviceName = string.IsNullOrWhiteSpace(_name.Text) ? "PIXDECK" : _name.Text.Trim();
            c.CpuLabel = _cpu.Text.Trim();
            c.GpuLabel = _gpu.Text.Trim();

            c.Roles = new Dictionary<string, string>();
            foreach (var r in _roleRows)
                if (r.Box.Checked && r.Id != null) c.Roles[r.Role.Key] = r.Id;
            c.RolesInitialized = true;

            var picks = _extraRows.Where(r => r.Box.Checked).Take(MaxExtras).ToList();
            for (int i = 0; i < 4; i++)
            {
                if (i < picks.Count)
                {
                    var s = picks[i].Sensor;
                    // si ya estaba elegido, se respeta su etiqueta/unidad/maximo (pudo editarse en Settings)
                    var prev = _hub.Config.Extras.FirstOrDefault(e => e.SensorId == s.Id && !string.IsNullOrEmpty(e.Label));
                    c.Extras[i] = prev != null ? new ExtraConfig { SensorId = prev.SensorId, Label = prev.Label, Unit = prev.Unit, Max = prev.Max } : new ExtraConfig
                    {
                        SensorId = s.Id,
                        Label = HubService.Clean(s.Name, 11).ToUpperInvariant(),
                        Unit = HubService.Clean(s.Unit.Replace("°", ""), 5),
                        Max = s.Type is SensorType.Load or SensorType.Control or SensorType.Level ? 100 : 0,
                    };
                }
                else c.Extras[i] = new ExtraConfig();
            }
        }
        if (_autostart.Checked != _autostartWas && !Autostart.Set(_autostart.Checked))
            MessageBox.Show(this, "No se pudo cambiar el inicio con Windows. ¿El programa corre como administrador?",
                            "PixDeck Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _hub.NeedsSetup = false;
        _hub.ApplyConfig(c);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
