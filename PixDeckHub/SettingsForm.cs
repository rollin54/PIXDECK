using System.IO.Ports;
using LibreHardwareMonitor.Hardware;

namespace PixDeckHub;

/// <summary>SETTINGS: todo lo que se manda al equipo (nombre, sensores, extras, alertas, puerto).</summary>
public sealed class SettingsForm : DarkForm
{
    sealed record Item(string Id, string Text)
    {
        public override string ToString() => Text;
    }

    readonly HubService _hub;
    readonly AppConfig _cfg;
    readonly List<SensorInfo> _sensors;
    readonly Panel _content;
    readonly Dictionary<string, Panel> _pages = new();
    readonly List<NeonButton> _nav = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    // General
    TextBox _name = null!, _cpuLabel = null!, _gpuLabel = null!;
    ComboBox _port = null!;
    NumericUpDown _interval = null!, _vramFallback = null!;
    CheckBox _autostart = null!;
    bool _autostartWas;
    // Sensores
    readonly Dictionary<string, (ComboBox Combo, Label Value)> _roleRows = new();
    // Extras
    readonly (ComboBox Combo, TextBox Label, TextBox Unit, NumericUpDown Max, Label Value)[] _extraRows =
        new (ComboBox, TextBox, TextBox, NumericUpDown, Label)[4];
    // Alertas
    NumericUpDown _alertOn = null!, _alertOff = null!, _warn = null!;
    CheckBox _sndBoot = null!, _sndConn = null!, _sndTheme = null!, _sndAlarm = null!;
    ComboBox _selBoot = null!, _selConn = null!, _selTheme = null!, _selAlarm = null!, _repeat = null!;

    public SettingsForm(HubService hub)
    {
        _hub = hub;
        _cfg = hub.Config.Clone();
        _sensors = hub.Hardware.Snapshot();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "PixDeck Hub · Settings";
        ClientSize = new Size(940, 620);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var title = Ui.Label("SETTINGS", 24, 16, Ui.Text, Ui.Black(20));
        var sub = Ui.Label("Todo lo que ves en la pantalla del equipo se configura aquí.", 26, 56, Ui.Dim, Ui.Font(9.5f));
        Controls.Add(title);
        Controls.Add(sub);

        _content = new Panel { Left = 214, Top = 90, Width = 704, Height = 450, BackColor = Ui.Panel };
        Controls.Add(_content);

        AddPage("general", "General", "Nombre y conexión", BuildGeneral());
        AddPage("sensores", "Sensores", "Qué dato va a cada lugar", BuildSensors());
        AddPage("extras", "Datos extra", "Ventiladores, discos, red...", BuildExtras());
        AddPage("alertas", "Alertas", "Temperaturas y buzzer", BuildAlerts());

        var save = new NeonButton { Left = 618, Top = 556, Width = 150, Height = 46, Title = "Guardar", TitleSize = 11, Accent = Ui.Ok, Accent2 = Ui.Cyan };
        var cancel = new NeonButton { Left = 778, Top = 556, Width = 140, Height = 46, Title = "Cancelar", TitleSize = 11, Accent = Ui.Border, Accent2 = Ui.Dim };
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        Controls.Add(save);
        Controls.Add(cancel);

        Ui.Style(this);
        ResumeLayout(false);
        ShowPage("general");

        _timer.Tick += (_, _) => RefreshValues();
        _timer.Start();
        RefreshValues();
    }

    void AddPage(string key, string title, string subtitle, Panel page)
    {
        page.Dock = DockStyle.Fill;
        page.BackColor = Ui.Panel;
        page.Visible = false;
        _content.Controls.Add(page);
        _pages[key] = page;
        var b = new NeonButton
        {
            Left = 24, Top = 90 + _nav.Count * 72, Width = 172, Height = 62, Title = title, Subtitle = subtitle, TitleSize = 11,
            Tag = key,
        };
        b.Click += (_, _) => ShowPage(key);
        _nav.Add(b);
        Controls.Add(b);
    }

    void ShowPage(string key)
    {
        foreach (var (k, p) in _pages) p.Visible = k == key;
        foreach (var b in _nav) { b.Selected = (string)b.Tag! == key; b.Invalidate(); }
    }

    static Label Hint(string t, int x, int y, int w = 640) =>
        new() { Text = t, Left = x, Top = y, Width = w, Height = 36, ForeColor = Ui.Dim, Font = Ui.Font(9), BackColor = Color.Transparent };

    // ---------------- General ----------------
    Panel BuildGeneral()
    {
        var p = new Panel();
        int y = 20;
        TextBox Field(string label, string value, int max, string hint)
        {
            p.Controls.Add(Ui.Label(label, 24, y + 4, Ui.Text, Ui.Semi(10)));
            var t = new TextBox { Left = 220, Top = y, Width = 260, Text = value, MaxLength = max };
            p.Controls.Add(t);
            p.Controls.Add(Ui.Label(hint, 492, y + 5, Ui.Dim, Ui.Font(8.5f)));
            y += 44;
            return t;
        }
        _name = Field("Nombre del equipo", _cfg.DeviceName, 16, "Se ve en el encabezado");
        _cpuLabel = Field("Etiqueta CPU", _cfg.CpuLabel, 16, "Ej: RYZEN 9600X");
        _gpuLabel = Field("Etiqueta GPU", _cfg.GpuLabel, 16, "Ej: RTX 3070 Ti");

        p.Controls.Add(Ui.Label("Puerto USB", 24, y + 4, Ui.Text, Ui.Semi(10)));
        _port = new ComboBox { Left = 220, Top = y, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _port.Items.Add("AUTO");
        foreach (var n in SerialPort.GetPortNames().Distinct().OrderBy(n => n)) _port.Items.Add(n);
        if (!_port.Items.Contains(_cfg.Port)) _port.Items.Add(_cfg.Port);
        _port.SelectedItem = _cfg.Port;
        p.Controls.Add(_port);
        p.Controls.Add(Ui.Label($"Actual: {_hub.Link.PortName ?? "sin conexión"}", 392, y + 5, Ui.Dim, Ui.Font(8.5f)));
        y += 44;

        p.Controls.Add(Ui.Label("Envío de datos (ms)", 24, y + 4, Ui.Text, Ui.Semi(10)));
        _interval = new NumericUpDown { Left = 220, Top = y, Width = 100, Minimum = 250, Maximum = 5000, Increment = 250, Value = _cfg.IntervalMs };
        p.Controls.Add(_interval);
        p.Controls.Add(Ui.Label("1000 = una vez por segundo", 332, y + 5, Ui.Dim, Ui.Font(8.5f)));
        y += 44;

        p.Controls.Add(Ui.Label("VRAM total (MB)", 24, y + 4, Ui.Text, Ui.Semi(10)));
        _vramFallback = new NumericUpDown { Left = 220, Top = y, Width = 100, Minimum = 0, Maximum = 131072, Increment = 1024, Value = _cfg.VramTotalFallbackMB };
        p.Controls.Add(_vramFallback);
        p.Controls.Add(Ui.Label("Solo si tu GPU no reporta el total (0 = automático)", 332, y + 5, Ui.Dim, Ui.Font(8.5f)));
        y += 56;

        _autostartWas = Autostart.IsEnabled();
        _autostart = new CheckBox { Left = 24, Top = y, Width = 600, Text = "Iniciar con Windows (en segundo plano, sin ventana, sin aviso de UAC)", Checked = _autostartWas };
        p.Controls.Add(_autostart);
        y += 34;
        p.Controls.Add(Hint("Se crea una tarea en el Programador de tareas de Windows con privilegios elevados, " +
                            "porque LibreHardwareMonitor necesita administrador para leer las temperaturas del CPU.", 24, y));
        return p;
    }

    // ---------------- Sensores ----------------
    IEnumerable<Item> SensorItems(Func<SensorInfo, bool> filter)
    {
        yield return new Item("", "(ninguno)");
        foreach (var s in _sensors.Where(filter)) yield return new Item(s.Id, s.Display);
    }

    static void Select(ComboBox cb, string? id)
    {
        cb.SelectedIndex = 0;
        if (string.IsNullOrEmpty(id)) return;
        for (int i = 0; i < cb.Items.Count; i++)
            if (((Item)cb.Items[i]!).Id == id) { cb.SelectedIndex = i; return; }
        // El sensor elegido no esta disponible ahora: se conserva igual para no perderlo al guardar.
        cb.SelectedIndex = cb.Items.Add(new Item(id, $"(no disponible ahora) {id}"));
    }

    Panel BuildSensors()
    {
        var p = new Panel { AutoScroll = true };
        p.Controls.Add(Hint("Elige qué sensor de LibreHardwareMonitor alimenta cada dato de la pantalla. " +
                            "Lo que dejes en (ninguno) aparece como \"--\".", 24, 12));
        var auto = new NeonButton { Left = 520, Top = 46, Width = 160, Height = 34, Title = "Detectar automático", TitleSize = 9.5f };
        auto.Click += (_, _) =>
        {
            var d = _hub.Hardware.AutoDetect();
            foreach (var (k, row) in _roleRows) Select(row.Combo, d.TryGetValue(k, out var id) ? id : null);
        };
        p.Controls.Add(auto);

        int y = 92;
        foreach (var role in Roles.All)
        {
            p.Controls.Add(Ui.Label(role.Title.Split(" (")[0], 24, y + 4, Ui.Text, Ui.Semi(9.5f)));
            var cb = new ComboBox { Left = 210, Top = y, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 520 };
            cb.Items.AddRange(SensorItems(s => role.Types.Contains(s.Type)).ToArray<object>());
            Select(cb, _cfg.Roles.TryGetValue(role.Key, out var id) ? id : null);
            var val = Ui.Label("--", 582, y + 4, Ui.Cyan, Ui.Semi(9.5f), 96);
            p.Controls.Add(cb);
            p.Controls.Add(val);
            _roleRows[role.Key] = (cb, val);
            y += 38;
        }
        p.Controls.Add(new Label { Left = 0, Top = y, Height = 16, Width = 10 });   // margen al final del scroll
        return p;
    }

    // ---------------- Extras ----------------
    Panel BuildExtras()
    {
        var p = new Panel();
        p.Controls.Add(Hint("Hasta 4 datos más (ventiladores, temperatura de disco, red, voltajes...). Cada tema los muestra a su estilo: " +
                            "multímetro, terminal, computadora de viaje, etc. Etiqueta vacía = desactivado.", 24, 12, 650));
        int y = 64;
        p.Controls.Add(Ui.Label("Sensor", 24, y, Ui.Dim, Ui.Font(8.5f)));
        p.Controls.Add(Ui.Label("Etiqueta (11)", 400, y, Ui.Dim, Ui.Font(8.5f)));
        p.Controls.Add(Ui.Label("Unidad", 520, y, Ui.Dim, Ui.Font(8.5f)));
        p.Controls.Add(Ui.Label("Máx. barra", 588, y, Ui.Dim, Ui.Font(8.5f)));
        y += 22;
        for (int i = 0; i < 4; i++)
        {
            var x = _cfg.Extras[i];
            var cb = new ComboBox { Left = 24, Top = y, Width = 364, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 560 };
            cb.Items.AddRange(SensorItems(_ => true).ToArray<object>());
            Select(cb, x.SensorId);
            var lb = new TextBox { Left = 400, Top = y, Width = 110, MaxLength = 11, Text = x.Label };
            var un = new TextBox { Left = 520, Top = y, Width = 58, MaxLength = 5, Text = x.Unit };
            var mx = new NumericUpDown { Left = 588, Top = y, Width = 90, Minimum = 0, Maximum = 100000, DecimalPlaces = 0, Value = (decimal)Math.Clamp(x.Max, 0, 100000) };
            var val = Ui.Label("--", 24, y + 30, Ui.Cyan, Ui.Font(8.5f), 360);
            int idx = i;
            cb.SelectedIndexChanged += (_, _) =>
            {
                var it = (Item)cb.SelectedItem!;
                var s = _sensors.FirstOrDefault(z => z.Id == it.Id);
                if (s == null) { lb.Text = ""; return; }
                lb.Text = HubService.Clean(s.Name, 11).ToUpperInvariant();
                un.Text = HubService.Clean(s.Unit.Replace("°", ""), 5);
                if (s.Type is SensorType.Load or SensorType.Control or SensorType.Level) mx.Value = 100;
            };
            p.Controls.AddRange(new Control[] { cb, lb, un, mx, val });
            _extraRows[idx] = (cb, lb, un, mx, val);
            y += 84;
        }
        return p;
    }

    // ---------------- Alertas ----------------
    Panel BuildAlerts()
    {
        var p = new Panel();
        int y = 24;
        NumericUpDown Num(string label, int value, string hint)
        {
            p.Controls.Add(Ui.Label(label, 24, y + 4, Ui.Text, Ui.Semi(10)));
            var n = new NumericUpDown { Left = 260, Top = y, Width = 90, Minimum = 20, Maximum = 110, Value = Math.Clamp(value, 20, 110) };
            p.Controls.Add(n);
            p.Controls.Add(Ui.Label(hint, 362, y + 5, Ui.Dim, Ui.Font(8.5f)));
            y += 46;
            return n;
        }
        _alertOn = Num("Alerta a partir de (°C)", _cfg.AlertOn, "Suena el buzzer y parpadea la pantalla");
        _alertOff = Num("Se rearma bajo (°C)", _cfg.AlertOff, "Evita que suene una y otra vez");
        _warn = Num("Color ámbar desde (°C)", _cfg.Warn, "Aviso visual sin sonido");
        y += 2;
        p.Controls.Add(Ui.Label("SONIDOS DEL EQUIPO", 24, y, Ui.Violet, Ui.Semi(10)));
        p.Controls.Add(Ui.Label("▶ = escucharlo en el equipo", 500, y + 2, Ui.Dim, Ui.Font(8.5f)));
        y += 28;

        // Cada evento: activar/desactivar + que melodia + probar
        (CheckBox, ComboBox) SndRow(int ev, string text, bool on, int sel)
        {
            var cb = new CheckBox { Left = 24, Top = y + 4, Width = 210, Text = text, Checked = on };
            var combo = new ComboBox { Left = 240, Top = y, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(SoundCatalog.Names[ev]);
            combo.SelectedIndex = Math.Clamp(sel, 0, SoundCatalog.Names[ev].Length - 1);
            var play = new NeonButton { Left = 500, Top = y - 2, Width = 54, Height = 32, Title = "▶", TitleSize = 10, Accent = Ui.Ok, Accent2 = Ui.Cyan };
            play.Click += (_, _) =>
            {
                if (!_hub.PlaySound(ev, combo.SelectedIndex))
                    MessageBox.Show(this, "El equipo no está conectado por USB.", "PixDeck Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            p.Controls.AddRange(new Control[] { cb, combo, play });
            y += 38;
            return (cb, combo);
        }
        (_sndBoot, _selBoot) = SndRow(0, "Encendido", _cfg.SoundBoot, _cfg.SoundBootSel);
        (_sndConn, _selConn) = SndRow(1, "Conexión con la PC", _cfg.SoundConnect, _cfg.SoundConnectSel);
        (_sndTheme, _selTheme) = SndRow(2, "Cambio de tema", _cfg.SoundTheme, _cfg.SoundThemeSel);
        (_sndAlarm, _selAlarm) = SndRow(3, "Alarma de temperatura", _cfg.SoundAlarm, _cfg.SoundAlarmSel);

        p.Controls.Add(Ui.Label("Repetir la alarma", 44, y + 4, Ui.Text, Ui.Font(10)));
        _repeat = new ComboBox { Left = 240, Top = y, Width = 314, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (_, text) in SoundCatalog.Repeat) _repeat.Items.Add(text);
        int ri = Array.FindIndex(SoundCatalog.Repeat, r => r.Sec == _cfg.AlarmRepeatSec);
        _repeat.SelectedIndex = ri < 0 ? 0 : ri;
        p.Controls.Add(_repeat);
        y += 40;
        p.Controls.Add(Ui.Label("La alarma visual (borde rojo y cartel) se muestra siempre, aunque apagues su sonido.", 24, y, Ui.Dim, Ui.Font(8.5f)));
        return p;
    }

    // ---------------- valores en vivo ----------------
    void RefreshValues()
    {
        foreach (var (_, row) in _roleRows)
        {
            var id = (row.Combo.SelectedItem as Item)?.Id;
            var s = string.IsNullOrEmpty(id) ? null : _sensors.FirstOrDefault(z => z.Id == id);
            var v = _hub.Hardware.Value(id);
            row.Value.Text = v is float f && s != null ? $"{f:0.#} {s.Unit}" : "--";
        }
        foreach (var row in _extraRows)
        {
            if (row.Combo == null) continue;
            var id = (row.Combo.SelectedItem as Item)?.Id;
            var v = _hub.Hardware.Value(id);
            row.Value.Text = v is float f ? $"Valor actual: {f:0.##}" : "";
        }
    }

    // ---------------- guardar ----------------
    void Save()
    {
        var c = _cfg;
        c.DeviceName = string.IsNullOrWhiteSpace(_name.Text) ? "PIXDECK" : _name.Text.Trim();
        c.CpuLabel = _cpuLabel.Text.Trim();
        c.GpuLabel = _gpuLabel.Text.Trim();
        c.Port = _port.SelectedItem as string ?? "AUTO";
        c.IntervalMs = (int)_interval.Value;
        c.VramTotalFallbackMB = (int)_vramFallback.Value;

        c.Roles = new Dictionary<string, string>();
        foreach (var (k, row) in _roleRows)
            if (row.Combo.SelectedItem is Item it && it.Id != "") c.Roles[k] = it.Id;
        c.RolesInitialized = true;

        for (int i = 0; i < 4; i++)
        {
            var row = _extraRows[i];
            var it = row.Combo.SelectedItem as Item;
            c.Extras[i] = new ExtraConfig
            {
                SensorId = it?.Id ?? "",
                Label = it == null || it.Id == "" ? "" : row.Label.Text.Trim(),
                Unit = row.Unit.Text.Trim(),
                Max = (float)row.Max.Value,
            };
        }

        c.AlertOn = (int)_alertOn.Value;
        c.AlertOff = Math.Min((int)_alertOff.Value, c.AlertOn);
        c.Warn = Math.Min((int)_warn.Value, c.AlertOn);
        c.SoundBoot = _sndBoot.Checked;
        c.SoundConnect = _sndConn.Checked;
        c.SoundTheme = _sndTheme.Checked;
        c.SoundAlarm = _sndAlarm.Checked;
        c.Sound = c.SoundBoot || c.SoundConnect || c.SoundTheme || c.SoundAlarm;
        c.SoundsSplit = true;
        c.SoundBootSel = _selBoot.SelectedIndex;
        c.SoundConnectSel = _selConn.SelectedIndex;
        c.SoundThemeSel = _selTheme.SelectedIndex;
        c.SoundAlarmSel = _selAlarm.SelectedIndex;
        c.AlarmRepeatSec = SoundCatalog.Repeat[Math.Max(0, _repeat.SelectedIndex)].Sec;

        if (_autostart.Checked != _autostartWas && !Autostart.Set(_autostart.Checked))
            MessageBox.Show(this, "No se pudo cambiar el inicio con Windows. ¿El programa corre como administrador?",
                            "PixDeck Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        _hub.ApplyConfig(c);
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
