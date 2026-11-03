using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI;

/// <summary>离线规则分析器：不依赖任何外部服务，结果确定可复现。</summary>
public sealed class RuleBasedProjectAnalyzer : IProjectAnalyzer
{
    public string Id => "rule-based";

    public Task<ProjectAnalysisResult> AnalyzeAsync(EvidencePack pack, ProjectProfile baseline, CancellationToken ct)
    {
        var warnings = new List<string>();
        var features = new List<Fact<string>>();

        var readmeTitle = pack.Items.FirstOrDefault(i => i.EvidenceType == "README 标题");
        var readmeDescription = pack.Items.FirstOrDefault(i => i.EvidenceType == "README 描述");
        var sections = pack.Items.Where(i => i.EvidenceType == "功能条目").ToList();

        var name = baseline.Name;
        if (name.Value is null && readmeTitle is not null)
        {
            name = new Fact<string>(
                FirstLine(readmeTitle.Content),
                FactStatus.Observed,
                [Ref(readmeTitle)],
                "取自 README 标题");
        }

        var purpose = baseline.Purpose;
        if (purpose.Value is null && readmeDescription is not null)
        {
            purpose = new Fact<string>(
                readmeDescription.Content.Trim(),
                FactStatus.Observed,
                [Ref(readmeDescription)],
                "取自 README 描述");
        }

        foreach (var section in sections.Take(15))
        {
            features.Add(new Fact<string>(
                FirstLine(section.Content),
                FactStatus.Observed,
                [Ref(section)],
                "取自 README 章节标题"));
        }

        if (features.Count == 0)
        {
            foreach (var item in pack.Items.Where(i => i.EvidenceType is "项目配置" or "构建配置").Take(3))
            {
                features.Add(new Fact<string>(
                    $"{Path.GetFileName(item.RelativePath)}：{FirstLine(item.Content)}",
                    FactStatus.Observed,
                    [Ref(item)],
                    "取自项目配置文件"));
            }
        }

        if (features.Count == 0)
        {
            warnings.Add("证据包中未找到可用于描述功能的材料，功能说明需申请人补充。");
        }

        var profile = new ProjectProfile(
            name,
            baseline.Version,
            purpose,
            features,
            baseline.DetectedTechnologies,
            baseline.DetectedEntrypoints,
            baseline.OpenQuestions,
            baseline.AnalyzerVersion);

        return Task.FromResult(new ProjectAnalysisResult(profile, warnings));
    }

    private static string FirstLine(string content)
    {
        var line = content.Split('\n').FirstOrDefault(l => l.Trim().Length > 0);
        return line?.Trim() ?? string.Empty;
    }

    private static EvidenceRef Ref(EvidencePackItem item) =>
        new(item.RelativePath, item.StartLine, item.EndLine, item.EvidenceType, item.Sha256);
}
