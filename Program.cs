using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RazerLite;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        AppConfig config = AppConfig.Load();

        if (args.Any(a => string.Equals(a, "--startup-apply", StringComparison.OrdinalIgnoreCase)))
        {
            bool applyMouse = config.MouseEnabled && config.MouseApplyOnStartup;
            bool applyKeyboard = config.KeyboardEnabled && config.KeyboardApplyOnStartup;
            if (!applyMouse && !applyKeyboard)
                return;

            List<HidDeviceInfo> devices = HidNative.EnumerateRazerDevices();

            if (applyMouse)
            {
                RazerProtocol.ApplyDeathAdder35G(
                    devices,
                    config.MouseDpi,
                    config.MousePollRateHz,
                    config.MouseScrollLed,
                    config.MouseLogoLed);
            }

            if (applyKeyboard)
            {
                RazerProtocol.ApplyBlackWidow(
                    devices,
                    config.KeyboardEffect,
                    config.KeyboardBrightness,
                    config.ColorR,
                    config.ColorG,
                    config.ColorB);
            }
            return;
        }

        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(config));
    }
}
