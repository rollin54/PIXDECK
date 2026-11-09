using LibreHardwareMonitor.Hardware;

namespace PixDeckHub;

public sealed record SensorInfo(string Id, string Name, SensorType Type, string HardwareName,
                                HardwareType HardwareType, float? Value)
{
    public string Unit => Type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Load or SensorType.Control or SensorType.Level => "%",
        SensorType.Clock => "MHz",
        SensorType.Power => "W",
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "MB/s",
        SensorType.Frequency => "Hz",
        _ => ""
    };

    public string ValueText => Value is float v ? $"{v:0.#} {Unit}" : "--";
    public string Display => $"{HardwareName} › {Name}  [{Type}]";
}

/// <summary>Rol = un dato que el ESP32 sabe dibujar (posicion fija en la linea D|...).</summary>
public sealed record RoleDef(string Key, string Title, SensorType[] Types);

public static class Roles
{
    public static readonly RoleDef[] All =
    {
        new("cpu_temp",   "CPU · Temperatura",          new[] { SensorType.Temperature }),
        new("cpu_load",   "CPU · Uso",                  new[] { SensorType.Load }),
        new("cpu_clock",  "CPU · Frecuencia",           new[] { SensorType.Clock }),
        new("cpu_power",  "CPU · Potencia",             new[] { SensorType.Power }),
        new("gpu_temp",   "GPU · Temperatura",          new[] { SensorType.Temperature }),
        new("gpu_load",   "GPU · Uso",                  new[] { SensorType.Load }),
        new("gpu_clock",  "GPU · Frecuencia",           new[] { SensorType.Clock }),
        new("gpu_power",  "GPU · Potencia",             new[] { SensorType.Power }),
        new("ram_used",   "RAM · Usada (GB)",           new[] { SensorType.Data }),
        new("ram_avail",  "RAM · Disponible (GB, para calcular el total)", new[] { SensorType.Data }),
        new("ram_load",   "RAM · Uso %",                new[] { SensorType.Load }),
        new("vram_used",  "VRAM · Usada (MB)",          new[] { SensorType.SmallData, SensorType.Data }),
        new("vram_total", "VRAM · Total (MB)",          new[] { SensorType.SmallData, SensorType.Data }),
    };
}

public sealed class HardwareService : IDisposable
{
    readonly object _lock = new();
    Computer? _pc;
    Dictionary<string, ISensor> _map = new();
    public string? Error { get; private set; }
    public bool IsOpen => _pc != null;

    sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware) sub.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
    readonly UpdateVisitor _visitor = new();

    public void Open()
    {
        try
        {
            var pc = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
                IsControllerEnabled = true,
                IsPsuEnabled = true,
            };
            pc.Open();
            lock (_lock) { _pc = pc; }
            Update();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    public void Update()
    {
        // La lectura de sensores puede tardar cientos de ms: se hace fuera del lock para no
        // trabar la interfaz (solo el hilo del hub llama a Update).
        Computer? pc;
        lock (_lock) pc = _pc;
        if (pc == null) return;
        try { pc.Accept(_visitor); } catch (Exception ex) { Error = ex.Message; }
        var map = new Dictionary<string, ISensor>();
        try { foreach (var s in EnumerateSensors(pc)) map[s.Identifier.ToString()] = s; } catch { return; }
        lock (_lock) _map = map;
    }

    static IEnumerable<ISensor> EnumerateSensors(Computer pc)
    {
        foreach (var hw in pc.Hardware)
        {
            foreach (var s in hw.Sensors) yield return s;
            foreach (var sub in hw.SubHardware)
                foreach (var s in sub.Sensors) yield return s;
        }
    }

    public float? Value(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        lock (_lock)
        {
            if (!_map.TryGetValue(id, out var s)) return null;
            return Scale(s);
        }
    }

    // Red: LHM entrega B/s; se muestra en MB/s para que quepa en la pantalla.
    static float? Scale(ISensor s)
    {
        var v = s.Value;
        if (v is not float f || float.IsNaN(f) || float.IsInfinity(f)) return null;
        return s.SensorType == SensorType.Throughput ? f / 1048576f : f;
    }

    public List<SensorInfo> Snapshot()
    {
        lock (_lock)
        {
            return _map.Values
                .Select(s => new SensorInfo(s.Identifier.ToString(), s.Name, s.SensorType, s.Hardware.Name,
                                            s.Hardware.HardwareType, Scale(s)))
                .OrderBy(s => s.HardwareType).ThenBy(s => s.HardwareName).ThenBy(s => s.Type).ThenBy(s => s.Name)
                .ToList();
        }
    }

    // ---------------- deteccion automatica de los sensores tipicos ----------------
    public Dictionary<string, string> AutoDetect()
    {
        var all = Snapshot();
        var r = new Dictionary<string, string>();

        static bool IsGpu(HardwareType t) => t is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
        // GPU dedicada antes que la integrada
        var gpuName = all.Where(s => IsGpu(s.HardwareType))
                         .OrderBy(s => s.HardwareType == HardwareType.GpuIntel ? 1 : 0)
                         .Select(s => s.HardwareName).FirstOrDefault();

        string? Pick(Func<SensorInfo, bool> hw, SensorType t, params string[] prefer)
        {
            var c = all.Where(s => hw(s) && s.Type == t).ToList();
            foreach (var p in prefer)
            {
                var m = c.FirstOrDefault(s => s.Name.Equals(p, StringComparison.OrdinalIgnoreCase))
                     ?? c.FirstOrDefault(s => s.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (m != null) return m.Id;
            }
            return c.FirstOrDefault()?.Id;
        }
        void Set(string key, string? id) { if (id != null) r[key] = id; }

        Func<SensorInfo, bool> cpu = s => s.HardwareType == HardwareType.Cpu;
        Func<SensorInfo, bool> gpu = s => s.HardwareName == gpuName;
        Func<SensorInfo, bool> ram = s => s.HardwareType == HardwareType.Memory &&
                                          !s.HardwareName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                          !s.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase);

        Set("cpu_temp", Pick(cpu, SensorType.Temperature, "Core (Tctl/Tdie)", "CPU Package", "Package", "Tctl", "Core Average"));
        Set("cpu_load", Pick(cpu, SensorType.Load, "CPU Total", "Total"));
        Set("cpu_clock", Pick(s => cpu(s) && !s.Name.Contains("Bus"), SensorType.Clock, "Cores (Average)", "Core #1"));
        Set("cpu_power", Pick(cpu, SensorType.Power, "Package", "CPU Package", "Cores"));
        if (gpuName != null)
        {
            Set("gpu_temp", Pick(gpu, SensorType.Temperature, "GPU Core", "GPU"));
            Set("gpu_load", Pick(gpu, SensorType.Load, "GPU Core", "D3D 3D"));
            Set("gpu_clock", Pick(gpu, SensorType.Clock, "GPU Core"));
            Set("gpu_power", Pick(gpu, SensorType.Power, "GPU Package", "GPU Power", "GPU Core"));
            Set("vram_used", Pick(gpu, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used"));
            Set("vram_total", Pick(gpu, SensorType.SmallData, "GPU Memory Total"));
        }
        Set("ram_used", Pick(s => ram(s) && s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase), SensorType.Data, "Memory Used"));
        Set("ram_avail", Pick(s => ram(s) && s.Name.Contains("Available", StringComparison.OrdinalIgnoreCase), SensorType.Data, "Memory Available"));
        Set("ram_load", Pick(ram, SensorType.Load, "Memory"));
        return r;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try { _pc?.Close(); } catch { }
            _pc = null;
        }
    }
}
