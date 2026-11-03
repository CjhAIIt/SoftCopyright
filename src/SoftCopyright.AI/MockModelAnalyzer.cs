using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI;

/// <summary>演示用分析器：输出结构合法、且引用证据包中真实路径的示例结果。</summary>
public sealed class MockModelAnalyzer : IProjectAnalyzer
{
    public string Id => "mock";

    public Task<ProjectAnalysisResult> AnalyzeAsync(EvidencePack pack, ProjectProfile baseline, CancellationToken ct)
    {
        var features = new List<Fact<string>>();

        foreach (var item in pack.Items.Where(i => i.EvidenceType == "功能条目").Take(5))
        {
            features.Add(new Fact<string>(
                item.Content.Trim(),
                FactStatus.Observed,
                [new EvidenceRef(item.RelativePath, item.StartLine, item.EndLine, item.EvidenceType, item.Sha256)],
                "演示 Provider 输出"));
        }

        if (features.Count == 0)
        {
            var first = pack.Items.FirstOrDefault();
            if (first is not null)
            {
                features.Add(new Fact<string>(
                    $"{Path.GetFileName(first.RelativePath)} 提供的项目说明",
                    FactStatus.Inferred,
                    [new EvidenceRef(first.RelativePath, first.StartLine, first.EndLine, first.EvidenceType, first.Sha256)],
                    "演示 Provider 输出"));
            }
        }

        var profile = new ProjectProfile(
            baseline.Name,
            baseline.Version,
            baseline.Purpose,
            features,
            baseline.DetectedTechnologies,
            baseline.DetectedEntrypoints,
            baseline.OpenQuestions,
            baseline.AnalyzerVersion);

        return Task.FromResult(new ProjectAnalysisResult(profile, ["当前使用演示 Provider（Mock），输出仅用于流程验证。"]));
    }
}
