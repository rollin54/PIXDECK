using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace PixDeckSetup;

public sealed record InstallOptions(string Dir, bool Autostart, bool Desktop);

/// <summary>Lo que hace el instalador: copiar archivos, accesos directos, registro y tarea de inicio.</summary>
public static class Installer
{
    public const string AppName = "PixDeck Hub";
    public const string ExeName = "PixDeckHub.exe";
    public const string UninstallExe = "Desinstalar PixDeck Hub.exe";
    public const string TaskName = "PixDeckHub";
    public const string Publisher = "PixDeck";
    const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PixDeckHub";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "3.0.0";

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);

    static string StartMenuLink =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName + ".lnk");
    static string DesktopLink =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AppName + ".lnk");

    static RegistryKey Hklm => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    /// <summary>Carpeta de una instalacion anterior (para actualizar), o null.</summary>
    public static string? InstalledDir()
    {
        using var k = Hklm.OpenSubKey(UninstallKey);
        var dir = k?.GetValue("InstallLocation") as string;
        return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, ExeName)) ? dir : null;
    }

    public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("payload.zip");

    /// <summary>Cierra PixDeck Hub si esta abierto (para poder reemplazar sus archivos).</summary>
    public static void StopApp()
    {
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try { p.Kill(); p.WaitForExit(4000); } catch { }
            finally { p.Dispose(); }
        }
    }

    public static void Install(InstallOptions o, Action<int, string> progress)
    {
        progress(3, "Cerrando PixDeck Hub si está abierto...");
        StopApp();

        progress(8, "Copiando archivos...");
        Directory.CreateDirectory(o.Dir);
        var root = Path.GetFullPath(o.Dir) + Path.DirectorySeparatorChar;
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                       ?? throw new InvalidOperationException("El instalador no trae los archivos del programa."))
        using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            int n = 0, total = Math.Max(1, zip.Entries.Count);
            foreach (var e in zip.Entries)
            {
                var dest = Path.GetFullPath(Path.Combine(o.Dir, e.FullName));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;   // nunca fuera de la carpeta
                if (string.IsNullOrEmpty(e.Name)) { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                e.ExtractToFile(dest, true);
                progress(8 + 72 * ++n / total, $"Copiando {e.FullName}");
            }
        }

        progress(82, "Preparando el desinstalador...");
        File.Copy(Environment.ProcessPath!, Path.Combine(o.Dir, UninstallExe), true);

        progress(86, "Creando accesos directos...");
        var exe = Path.Combine(o.Dir, ExeName);
        CreateShortcut(StartMenuLink, exe, o.Dir, "Monitor de hardware para PIXDECK");
        if (o.Desktop) CreateShortcut(DesktopLink, exe, o.Dir, "Monitor de hardware para PIXDECK");
        else TryDelete(DesktopLink);

        progress(90, "Registrando en Aplicaciones instaladas...");
        using (var k = Hklm.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", AppName);
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", Publisher);
            k.SetValue("DisplayIcon", exe + ",0");
            k.SetValue("InstallLocation", o.Dir);
            k.SetValue("UninstallString", $"\"{Path.Combine(o.Dir, UninstallExe)}\" --uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(DirSize(o.Dir) / 1024), RegistryValueKind.DWord);
        }

        progress(95, o.Autostart ? "Activando el inicio con Windows..." : "Sin inicio con Windows...");
        SetAutostart(o.Autostart, exe);

        progress(100, "¡Listo!");
    }

    public static void SetAutostart(bool enable, string exe)
    {
        if (enable)
            Schtasks($"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\" --autostart\" /SC ONLOGON /RL HIGHEST /DELAY 0000:15 /F");
        else
            Schtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    static int Schtasks(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            });
            if (p == null) return -1;
            p.WaitForExit(10000);
            return p.ExitCode;
        }
        catch { return -1; }
    }

    static void CreateShortcut(string lnk, string target, string workDir, string description)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic sc = shell.CreateShortcut(lnk);
            sc.TargetPath = target;
            sc.WorkingDirectory = workDir;
            sc.IconLocation = target + ",0";
            sc.Description = description;
            sc.Save();
        }
        catch { /* sin acceso directo no es grave */ }
    }

    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }

    static long DirSize(string dir)
    {
        try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }

    // ======================================================================
    // desinstalar
    // ======================================================================
    /// <summary>Solo se borra una carpeta que de verdad es de PixDeck Hub.</summary>
    public static bool IsSafeInstallDir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        var full = Path.GetFullPath(dir).TrimEnd('\\');
        if (full.Length <= 3) return false;                                       // C:\
        var forbidden = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (forbidden.Any(f => !string.IsNullOrEmpty(f) && string.Equals(f.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase))) return false;
        return File.Exists(Path.Combine(full, ExeName));
    }

    public static void Uninstall(string dir, bool purgeData)
    {
        StopApp();
        Schtasks($"/Delete /TN \"{TaskName}\" /F");
        TryDelete(StartMenuLink);
        TryDelete(DesktopLink);
        try { Hklm.DeleteSubKeyTree(UninstallKey, false); } catch { }

        if (IsSafeInstallDir(dir))
        {
            for (int i = 0; i < 5; i++)
            {
                try { Directory.Delete(dir, true); break; }
                catch { Thread.Sleep(600); }
            }
        }
        if (purgeData)
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixDeckHub");
            try { if (Directory.Exists(cfg)) Directory.Delete(cfg, true); } catch { }
        }
    }
}
