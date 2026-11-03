using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.AI;

/// <summary>证据包构建器：只发送筛选后的必要片段，剔除疑似凭据内容并控制总量。</summary>
public sealed class EvidencePackBuilder(IOptions<AiOptions> options) : IEvidencePackBuilder
{
    public async Task<EvidencePack> BuildAsync(
        IReadOnlyList<ProjectFileEntry> files,
        IReadOnlyList<EvidenceEntry> evidences,
        IProjectFileReader reader,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var fileByPath = files.ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
        var omitted = new List<string>();
        var items = new List<EvidencePackItem>();
        var totalBytes = 0;

        // 优先级：README 与配置类证据优先，其次是其余证据。
        var ordered = evidences
            .OrderByDescending(e => Priority(e.EvidenceType))
            .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var evidence in ordered)
        {
            if (items.Count >= opts.MaxEvidenceItems)
            {
                omitted.Add(evidence.RelativePath);
                continue;
            }

            if (!fileByPath.TryGetValue(evidence.RelativePath, out var file))
            {
                continue;
            }

            if (file.SecretRisk == SecretRisk.Suspected || file.IsExcluded)
            {
                omitted.Add(evidence.RelativePath);
                continue;
            }

            var content = await reader.ReadTextAsync(evidence.RelativePath, opts.MaxItemBytes * 4, ct);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            string payload;
            int? startLine = evidence.StartLine;
            int? endLine = evidence.EndLine;

            if (file.Category == FileCategory.Code)
            {
                var lines = content.Split('\n');
                var head = lines.Take(opts.MaxCodeHeadLines).ToArray();
                payload = string.Join('\n', head);
                startLine = 1;
                endLine = head.Length;
            }
            else
            {
                payload = content.Length > opts.MaxItemBytes ? content[..opts.MaxItemBytes] : content;
            }

            var bytes = System.Text.Encoding.UTF8.GetByteCount(payload);
            if (totalBytes + bytes > opts.MaxEvidenceBytes)
            {
                omitted.Add(evidence.RelativePath);
                continue;
            }

            totalBytes += bytes;
            items.Add(new EvidencePackItem(
                evidence.RelativePath,
                evidence.EvidenceType,
                evidence.Sha256,
                payload,
                startLine,
                endLine));
        }

        return new EvidencePack(items, omitted.Distinct().ToList(), totalBytes);
    }

    private static int Priority(string evidenceType) => evidenceType switch
    {
        "README 标题" => 100,
        "README 描述" => 95,
        "功能条目" => 90,
        "项目配置" => 80,
        "构建配置" => 80,
        "小程序配置" => 80,
        "依赖清单" => 70,
        "解决方案名称" => 60,
        _ => 50
    };
}
