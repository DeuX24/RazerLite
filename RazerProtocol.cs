namespace RazerLite;

internal enum KeyboardEffect
{
    Static,
    Spectrum,
    WaveLeft,
    WaveRight,
    Breathing,
    Off
}

internal sealed class ApplyResult
{
    public bool Success { get; }
    public string Message { get; }

    public ApplyResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}

internal static class RazerProtocol
{
    public const ushort PidDeathAdder35G = 0x0016;
    public const ushort PidBlackWidowChromaV2 = 0x0221;

    private static readonly HashSet<ushort> KnownBlackWidowPids =
    [
        0x0221, // BlackWidow Chroma V2
        0x0203, // BlackWidow Chroma
        0x0209, // BlackWidow Chroma TE
        0x0216, // BlackWidow X Chroma
        0x021A, // BlackWidow X TE Chroma
        0x0214  // BlackWidow Ultimate 2016
    ];

    public static List<HidDeviceInfo> FindDeathAdder35G(IReadOnlyList<HidDeviceInfo> devices) =>
        devices.Where(d => d.ProductId == PidDeathAdder35G).ToList();

    public static List<HidDeviceInfo> FindBlackWidow(IReadOnlyList<HidDeviceInfo> devices)
    {
        var exact = devices.Where(d => d.ProductId == PidBlackWidowChromaV2).ToList();
        if (exact.Count > 0)
            return exact;

        var known = devices.Where(d => KnownBlackWidowPids.Contains(d.ProductId)).ToList();
        if (known.Count > 0)
            return known;

        return devices.Where(d => d.ProductId != PidDeathAdder35G && d.FeatureReportByteLength == 91).ToList();
    }

    public static ApplyResult ApplyDeathAdder35G(
        IReadOnlyList<HidDeviceInfo> devices,
        int dpi,
        int pollRateHz,
        bool scrollWheelLed,
        bool logoLed)
    {
        var targets = FindDeathAdder35G(devices);
        if (targets.Count == 0)
            return new ApplyResult(false, "DeathAdder Left-Hand / 3.5G (PID 0016) not detected.");

        byte ledsByte = (byte)((logoLed ? 0x01 : 0x00) | (scrollWheelLed ? 0x02 : 0x00));
        byte dpiByte = dpi switch
        {
            450 => 4,
            900 => 3,
            1800 => 2,
            3500 => 1,
            _ => 2
        };
        byte pollByte = pollRateHz switch
        {
            1000 => 1,
            500 => 2,
            125 => 3,
            _ => 1
        };

        int lastError = 0;
        foreach (HidDeviceInfo dev in targets)
        {
            byte[][] payloads =
            [
                [0x10, ledsByte, dpiByte, pollByte],
                [0x00, ledsByte, dpiByte, pollByte]
            ];

            foreach (byte[] payload in payloads)
            {
                (bool ok, int err) = HidNative.SendFeatureReport(dev.DevicePath, payload);
                if (ok)
                    return new ApplyResult(true, $"DeathAdder applied: {dpi} DPI, {pollRateHz} Hz.");
                lastError = err;
            }
        }

        return new ApplyResult(
            false,
            $"Windows HID driver blocked the legacy 3.5G control packet (Win32 error {lastError}). " +
            "This mouse uses a legacy USB control transfer; a libusb-win32 filter driver or Razer's legacy v3.05 driver may be required on this PC.");
    }

    public static ApplyResult ApplyBlackWidow(
        IReadOnlyList<HidDeviceInfo> devices,
        KeyboardEffect effect,
        int brightnessPercent,
        byte r,
        byte g,
        byte b)
    {
        var targets = FindBlackWidow(devices)
            .OrderByDescending(d => d.FeatureReportByteLength == 91)
            .ThenByDescending(d => d.UsagePage >= 0xFF00)
            .ToList();

        if (targets.Count == 0)
            return new ApplyResult(false, "Razer BlackWidow keyboard not detected.");

        byte brightnessByte = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(brightnessPercent * 255.0 / 100.0)));

        byte[] brightnessReport = BuildChromaReport(
            transactionId: 0x1F,
            commandClass: 0x03,
            commandId: 0x03,
            dataSize: 0x03,
            args: [0x01, 0x05, brightnessByte]);

        byte[] effectReport = effect switch
        {
            KeyboardEffect.Static => BuildChromaReport(0x1F, 0x03, 0x0A, 0x04, [0x06, r, g, b]),
            KeyboardEffect.Spectrum => BuildChromaReport(0x1F, 0x03, 0x0A, 0x01, [0x04]),
            KeyboardEffect.WaveLeft => BuildChromaReport(0x1F, 0x03, 0x0A, 0x02, [0x01, 0x02]),
            KeyboardEffect.WaveRight => BuildChromaReport(0x1F, 0x03, 0x0A, 0x02, [0x01, 0x01]),
            KeyboardEffect.Breathing => BuildChromaReport(0x1F, 0x03, 0x0A, 0x07, [0x03, 0x01, r, g, b, 0x00, 0x00]),
            KeyboardEffect.Off => BuildChromaReport(0x1F, 0x03, 0x0A, 0x01, [0x00]),
            _ => BuildChromaReport(0x1F, 0x03, 0x0A, 0x01, [0x04])
        };

        int lastError = 0;
        foreach (HidDeviceInfo dev in targets)
        {
            (bool okEffect, int err1) = HidNative.SendFeatureReport(dev.DevicePath, effectReport);
            if (!okEffect)
            {
                lastError = err1;
                continue;
            }

            if (effect != KeyboardEffect.Off)
                HidNative.SendFeatureReport(dev.DevicePath, brightnessReport);

            return new ApplyResult(true, $"BlackWidow applied: {effect} ({brightnessPercent}% brightness).");
        }

        return new ApplyResult(false, $"Failed to send lighting report to BlackWidow (Win32 error {lastError}).");
    }

    private static byte[] BuildChromaReport(
        byte transactionId,
        byte commandClass,
        byte commandId,
        byte dataSize,
        byte[] args)
    {
        // Index 0 is HID Report ID (0x00), followed by 90-byte struct razer_report (indices 1..90)
        byte[] buffer = new byte[91];
        buffer[0] = 0x00;           // HID Report ID
        buffer[1] = 0x00;           // status
        buffer[2] = transactionId;  // transaction_id
        buffer[3] = 0x00;           // remaining_pkts MSB
        buffer[4] = 0x00;           // remaining_pkts LSB
        buffer[5] = 0x00;           // protocol_type
        buffer[6] = dataSize;       // data_size
        buffer[7] = commandClass;   // command_class
        buffer[8] = commandId;      // command_id

        int copyLen = Math.Min(args.Length, 80);
        Array.Copy(args, 0, buffer, 9, copyLen);

        // CRC is XOR of razer_report bytes [2..87], which are buffer[3..88]
        byte crc = 0;
        for (int i = 3; i <= 88; i++)
            crc ^= buffer[i];

        buffer[89] = crc;
        buffer[90] = 0x00; // reserved
        return buffer;
    }
}
