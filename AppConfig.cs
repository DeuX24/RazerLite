using System.Windows.Forms;
using Microsoft.Win32;

namespace RazerLite;

internal sealed class AppConfig
{
    public int MouseDpi { get; set; } = 1800;
    public int MousePollRateHz { get; set; } = 1000;
    public bool MouseScrollLed { get; set; } = true;
    public bool MouseLogoLed { get; set; } = true;

    public KeyboardEffect KeyboardEffect { get; set; } = KeyboardEffect.Static;
    public int KeyboardBrightness { get; set; } = 80;
    public byte ColorR { get; set; } = 0;
    public byte ColorG { get; set; } = 230;
    public byte ColorB { get; set; } = 91;

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RazerLite");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.ini");

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "RazerLite";

    public static AppConfig Load()
    {
        var cfg = new AppConfig();
        try
        {
            if (!File.Exists(ConfigPath))
                return cfg;

            foreach (string rawLine in File.ReadAllLines(ConfigPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case nameof(MouseDpi) when int.TryParse(val, out int dpi):
                        cfg.MouseDpi = dpi;
                        break;
                    case nameof(MousePollRateHz) when int.TryParse(val, out int poll):
                        cfg.MousePollRateHz = poll;
                        break;
                    case nameof(MouseScrollLed) when bool.TryParse(val, out bool scrollLed):
                        cfg.MouseScrollLed = scrollLed;
                        break;
                    case nameof(MouseLogoLed) when bool.TryParse(val, out bool logoLed):
                        cfg.MouseLogoLed = logoLed;
                        break;
                    case nameof(KeyboardEffect) when Enum.TryParse(val, out KeyboardEffect effect):
                        cfg.KeyboardEffect = effect;
                        break;
                    case nameof(KeyboardBrightness) when int.TryParse(val, out int bright):
                        cfg.KeyboardBrightness = Math.Max(0, Math.Min(100, bright));
                        break;
                    case nameof(ColorR) when byte.TryParse(val, out byte r):
                        cfg.ColorR = r;
                        break;
                    case nameof(ColorG) when byte.TryParse(val, out byte g):
                        cfg.ColorG = g;
                        break;
                    case nameof(ColorB) when byte.TryParse(val, out byte b):
                        cfg.ColorB = b;
                        break;
                }
            }
        }
        catch
        {
            // Fall back to defaults if config is unreadable
        }

        return cfg;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        string[] lines =
        [
            $"MouseDpi={MouseDpi}",
            $"MousePollRateHz={MousePollRateHz}",
            $"MouseScrollLed={MouseScrollLed}",
            $"MouseLogoLed={MouseLogoLed}",
            $"KeyboardEffect={KeyboardEffect}",
            $"KeyboardBrightness={KeyboardBrightness}",
            $"ColorR={ColorR}",
            $"ColorG={ColorG}",
            $"ColorB={ColorB}"
        ];
        File.WriteAllLines(ConfigPath, lines);
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) is string val && !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartupEnabled(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (key is null)
            return;

        if (enabled)
        {
            string exePath = Application.ExecutablePath;
            key.SetValue(RunValueName, $"\"{exePath}\" --startup-apply");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
