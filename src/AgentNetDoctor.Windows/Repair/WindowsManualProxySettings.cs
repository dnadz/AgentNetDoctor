using AgentNetDoctor.Core.Repair;
using Microsoft.Win32;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

namespace AgentNetDoctor.Windows.Repair;

public sealed class LoopbackEndpointCheck : ILocalEndpointCheck
{
    public async Task<LocalEndpointState> CheckAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        if (!IPAddress.IsLoopback(endpoint.Address)) return LocalEndpointState.Unknown;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        using var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try { await socket.ConnectAsync(endpoint, timeout.Token); return LocalEndpointState.Accepting; }
        catch (SocketException error) { return error.SocketErrorCode == SocketError.ConnectionRefused ? LocalEndpointState.Refused : LocalEndpointState.Unknown; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return LocalEndpointState.Unknown; }
    }
}

public sealed class WindowsManualProxySettings : IManualProxySettings
{
    public static bool WritesValidated => false;
    public ManualProxyConfiguration Read()
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("当前仅支持Windows x64。");
        var size = Marshal.SizeOf<Option>();
        var memory = Marshal.AllocHGlobal(size * 4);
        try
        {
            var codes = new[] { 10, 2, 3, 4 }; // FLAGS_UI avoids reporting autodetection results as configured flags.
            for (var i = 0; i < codes.Length; i++) Marshal.StructureToPtr(new Option { Code = codes[i] }, memory + i * size, false);
            var list = new OptionList { Size = Marshal.SizeOf<OptionList>(), Count = 4, Options = memory };
            var length = list.Size;
            if (!InternetQueryOption(IntPtr.Zero, 75, ref list, ref length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            string Text(int index) => Marshal.PtrToStringUni(Marshal.PtrToStructure<Option>(memory + index * size).Text) ?? "";
            return new(Marshal.PtrToStructure<Option>(memory).Flags, Text(1), Text(2), Text(3), HasPolicyOrUnknown());
        }
        finally
        {
            for (var i = 1; i < 4; i++)
            {
                var pointer = Marshal.PtrToStructure<Option>(memory + i * size).Text;
                if (pointer != IntPtr.Zero) GlobalFree(pointer);
            }
            Marshal.FreeHGlobal(memory);
        }
    }

    public bool CompareExchangeFlags(ManualProxyConfiguration expected, int flags)
    {
        if (!WritesValidated) throw new NotSupportedException("当前发布包未通过隔离Windows写入验收，禁止修改系统代理。");
        if (expected.Flags is not (1 or 2 or 3) || flags is not (1 or 2 or 3) ||
            ManualProxyRepair.EligibleEndpoint(expected with { Flags = 3 }) is null) return false;
        using var identity = WindowsIdentity.GetCurrent();
        using var gate = new Mutex(false, "Local\\AgentNetDoctor.UserProxy." + identity.User?.Value);
        bool held;
        try { held = gate.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
        if (!held) throw new IOException("另一个修复操作正在执行。");
        try
        {
            if (Read() != expected) return false;
            var memory = Marshal.AllocHGlobal(Marshal.SizeOf<Option>());
            try
            {
                Marshal.StructureToPtr(new Option { Code = 1, Flags = flags }, memory, false);
                var list = new OptionList { Size = Marshal.SizeOf<OptionList>(), Count = 1, Options = memory };
                if (!InternetSetOptions(IntPtr.Zero, 75, ref list, list.Size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Marshal.FreeHGlobal(memory); }
            // Notify consumers through supported APIs. Existing applications may still require a normal restart.
            var notified = InternetNotify(IntPtr.Zero, 39, IntPtr.Zero, 0);
            var refreshed = InternetNotify(IntPtr.Zero, 37, IntPtr.Zero, 0);
            if (!notified || !refreshed) throw new IOException("设置可能已修改，但Windows刷新通知未全部成功；请检查恢复记录并正常重启相关应用。");
            if (Read() != expected with { Flags = flags }) throw new IOException("写后复核不一致；请检查恢复记录，禁止自动重试。");
            return true;
        }
        finally { gate.ReleaseMutex(); }
    }

    private static bool HasPolicyOrUnknown()
    {
        try
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    using var basis = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var path in new[] { @"Software\Policies\Microsoft\Windows\CurrentVersion\Internet Settings", @"Software\Policies\Microsoft\Internet Explorer\Control Panel" })
                    {
                        using var key = basis.OpenSubKey(path);
                        if (key is not null && (key.ValueCount > 0 || key.SubKeyCount > 0)) return true;
                    }
                    using var ordinary = basis.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                    if (ordinary?.GetValue("ProxySettingsPerUser") is int value && value == 0) return true;
                }
            return false;
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException) { return true; }
    }
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct Option { [FieldOffset(0)] public int Code; [FieldOffset(8)] public int Flags; [FieldOffset(8)] public IntPtr Text; }
    [StructLayout(LayoutKind.Sequential)]
    private struct OptionList { public int Size; public IntPtr Connection; public int Count; public int Error; public IntPtr Options; }
    [DllImport("wininet.dll", EntryPoint = "InternetQueryOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetQueryOption(IntPtr handle, int option, ref OptionList list, ref int length);
    [DllImport("wininet.dll", EntryPoint = "InternetSetOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetSetOptions(IntPtr handle, int option, ref OptionList list, int length);
    [DllImport("wininet.dll", EntryPoint = "InternetSetOptionW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InternetNotify(IntPtr handle, int option, IntPtr value, int length);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr pointer);
}
