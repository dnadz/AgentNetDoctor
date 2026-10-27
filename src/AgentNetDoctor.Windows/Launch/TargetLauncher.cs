using AgentNetDoctor.Core.Launch;
using AgentNetDoctor.Windows.Repair;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace AgentNetDoctor.Windows.Launch;

// Adapter-owned registrations; a stored profile cannot supply executable paths, arguments or arbitrary environment keys.
public sealed record VerifiedLaunchTarget(string Id, string Version, string ExecutablePath, string WorkingDirectory,
    IReadOnlyList<string> Arguments, IReadOnlyList<LaunchNetworkMode> SupportedModes, Func<string?> CurrentVersion, Func<bool> IsStopped);

public sealed class TargetLauncher(IReadOnlyList<VerifiedLaunchTarget> registrations)
{
    private readonly IReadOnlyDictionary<string, VerifiedLaunchTarget> targets = registrations.ToDictionary(
        target => target.Id, target => target with { Arguments = target.Arguments.ToArray(), SupportedModes = target.SupportedModes.ToArray() }, StringComparer.Ordinal);

    public async Task<LaunchOutcome> StartAsync(LaunchProfile profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidProfile(profile) || !targets.TryGetValue(profile.TargetId, out var target) ||
            target.Version != profile.Version || !target.SupportedModes.Contains(profile.Mode))
            return new(LaunchState.Rejected, "启动目标或网络模式未验证。");
        try
        {
            if (target.CurrentVersion() != profile.Version) return new(LaunchState.Rejected, "目标版本已变化，请重新检查。");
            if (!target.IsStopped()) return new(LaunchState.NeedsUserExit, "请保存工作并正常退出目标程序，再重试；不会强制关闭。");
            var executable = LocalFileGuard.Validate(target.ExecutablePath);
            var working = LocalFileGuard.Validate(target.WorkingDirectory);
            if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                return new(LaunchState.Rejected, "启动仅支持已注册的本地可执行文件。");
            // Keep the executable closed to writes/deletes through the hash check and process creation.
            await using var binary = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(binary, cancellationToken));
            if (hash != profile.ExecutableFingerprint) return new(LaunchState.Rejected, "可执行文件已变化，请重新检查。");
            var start = new ProcessStartInfo(executable) { WorkingDirectory = working, UseShellExecute = false };
            foreach (var argument in target.Arguments) start.ArgumentList.Add(argument);
            foreach (var key in start.Environment.Keys.Where(IsProxyVariable).ToArray()) start.Environment.Remove(key);
            if (profile.Mode == LaunchNetworkMode.ExplicitHttpProxy)
            {
                start.Environment["HTTP_PROXY"] = profile.Proxy;
                start.Environment["HTTPS_PROXY"] = profile.Proxy;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (target.CurrentVersion() != profile.Version) return new(LaunchState.Rejected, "目标版本已变化。");
            if (!target.IsStopped()) return new(LaunchState.NeedsUserExit, "目标已启动，请正常退出后重试。");
            using var child = Process.Start(start);
            return child is null ? new(LaunchState.Failed, "未能创建目标进程。") :
                new(LaunchState.StartedNeedsVerification, "已启动；仅设置子进程代理环境，不证明绕过TUN或应用内代理，仍需业务验证。", child.Id);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException)
        {
            return new(LaunchState.Failed, "无法安全读取或启动目标；未修改系统网络设置。");
        }
    }

    public static bool ValidProfile(LaunchProfile profile) =>
        profile.TargetId is { Length: > 0 and <= 64 } && profile.TargetId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
        profile.Version is { Length: > 0 and <= 128 } && profile.ExecutableFingerprint is { Length: 64 } && profile.ExecutableFingerprint.All(char.IsAsciiHexDigit) &&
        (profile.Mode == LaunchNetworkMode.ClearProxyEnvironment && profile.Proxy is null ||
         profile.Mode == LaunchNetworkMode.ExplicitHttpProxy && profile.Proxy is { Length: > 0 and <= 2048 } &&
         Uri.TryCreate(profile.Proxy, UriKind.Absolute, out var proxy) && proxy.Scheme == "http" && proxy.Host.Length > 0 &&
         proxy.UserInfo.Length == 0 && proxy.Query.Length == 0 && proxy.Fragment.Length == 0 && proxy.AbsolutePath == "/");

    private static bool IsProxyVariable(string key) => new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY" }.Contains(key, StringComparer.OrdinalIgnoreCase);
}
