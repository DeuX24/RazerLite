using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RazerLite;

internal sealed class HidDeviceInfo
{
    public string DevicePath { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public ushort UsagePage { get; }
    public ushort Usage { get; }
    public ushort FeatureReportByteLength { get; }
    public string ProductName { get; }

    public HidDeviceInfo(
        string devicePath,
        ushort vendorId,
        ushort productId,
        ushort usagePage,
        ushort usage,
        ushort featureReportByteLength,
        string productName)
    {
        DevicePath = devicePath;
        VendorId = vendorId;
        ProductId = productId;
        UsagePage = usagePage;
        Usage = usage;
        FeatureReportByteLength = featureReportByteLength;
        ProductName = productName;
    }
}

internal static class HidNative
{
    public const ushort RazerVendorId = 0x1532;

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll", SetLastError = true)]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    public static List<HidDeviceInfo> EnumerateRazerDevices()
    {
        var results = new List<HidDeviceInfo>();
        HidD_GetHidGuid(out Guid hidGuid);

        IntPtr deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            return results;

        try
        {
            var interfaceData = new SpDeviceInterfaceData
            {
                cbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
            };

            for (uint index = 0; SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData); index++)
            {
                SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                if (requiredSize == 0)
                    continue;

                IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                        continue;

                    string? devicePath = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                    if (string.IsNullOrEmpty(devicePath))
                        continue;

                    using SafeFileHandle handle = OpenDevice(devicePath, readWrite: false);
                    if (handle.IsInvalid)
                        continue;

                    var attrs = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
                    if (!HidD_GetAttributes(handle, ref attrs) || attrs.VendorID != RazerVendorId)
                        continue;

                    ushort usagePage = 0;
                    ushort usage = 0;
                    ushort featureLen = 0;
                    if (HidD_GetPreparsedData(handle, out IntPtr preparsed))
                    {
                        try
                        {
                            if (HidP_GetCaps(preparsed, out HidpCaps caps) > 0)
                            {
                                usagePage = caps.UsagePage;
                                usage = caps.Usage;
                                featureLen = caps.FeatureReportByteLength;
                            }
                        }
                        finally
                        {
                            HidD_FreePreparsedData(preparsed);
                        }
                    }

                    string productName = ReadProductString(handle) ?? $"Razer Device (PID 0x{attrs.ProductID:X4})";
                    results.Add(new HidDeviceInfo(devicePath!, attrs.VendorID, attrs.ProductID, usagePage, usage, featureLen, productName));
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return results;
    }

    public static (bool Success, int Win32Error) SendFeatureReport(string devicePath, byte[] reportBuffer)
    {
        foreach (bool readWrite in new[] { false, true })
        {
            using SafeFileHandle handle = OpenDevice(devicePath, readWrite);
            if (handle.IsInvalid)
                continue;

            if (HidD_SetFeature(handle, reportBuffer, reportBuffer.Length))
                return (true, 0);

            int err = Marshal.GetLastWin32Error();
            if (readWrite)
                return (false, err);
        }

        return (false, Marshal.GetLastWin32Error());
    }

    public static (bool Success, int Win32Error) GetFeatureReport(string devicePath, byte[] reportBuffer)
    {
        using SafeFileHandle handle = OpenDevice(devicePath, readWrite: false);
        if (handle.IsInvalid)
            return (false, Marshal.GetLastWin32Error());

        bool ok = HidD_GetFeature(handle, reportBuffer, reportBuffer.Length);
        return (ok, ok ? 0 : Marshal.GetLastWin32Error());
    }

    private static SafeFileHandle OpenDevice(string devicePath, bool readWrite)
    {
        uint access = readWrite ? (GenericRead | GenericWrite) : 0;
        return CreateFile(devicePath, access, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
    }

    private static string? ReadProductString(SafeFileHandle handle)
    {
        byte[] buf = new byte[256];
        if (!HidD_GetProductString(handle, buf, buf.Length))
            return null;

        string raw = Encoding.Unicode.GetString(buf);
        int nullIdx = raw.IndexOf('\0');
        string trimmed = (nullIdx >= 0 ? raw.Substring(0, nullIdx) : raw).Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }
}
