using System.Text.Json;

namespace PixDeckHub;

public sealed class ExtraConfig
{
    public string SensorId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
    public float Max { get; set; }
}

/// <summary>Configuracion del programa. Se guarda en %APPDATA%\PixDeckHub\config.json.</summary>
public sealed class AppConfig
{
    public string DeviceName { get; set; } = "PIXDECK";
    public string CpuLabel { get; set; } = "CPU";
    public string GpuLabel { get; set; } = "GPU";
    public string Port { get; set; } = "AUTO";
    public int IntervalMs { get; set; } = 1000;
    public int VramTotalFallbackMB { get; set; } = 0;

    public int Theme { get; set; } = 0;
    public int RotateSec { get; set; } = 0;

    public int AlertOn { get; set; } = 70;
    public int AlertOff { get; set; } = 55;
    public int Warn { get; set; } = 60;
    public bool Sound { get; set; } = true;            // version anterior (un solo interruptor)
    public bool SoundBoot { get; set; } = true;        // encendido
    public bool SoundConnect { get; set; } = true;     // conexion con la PC
    public bool SoundTheme { get; set; } = true;       // cambio de tema
    public bool SoundAlarm { get; set; } = true;       // alarma de temperatura
    public bool SoundsSplit { get; set; }

    // Melodia elegida para cada evento (indice en SoundCatalog) y repeticion de la alarma (0 = una vez)
    public int SoundBootSel { get; set; }
    public int SoundConnectSel { get; set; }
    public int SoundThemeSel { get; set; }
    public int SoundAlarmSel { get; set; }
    public int AlarmRepeatSec { get; set; }

    /// <summary>Lo que se manda al ESP32: 16 + bits (1 encendido, 2 conexion, 4 tema, 8 alarma).</summary>
    public int SoundMask => 16 | (SoundBoot ? 1 : 0) | (SoundConnect ? 2 : 0) | (SoundTheme ? 4 : 0) | (SoundAlarm ? 8 : 0);

    public bool RolesInitialized { get; set; }
    public Dictionary<string, string> Roles { get; set; } = new();
    public List<ExtraConfig> Extras { get; set; } = new();
    public List<CustomThemeRef> CustomThemes { get; set; } = new();

    // PIXDECK_CONFIG_DIR permite usar otra carpeta (pruebas) sin tocar la configuracion real.
    public static string Folder =>
        Environment.GetEnvironmentVariable("PIXDECK_CONFIG_DIR") is { Length: > 0 } dir ? dir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixDeckHub");
    static string FilePath => Path.Combine(Folder, "config.json");
    public static bool Exists => File.Exists(FilePath);
    public bool SetupDone { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly object SaveLock = new();

    public static AppConfig Load()
    {
        AppConfig c;
        try
        {
            c = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig()
                : new AppConfig();
        }
        catch { c = new AppConfig(); }
        c.Normalize();
        return c;
    }

    public void Save()
    {
        lock (SaveLock)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
            }
            catch { }
        }
    }

    public void Normalize()
    {
        if (!SoundsSplit)   // configuracion de la version anterior: el interruptor unico vale para los cuatro
        {
            SoundBoot = SoundConnect = SoundTheme = SoundAlarm = Sound;
            SoundsSplit = true;
        }
        while (Extras.Count < 4) Extras.Add(new ExtraConfig());
        if (Extras.Count > 4) Extras.RemoveRange(4, Extras.Count - 4);
        if (Theme < 0 || (Theme >= ThemeCatalog.Count && !CustomThemes.Any(c => ThemeCatalog.Count + c.Slot == Theme)))
            Theme = 0;
        IntervalMs = Math.Clamp(IntervalMs, 250, 5000);
        RotateSec = Math.Clamp(RotateSec, 0, 3600);
    }

    public AppConfig Clone() =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, Json))!;
}

/// <summary>
/// Nombres de las melodias del firmware (pixel_sounds.h). El ORDEN tiene que coincidir con las tablas del ESP32.
/// Evento: 0 encendido, 1 conexion, 2 cambio de tema, 3 alarma.
/// </summary>
public static class SoundCatalog
{
    public static readonly string[][] Names =
    {
        new[] { "Clásico", "Retro 8-bit", "Sci-fi", "Arcade", "Fanfarria", "Pitido simple" },
        new[] { "Doble tono", "Conectado (ascendente)", "Burbuja", "Blip" },
        new[] { "Clic", "Swoosh", "Tic-tac", "Blip doble" },
        new[] { "Bitono", "Sirena", "Urgente", "SOS (Morse)", "Suave" },
    };
    public static readonly (int Sec, string Text)[] Repeat =
    {
        (0, "Una vez"), (15, "Cada 15 s mientras siga caliente"), (30, "Cada 30 s mientras siga caliente"), (60, "Cada 60 s mientras siga caliente"),
    };
}

/// <summary>Tema subido al equipo: slot 0..7 en la flash del ESP32 => indice de tema 8 + slot.</summary>
public sealed class CustomThemeRef
{
    public int Slot { get; set; }
    public string File { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Temas incluidos en el firmware (el orden es el indice que entiende el ESP32).</summary>
public static class ThemeCatalog
{
    public const int MaxCustom = 8;
    public static readonly (string Name, string Description)[] Items =
    {
        ("ELECTRÓNICA", "Placa PCB, chips, osciloscopio, multímetro y barras LED"),
        ("INFORMÁTICA", "Terminal estilo top/htop con barras de texto y cursor"),
        ("AUTOS",       "Tablero deportivo: relojes con aguja, testigos y marcha"),
        ("ANIME",       "Paneles manga, números gigantes, barras HP/MP y velocidad"),
        ("PELÍCULAS",   "Cine clásico: cinta en movimiento, marquesina y boletos"),
        ("PIXDECK",     "El original de PIXDECK V2: gauges, RAM y VRAM"),
        ("PIXDECK RINGS", "Anillos dobles grandes: uso por fuera, temperatura por dentro"),
        ("PIXDECK GRAPH", "Temperatura con gráfica de área y 4 mosaicos con mini anillos"),
    };
    public static int Count => Items.Length;
}
