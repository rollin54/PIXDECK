using System.Diagnostics;

namespace PixDeckSetup;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))) { Uninstall(args); return; }
        Application.Run(new SetupForm());
    }

    static string? Arg(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // El desinstalador vive dentro de la carpeta que hay que borrar: primero se copia a TEMP
    // y se vuelve a lanzar desde ahi.
    static void Uninstall(string[] args)
    {
        var self = Environment.ProcessPath!;
        bool fromTemp = args.Contains("--temp");
        var dir = Arg(args, "--dir") ?? Installer.InstalledDir() ?? Path.GetDirectoryName(self)!;

        if (!fromTemp)
        {
            using var f = new UninstallForm();
            if (f.ShowDialog() != DialogResult.OK) return;
            var tmp = Path.Combine(Path.GetTempPath(), "PixDeckHub_Desinstalar.exe");
            File.Copy(self, tmp, true);
            Process.Start(new ProcessStartInfo(tmp)
            {
                UseShellExecute = true,
                Arguments = $"--uninstall --temp --dir \"{dir}\"" + (f.PurgeData ? " --purge" : ""),
            });
            return;
        }

        Installer.Uninstall(dir, args.Contains("--purge"));
        MessageBox.Show("PixDeck Hub se desinstaló correctamente.", "Desinstalar PixDeck Hub",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

        // borrar esta copia temporal cuando termine
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & del /f /q \"{self}\"")
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch { }
    }
}
