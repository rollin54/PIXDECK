using System.Diagnostics;
using System.IO.Ports;

namespace PixDeckHub;

/// <summary>Conexion USB-serie con el ESP32 (115200 baudios, una linea por mensaje).</summary>
public sealed class DeviceLink : IDisposable
{
    const int Baud = 115200;
    readonly object _lock = new();
    SerialPort? _port;
    Thread? _reader;

    public string? PortName { get; private set; }
    public string? Firmware { get; private set; }
    public bool Connected => _port?.IsOpen == true;

    public event Action<string>? LineReceived;
    public event Action? StatusChanged;

    /// <summary>Busca el equipo. preferred = "AUTO" o un nombre de puerto (COM5).</summary>
    public bool TryConnect(string preferred)
    {
        Disconnect(false);
        var ports = SerialPort.GetPortNames().Distinct().ToList();
        IEnumerable<string> order = preferred != "AUTO" && ports.Contains(preferred)
            ? new[] { preferred }
            : ports.OrderBy(p => p == preferred ? 0 : 1);
        if (preferred != "AUTO" && !ports.Contains(preferred)) return false;

        foreach (var name in order)
        {
            var sp = Probe(name, out var fw);
            if (sp == null) continue;
            lock (_lock)
            {
                _port = sp;
                PortName = name;
                Firmware = fw;
            }
            StartReader();
            StatusChanged?.Invoke();
            return true;
        }
        return false;
    }

    static SerialPort? Probe(string name, out string? fw)
    {
        fw = null;
        SerialPort? sp = null;
        try
        {
            sp = new SerialPort(name, Baud)
            {
                DtrEnable = false,   // evita reiniciar el ESP32 al abrir el puerto
                RtsEnable = false,
                NewLine = "\n",
                ReadTimeout = 150,
                WriteTimeout = 500,
                Encoding = System.Text.Encoding.ASCII,
            };
            sp.Open();
            var sw = Stopwatch.StartNew();
            long nextAsk = 0;
            // Si la placa se reinicio al abrir, arranca en ~1 s y manda HELLO sola.
            while (sw.ElapsedMilliseconds < 3000)
            {
                if (sw.ElapsedMilliseconds >= nextAsk)
                {
                    sp.Write("?\n");
                    nextAsk = sw.ElapsedMilliseconds + 400;
                }
                try
                {
                    var line = sp.ReadLine().Trim();
                    if (line.StartsWith("PIXDECK|HELLO"))
                    {
                        var f = line.Split('|');
                        fw = f.Length > 2 ? f[2] : "?";
                        return sp;
                    }
                }
                catch (TimeoutException) { }
            }
        }
        catch { }
        try { sp?.Close(); } catch { }
        sp?.Dispose();
        return null;
    }

    void StartReader()
    {
        SerialPort? mine;
        lock (_lock) mine = _port;
        if (mine == null) return;
        _reader = new Thread(() => ReadLoop(mine)) { IsBackground = true, Name = "PixDeckReader" };
        _reader.Start();
    }

    // Cada hilo lector atiende solo "su" puerto; si se reconecta, el viejo termina solo.
    void ReadLoop(SerialPort mine)
    {
        while (true)
        {
            lock (_lock) { if (!ReferenceEquals(_port, mine)) return; }
            try
            {
                var line = mine.ReadLine().Trim();
                if (line.Length > 0) LineReceived?.Invoke(line);
            }
            catch (TimeoutException) { }
            catch
            {
                bool current;
                lock (_lock) current = ReferenceEquals(_port, mine);
                if (current) Disconnect(true);
                return;
            }
        }
    }
    public bool Send(string line)
    {
        lock (_lock)
        {
            if (_port == null) return false;
            try
            {
                _port.Write(line + "\n");
                return true;
            }
            catch { }
        }
        Disconnect(true);
        return false;
    }

    public void Disconnect(bool notify)
    {
        SerialPort? sp;
        lock (_lock)
        {
            sp = _port;
            _port = null;
        }
        if (sp == null) return;
        try { sp.Close(); } catch { }
        sp.Dispose();
        if (notify) StatusChanged?.Invoke();
    }

    public void Dispose() => Disconnect(false);
}
