using AgentNetDoctor.Core.Models;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace AgentNetDoctor.Windows.Collection;

public static class NetworkEnvironmentReader
{
    public static NetworkDetails Read(CancellationToken cancellationToken) =>
        new(ReadInterfaces(cancellationToken), ReadWinHttp(), ObservationStatus.NotTested);

    private static Observation<IReadOnlyList<InterfaceRecord>> ReadInterfaces(CancellationToken cancellationToken)
    {
        List<InterfaceRecord> items = [];
        bool partial = false;
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var properties = adapter.GetIPProperties();
                    items.Add(new(adapter.Name, adapter.NetworkInterfaceType.ToString(), adapter.OperationalStatus.ToString(),
                        properties.DnsAddresses.Select(address => address.ToString()).ToArray(),
                        properties.GatewayAddresses.Select(address => address.Address.ToString()).ToArray()));
                }
                catch (NetworkInformationException) { partial = true; }
            }
            return new(partial ? ObservationStatus.Partial : ObservationStatus.Success, items,
                partial ? "INTERFACE_PROPERTIES_PARTIAL" : null, DateTimeOffset.UtcNow);
        }
        catch (NetworkInformationException ex)
        { return new(ex.ErrorCode == 5 ? ObservationStatus.PermissionDenied : ObservationStatus.Failed, items, "INTERFACE_ENUMERATION_FAILED", DateTimeOffset.UtcNow); }
    }

    private static Observation<WinHttpDefaults> ReadWinHttp()
    {
        NativeProxyInfo info = default;
        try
        {
            if (!WinHttpGetDefaultProxyConfiguration(out info))
            {
                var code = Marshal.GetLastWin32Error();
                return new(code == 5 ? ObservationStatus.PermissionDenied : ObservationStatus.Failed, null,
                    $"WINHTTP_READ_{code}", DateTimeOffset.UtcNow);
            }
            return new(ObservationStatus.Success, new(info.AccessType, !string.IsNullOrWhiteSpace(Marshal.PtrToStringUni(info.Proxy)), !string.IsNullOrWhiteSpace(Marshal.PtrToStringUni(info.Bypass))),
                null, DateTimeOffset.UtcNow);
        }
        finally { Free(info.Proxy); Free(info.Bypass); }
    }

    public static Observation<ProxySettings> ReadCurrentUserProxy()
    {
        NativeUserProxy info = default;
        try
        {
            if (!WinHttpGetIEProxyConfigForCurrentUser(out info))
            {
                var code = Marshal.GetLastWin32Error();
                return new(code == 5 ? ObservationStatus.PermissionDenied : ObservationStatus.Failed, null,
                    $"USER_PROXY_API_{code}", DateTimeOffset.UtcNow);
            }
            var hasManual = !string.IsNullOrWhiteSpace(Marshal.PtrToStringUni(info.Proxy));
            var hasPac = !string.IsNullOrWhiteSpace(Marshal.PtrToStringUni(info.AutoConfig));
            // Only configured state; do not resolve PAC/WPAD or export proxy credentials.
            return new(ObservationStatus.Partial, new(hasManual, hasManual ? "已配置（地址未导出）" : null, hasPac, info.AutoDetect),
                "EFFECTIVE_PROXY_NOT_VERIFIED", DateTimeOffset.UtcNow);
        }
        finally { Free(info.Proxy); Free(info.Bypass); Free(info.AutoConfig); }
    }

    private static void Free(IntPtr pointer) { if (pointer != IntPtr.Zero) GlobalFree(pointer); }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeProxyInfo { public uint AccessType; public IntPtr Proxy; public IntPtr Bypass; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeUserProxy
    {
        [MarshalAs(UnmanagedType.Bool)] public bool AutoDetect;
        public IntPtr AutoConfig; public IntPtr Proxy; public IntPtr Bypass;
    }
    [DllImport("winhttp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetDefaultProxyConfiguration(out NativeProxyInfo info);
    [DllImport("winhttp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WinHttpGetIEProxyConfigForCurrentUser(out NativeUserProxy info);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
