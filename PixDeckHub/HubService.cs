using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PixDeckHub;

/// <summary>Coordina sensores + USB: lee LibreHardwareMonitor y manda los datos al ESP32.</summary>
public sealed class HubService : IDisposable
{
    public AppConfig Config { get; private set; }
    public HardwareService Hardware { get; } = new();
    public DeviceLink Link { get; } = new();

    public string Status { get; private set; } = "Iniciando...";
    /// <summary>Primera vez que se abre: mostrar el asistente de sensores.</summary>
    public bool NeedsSetup { get; set; }
    public event Action? StatusChanged;
    public event Action<int>? ThemeChangedOnDevice;

    Thread? _loop;
    volatile bool _stop;
    readonly AutoResetEvent _wake = new(false);
    long _nextConnectTry;

    // temas subidos
    readonly object _ctLock = new();
    readonly Dictionary<int, uint> _deviceThemes = new();
    readonly ManualResetEventSlim _ctDone = new(false);
    readonly Dictionary<int, CustomTheme?> _themeCache = new();
    volatile bool _syncPending;
    public static string ThemesFolder => Path.Combine(AppConfig.Folder, "temas");

    public HubService()
    {
        Config = AppConfig.Load();
        NeedsSetup = !Config.SetupDone;
        ThemePreview.CustomResolver = t => GetCustom(t - ThemeCatalog.Count);
        ThemePreview.Warn = Config.Warn;
        ThemePreview.Alert = Config.AlertOn;
        Link.LineReceived += OnLine;
        Link.StatusChanged += () =>
        {
            if (!Link.Connected) SetStatus("Equipo desconectado, buscando...");
        };
    }

    /// <summary>Inicializa sensores (lento: 1-5 s). Se llama una vez, en segundo plano.</summary>
    public void InitHardware()
    {
        SetStatus("Iniciando sensores...");
        Hardware.Open();
        if (!Config.RolesInitialized && Hardware.IsOpen)
        {
            Config.Roles = Hardware.AutoDetect();
            Config.RolesInitialized = true;
            Config.Save();
        }
    }

    public void Start()
    {
        _loop = new Thread(Loop) { IsBackground = true, Name = "PixDeckHub" };
        _loop.Start();
    }

    void Loop()
    {
        var sw = Stopwatch.StartNew();
        while (!_stop)
        {
            if (!Link.Connected && sw.ElapsedMilliseconds >= _nextConnectTry)
            {
                SetStatus("Buscando el equipo por USB...");
                if (Link.TryConnect(Config.Port))
                {
                    SetStatus("Sincronizando temas...");
                    SyncCustomThemes();
                    PushConfig();
                    SetStatus($"Conectado en {Link.PortName} · firmware {Link.Firmware}");
                }
                else
                {
                    _nextConnectTry = sw.ElapsedMilliseconds + 4000;
                    SetStatus(Config.Port == "AUTO"
                        ? "No se encontró el equipo. Reintentando..."
                        : $"No responde {Config.Port}. Reintentando...");
                }
            }

            if (Link.Connected && _syncPending)
            {
                _syncPending = false;
                var prev = Status;
                SetStatus("Subiendo temas al equipo...");
                SyncCustomThemes();
                PushConfig();
                SetStatus(prev);
            }

            Hardware.Update();
            if (Link.Connected) Link.Send(BuildDataLine());

            _wake.WaitOne(Config.IntervalMs);
        }
    }

    void SetStatus(string s)
    {
        Status = s;
        StatusChanged?.Invoke();
    }

    void OnLine(string line)
    {
        // EVT|THEME|n => el usuario apreto el boton BOOT del equipo
        var f = line.Split('|');
        if (f.Length >= 3 && f[0] == "EVT" && f[1] == "THEME" && int.TryParse(f[2], out var t))
        {
            if (IsValidTheme(t)) Config.Theme = t;
            Config.Save();
            ThemeChangedOnDevice?.Invoke(Config.Theme);
        }
        // CT|slot|hash|nombre ... CT|END  => temas que el equipo tiene guardados
        else if (f.Length >= 2 && f[0] == "CT")
        {
            if (f[1] == "END") _ctDone.Set();
            else if (f.Length >= 3 && int.TryParse(f[1], out var slot) && uint.TryParse(f[2], out var hash))
                lock (_ctLock) _deviceThemes[slot] = hash;
        }
    }

    // ---------------- temas: lista, nombres, subida ----------------
    public bool IsValidTheme(int t) =>
        t >= 0 && (t < ThemeCatalog.Count || Config.CustomThemes.Any(c => ThemeCatalog.Count + c.Slot == t));

    public List<(int Index, string Name, string Description, bool Custom)> ThemeList()
    {
        var l = new List<(int, string, string, bool)>();
        for (int i = 0; i < ThemeCatalog.Count; i++) l.Add((i, ThemeCatalog.Items[i].Name, ThemeCatalog.Items[i].Description, false));
        foreach (var c in Config.CustomThemes.OrderBy(c => c.Slot).ToList())
            l.Add((ThemeCatalog.Count + c.Slot, c.Name.ToUpperInvariant(),
                   string.IsNullOrWhiteSpace(c.Description) ? "Tema subido (.pxtheme)" : c.Description, true));
        return l;
    }

    public string ThemeName(int t) =>
        ThemeList().FirstOrDefault(x => x.Index == t).Name ?? "?";

    public int NextTheme()
    {
        var l = ThemeList();
        int i = l.FindIndex(x => x.Index == Config.Theme);
        return l[(i + 1) % l.Count].Index;
    }

    public CustomTheme? GetCustom(int slot)
    {
        lock (_themeCache)
        {
            if (_themeCache.TryGetValue(slot, out var c)) return c;
            var r = Config.CustomThemes.FirstOrDefault(x => x.Slot == slot);
            CustomTheme? t = null;
            try { if (r != null) t = CustomTheme.Load(Path.Combine(ThemesFolder, r.File)); } catch { }
            _themeCache[slot] = t;
            return t;
        }
    }

    /// <summary>Importa un .pxtheme: lo copia a la biblioteca y lo sube al equipo. Devuelve el indice de tema.</summary>
    public int ImportTheme(string path)
    {
        var theme = CustomTheme.Load(path);   // lanza InvalidDataException con el motivo
        var cleanName = Clean(theme.Name, 16);

        // Mismo nombre que uno ya subido => se actualiza ese (asi el creador puede re-publicar)
        var existing = Config.CustomThemes.FirstOrDefault(c => c.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
        int slot = existing?.Slot ?? Enumerable.Range(0, ThemeCatalog.MaxCustom)
            .Where(s => Config.CustomThemes.All(c => c.Slot != s)).DefaultIfEmpty(-1).First();
        if (slot < 0) throw new InvalidOperationException($"El equipo admite hasta {ThemeCatalog.MaxCustom} temas subidos. Elimina uno primero.");

        Directory.CreateDirectory(ThemesFolder);
        var safe = string.Concat(Clean(theme.Name, 24).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var file = $"slot{slot}_{safe}.pxtheme";
        var dest = Path.Combine(ThemesFolder, file);
        if (!Path.GetFullPath(path).Equals(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            File.Copy(path, dest, true);
        if (existing != null)
        {
            if (existing.File != file) try { File.Delete(Path.Combine(ThemesFolder, existing.File)); } catch { }
            Config.CustomThemes.Remove(existing);
        }

        Config.CustomThemes.Add(new CustomThemeRef
        {
            Slot = slot, File = file, Name = cleanName, Description = theme.Description,
        });
        lock (_themeCache) _themeCache.Remove(slot);
        Config.Save();
        _syncPending = true;
        _wake.Set();
        return ThemeCatalog.Count + slot;
    }

    public void DeleteTheme(int themeIndex)
    {
        int slot = themeIndex - ThemeCatalog.Count;
        var r = Config.CustomThemes.FirstOrDefault(c => c.Slot == slot);
        if (r == null) return;
        Config.CustomThemes.Remove(r);
        try { File.Delete(Path.Combine(ThemesFolder, r.File)); } catch { }
        lock (_themeCache) _themeCache.Remove(slot);
        if (Config.Theme == themeIndex) Config.Theme = 0;
        Config.Save();
        Link.Send($"U|{slot}|DEL");
        Link.Send($"T|{Config.Theme}");
    }

    /// <summary>Compara los temas del equipo con la biblioteca y sube/borra lo que haga falta.</summary>
    void SyncCustomThemes()
    {
        lock (_ctLock) _deviceThemes.Clear();
        _ctDone.Reset();
        if (!Link.Send("Q") || !_ctDone.Wait(2000)) return;   // firmware viejo: no soporta temas subidos

        Dictionary<int, uint> device;
        lock (_ctLock) device = new Dictionary<int, uint>(_deviceThemes);
        var refs = Config.CustomThemes.ToList();

        foreach (var r in refs)
        {
            var theme = GetCustom(r.Slot);
            if (theme == null) continue;
            if (device.TryGetValue(r.Slot, out var h) && h == theme.Encode().Hash) continue;
            if (!UploadSlot(r.Slot, theme)) return;
        }
        foreach (var slot in device.Keys.Where(s => refs.All(r => r.Slot != s)))
            Link.Send($"U|{slot}|DEL");
    }

    // El ESP32 tiene un solo buffer de subida: nunca dos subidas a la vez.
    readonly object _uploadLock = new();
    const int PreviewSlot = ThemeCatalog.MaxCustom;

    bool UploadSlot(int slot, CustomTheme theme)
    {
        lock (_uploadLock)
        {
            var (lines, hash) = theme.Encode();
            if (!Link.Send($"U|{slot}|BEGIN|{hash}|{Clean(theme.Name, 16)}")) return false;
            foreach (var l in lines)
            {
                if (!Link.Send($"U|{slot}|W|{l}")) return false;
                Thread.Sleep(6);   // no llenar el buffer del ESP32 mientras dibuja
            }
            Link.Send($"U|{slot}|END");
            Thread.Sleep(150);     // escritura en la flash
            return true;
        }
    }

    /// <summary>Creador de temas: muestra el tema en el equipo sin guardarlo en la lista.</summary>
    public bool PreviewOnDevice(CustomTheme theme)
    {
        if (!Link.Connected) return false;
        if (!UploadSlot(PreviewSlot, theme)) return false;
        return Link.Send("P|1");
    }

    public void EndPreview()
    {
        if (Link.Connected) Link.Send("P|0");
    }

    // ---------------- protocolo ----------------
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string Num(float? v) => v is float f ? f.ToString(f >= 100 || f <= -100 ? "0" : "0.#", Inv) : "";

    public float? RoleValue(string key) =>
        Config.Roles.TryGetValue(key, out var id) ? Hardware.Value(id) : null;

    public float?[] CurrentValues()
    {
        float? used = RoleValue("ram_used"), avail = RoleValue("ram_avail");
        float? ramTot = used.HasValue && avail.HasValue ? used + avail : null;
        float? vramTot = RoleValue("vram_total");
        if (vramTot is null or < 256 && Config.VramTotalFallbackMB > 0) vramTot = Config.VramTotalFallbackMB;

        var v = new float?[17];
        v[0] = RoleValue("cpu_temp");
        v[1] = RoleValue("cpu_load");
        v[2] = RoleValue("cpu_clock");
        v[3] = RoleValue("cpu_power");
        v[4] = RoleValue("gpu_temp");
        v[5] = RoleValue("gpu_load");
        v[6] = RoleValue("gpu_clock");
        v[7] = RoleValue("gpu_power");
        v[8] = used;
        v[9] = ramTot;
        v[10] = RoleValue("ram_load");
        v[11] = RoleValue("vram_used");
        v[12] = vramTot;
        for (int i = 0; i < 4; i++)
        {
            var x = Config.Extras[i];
            v[13 + i] = string.IsNullOrWhiteSpace(x.Label) ? null : Hardware.Value(x.SensorId);
        }
        return v;
    }

    string BuildDataLine()
    {
        var sb = new StringBuilder("D|");
        sb.Append(DateTime.Now.ToString("HHmmss", Inv));
        foreach (var v in CurrentValues()) sb.Append('|').Append(Num(v));
        return sb.ToString();
    }

    public static string Clean(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var n = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c == 'ñ') { sb.Append('n'); continue; }
            if (c == 'Ñ') { sb.Append('N'); continue; }
            if (c >= 0x20 && c <= 0x7E && c != '|') sb.Append(c);
        }
        var r = sb.ToString().Trim();
        return r.Length > max ? r[..max] : r;
    }

    /// <summary>Manda toda la configuracion al equipo (al conectar y al guardar).</summary>
    public void PushConfig()
    {
        var c = Config;
        Link.Send($"N|{Clean(c.DeviceName, 16)}");
        Link.Send($"L|{Clean(c.CpuLabel, 16)}|{Clean(c.GpuLabel, 16)}");
        for (int i = 0; i < 4; i++)
        {
            var x = c.Extras[i];
            Link.Send($"X|{i}|{Clean(x.Label, 11)}|{Clean(x.Unit, 5)}|{x.Max.ToString("0.#", Inv)}");
        }
        Link.Send($"S|{c.Theme}|{c.RotateSec}|{c.AlertOn}|{c.AlertOff}|{c.Warn}|{c.SoundMask}");
        Link.Send($"Z|{c.SoundBootSel}|{c.SoundConnectSel}|{c.SoundThemeSel}|{c.SoundAlarmSel}|{c.AlarmRepeatSec}");
    }

    public void ApplyConfig(AppConfig newCfg)
    {
        bool portChanged = newCfg.Port != Config.Port;
        newCfg.Normalize();
        Config = newCfg;
        Config.Save();
        ThemePreview.Warn = Config.Warn;
        ThemePreview.Alert = Config.AlertOn;
        if (portChanged)
        {
            Link.Disconnect(true);
            _nextConnectTry = 0;
        }
        else if (Link.Connected) PushConfig();
        _wake.Set();
    }

    public void SetTheme(int theme)
    {
        Config.Theme = IsValidTheme(theme) ? theme : 0;
        Config.Save();
        Link.Send($"T|{Config.Theme}");
    }

    public void TestBuzzer() => Link.Send("B");

    /// <summary>Toca una melodia en el equipo (boton ▶ de Settings), aunque el evento este apagado.</summary>
    public bool PlaySound(int ev, int idx) => Link.Connected && Link.Send($"Y|{ev}|{idx}");

    public void Reconnect()
    {
        Link.Disconnect(true);
        _nextConnectTry = 0;
        _wake.Set();
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        _loop?.Join(2000);
        Link.Dispose();
        Hardware.Dispose();
    }
}

/// <summary>Inicio con Windows mediante el Programador de tareas (corre como admin sin aviso UAC).</summary>
public static class Autostart
{
    const string TaskName = "PixDeckHub";

    static int Run(string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) return -1;
            p.WaitForExit(10000);
            return p.ExitCode;
        }
        catch { return -1; }
    }

    public static bool IsEnabled() => Run($"/Query /TN \"{TaskName}\"") == 0;

    public static bool Set(bool enable)
    {
        if (!enable) return Run($"/Delete /TN \"{TaskName}\" /F") == 0 || !IsEnabled();
        var exe = Environment.ProcessPath ?? Application.ExecutablePath;
        return Run($"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --autostart\" /SC ONLOGON /RL HIGHEST /DELAY 0000:15 /F") == 0;
    }
}
