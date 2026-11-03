using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;

namespace SoftCopyright.Application.Services;

/// <summary>项目分析结果映射。</summary>
public static class AnalysisMapper
{
    public static AnalysisDto ToDto(ProjectAnalysis analysis, IReadOnlyList<Evidence> caseEvidences)
    {
        var profile = ParseProfile(analysis.ProfileJson);
        var coverage = ParseCoverage(analysis.CoverageJson);
        var questions = ParseQuestions(analysis.OpenQuestionsJson);
        var adapters = ParseAdapters(analysis.AdapterHitsJson);

        var evidenceDtos = caseEvidences
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .Select(e => Mapping.ToDto(new EvidenceEntry(
                e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, e.Excerpt, e.Source)))
            .ToList();

        return new AnalysisDto(
            profile.Name.Value,
            profile.Name.Status,
            profile.Version.Value,
            profile.Version.Status,
            profile.Purpose.Value,
            profile.Purpose.Status,
            profile.Features.Select(Mapping.ToDto).ToList(),
            profile.DetectedTechnologies,
            profile.DetectedEntrypoints,
            questions,
            coverage,
            evidenceDtos,
            adapters,
            analysis.AnalyzerVersion,
            analysis.RulesVersion,
            analysis.CreatedAt);
    }

    private static ProjectProfile ParseProfile(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ProjectProfile>(json, Mapping.JsonOptions)
                   ?? EmptyProfile();
        }
        catch (System.Text.Json.JsonException)
        {
            return EmptyProfile();
        }
    }

    private static ProjectProfile EmptyProfile() => new(
        Fact<string>.Unknown("暂无分析结果"),
        Fact<string>.Unknown("暂无分析结果"),
        Fact<string>.Unknown("暂无分析结果"),
        [],
        [],
        [],
        [],
        "1.0.0");

    private static List<CoverageDto> ParseCoverage(string json)
    {
        try
        {
            var coverage = System.Text.Json.JsonSerializer.Deserialize<CoverageSummary>(json, Mapping.JsonOptions);
            return coverage?.Items.Select(c => new CoverageDto(c.Dimension, c.Covered, c.Detail)).ToList() ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static List<string> ParseQuestions(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json, Mapping.JsonOptions) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static List<string> ParseAdapters(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var results = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var id = item.TryGetProperty("adapterId", out var idElement) ? idElement.GetString() : null;
                var summary = item.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    results.Add($"{id}：{summary}");
                }
            }

            return results;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
