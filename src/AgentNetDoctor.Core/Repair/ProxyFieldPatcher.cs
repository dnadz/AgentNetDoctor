using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Repair;

public static class ProxyFieldPatcher
{
    public static byte[] Patch(byte[] original, RepairPlan plan)
    {
        if (plan.Kind != ActionKind.PatchAppFields || plan.ToolId != "antigravity" || plan.Changes.Count != 1 ||
            original.Length > 1024 * 1024 || Convert.ToHexString(SHA256.HashData(original)) != plan.BeforeFingerprint)
            throw new InvalidOperationException("目标或原始状态不匹配");
        var change = plan.Changes[0];
        if (change.Field != "http.proxy" || change.After is null || !RepairPlanner.SafeProxy(change.After))
            throw new InvalidOperationException("字段或新值不在允许范围");
        var text = new UTF8Encoding(false, true).GetString(original);
        if (ConfigurationInspector.ReadJson(text, "antigravity").Status != ObservationStatus.Success)
            throw new InvalidOperationException("原始配置不明确");
        var bom = original.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        var reader = new Utf8JsonReader(original.AsSpan(bom), new JsonReaderOptions
        { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 64 });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 || reader.GetString() != change.Field) continue;
            if (!reader.Read() || reader.TokenType != JsonTokenType.String || reader.GetString() != change.Before)
                throw new InvalidOperationException("字段已变化或类型不匹配");
            int start = checked((int)reader.TokenStartIndex + bom), end = checked((int)reader.BytesConsumed + bom);
            var replacement = JsonSerializer.SerializeToUtf8Bytes(change.After);
            var result = new byte[start + replacement.Length + original.Length - end];
            original.AsSpan(0, start).CopyTo(result);
            replacement.CopyTo(result.AsSpan(start));
            original.AsSpan(end).CopyTo(result.AsSpan(start + replacement.Length));
            return result;
        }
        // Adding/removing JSONC properties needs comma/comment ownership; refuse until separately supported.
        throw new InvalidOperationException("只支持修改已存在的字符串字段");
    }
}
