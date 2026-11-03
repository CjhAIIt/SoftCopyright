using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>通用适配器：未知技术栈的兜底，提取 README 与目录结构线索。</summary>
public sealed class GenericAdapter : IProjectAdapter
{
    public string Id => "generic";

    public int Priority => int.MaxValue;

    public bool CanHandle(ProjectFileIndex index) => true;

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string>();
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "已使用通用扫描，未匹配到专用技术栈适配器。";

        var readme = context.Index.Files.FirstOrDefault(f =>
        {
            var name = Path.GetFileName(f.RelativePath);
            return name.StartsWith("README", StringComparison.OrdinalIgnoreCase) && f.Category == FileCategory.Doc;
        });

        if (readme is not null)
        {
            var text = await context.Reader.ReadTextAsync(readme.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var title = ExtractTitle(text);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    facts.Add(new AdapterFact("ReadmeTitle", title, readme.RelativePath, FindLine(text, title)));
                    evidences.Add(AdapterSupport.Evidence(context.Index, readme.RelativePath, "README 标题", title));
                }

                var description = ExtractDescription(text);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    facts.Add(new AdapterFact("ReadmeDescription", description, readme.RelativePath, FindLine(text, description)));
                    evidences.Add(AdapterSupport.Evidence(context.Index, readme.RelativePath, "README 描述", description));
                }
            }
        }

        foreach (var group in context.Index.Files.Where(f => f.Category == FileCategory.Code).GroupBy(f => f.DetectedLanguage))
        {
            if (!string.IsNullOrWhiteSpace(group.Key))
            {
                technologies.Add($"{group.Key}（{group.Count()} 个文件）");
            }
        }

        var topLevel = context.Index.Files
            .Select(f => f.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();

        foreach (var directory in topLevel)
        {
            facts.Add(new AdapterFact("TopLevelDirectory", directory, null, null));
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints, facts);
    }

    internal static string ExtractTitle(string text)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return string.Empty;
    }

    internal static string ExtractDescription(string text)
    {
        var lines = text.Split('\n');
        var inCodeFence = false;
        var buffer = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence)
            {
                continue;
            }

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("!", StringComparison.Ordinal))
            {
                if (buffer.Count > 0)
                {
                    break;
                }

                continue;
            }

            buffer.Add(line);
            if (buffer.Count >= 3)
            {
                break;
            }
        }

        return string.Join(' ', buffer).Trim();
    }

    private static int FindLine(string text, string needle)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(needle, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return 1;
    }
}
