using System.Security.Cryptography;
using System.Text.Json;

namespace AgentNetDoctor.Core.Launch;

public enum LaunchNetworkMode { ClearProxyEnvironment, ExplicitHttpProxy }
public sealed record LaunchProfile(string TargetId, string Version, string ExecutableFingerprint, LaunchNetworkMode Mode, string? Proxy);
public sealed record LaunchApproval(string Digest)
{
    public static LaunchApproval FromReviewedProfile(LaunchProfile profile) => new(Fingerprint(profile));
    public bool Matches(LaunchProfile profile) => Digest == Fingerprint(profile);
    private static string Fingerprint(LaunchProfile profile) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(profile)));
}
public enum LaunchState { StartedNeedsVerification, Rejected, NeedsUserExit, Failed }
public sealed record LaunchOutcome(LaunchState State, string Explanation, int? ProcessId = null);
