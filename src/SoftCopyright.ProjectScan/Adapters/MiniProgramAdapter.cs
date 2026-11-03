using System.Text.Json;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>微信小程序适配器：解析 project.config.json / app.json / app.js。</summary>
public sealed class MiniProgramAdapter : IProjectAdapter
{
    public string Id => "miniprogram";

    public int Priority => 50;

    public bool CanHandle(ProjectFileIndex index) =>
        index.HasAny("project.config.json", "app.json") && FindWxmlPath(index) is not null;

    private static string? FindWxmlPath(ProjectFileIndex index) =>
        index.Files.FirstOrDefault(f => f.RelativePath.EndsWith(".wxml", StringComparison.OrdinalIgnoreCase))?.RelativePath;

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string> { "微信小程序" };
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "未发现可解析的小程序配置。";

        var appJson = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "app.json", StringComparison.OrdinalIgnoreCase));

        if (appJson is not null)
        {
            var text = await context.Reader.ReadTextAsync(appJson.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                    {
                        var pageNames = pages.EnumerateArray()
                            .Select(p => p.GetString())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToList();

                        foreach (var page in pageNames)
                        {
                            facts.Add(new AdapterFact("Page", page, appJson.RelativePath, null));
                        }

                        entrypoints.AddRange(pageNames.Select(p => p + ".wxml").Take(20));
                        summary = $"发现小程序，包含 {pageNames.Count} 个页面";
                    }

                    if (root.TryGetProperty("window", out var window) &&
                        window.TryGetProperty("navigationBarTitleText", out var title))
                    {
                        var titleText = title.GetString();
                        if (!string.IsNullOrWhiteSpace(titleText))
                        {
                            facts.Add(new AdapterFact("Title", titleText, appJson.RelativePath, null));
                        }
                    }

                    var pageCount = root.TryGetProperty("pages", out var pagesElement) && pagesElement.ValueKind == JsonValueKind.Array
                        ? pagesElement.GetArrayLength()
                        : 0;

                    evidences.Add(AdapterSupport.Evidence(context.Index, appJson.RelativePath, "小程序配置",
                        $"小程序页面数 {pageCount}"));
                }
                catch (JsonException)
                {
                    evidences.Add(AdapterSupport.Evidence(context.Index, appJson.RelativePath, "小程序配置", "app.json 解析失败，已跳过"));
                }
            }
        }

        var projectConfig = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "project.config.json", StringComparison.OrdinalIgnoreCase));

        if (projectConfig is not null)
        {
            var text = await context.Reader.ReadTextAsync(projectConfig.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("projectname", out var projectName))
                    {
                        var value = projectName.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            facts.Add(new AdapterFact("Name", value, projectConfig.RelativePath, null));
                        }
                    }

                    // 只记录 AppID 是否存在，不记录具体值，避免留存可能敏感的标识。
                    var hasAppId = root.TryGetProperty("appid", out var appId) && !string.IsNullOrWhiteSpace(appId.GetString());
                    facts.Add(new AdapterFact("HasAppId", hasAppId ? "是" : "否", projectConfig.RelativePath, null));

                    evidences.Add(AdapterSupport.Evidence(context.Index, projectConfig.RelativePath, "小程序工程配置",
                        $"AppID {(hasAppId ? "已配置" : "未配置")}"));
                }
                catch (JsonException)
                {
                    evidences.Add(AdapterSupport.Evidence(context.Index, projectConfig.RelativePath, "小程序工程配置", "project.config.json 解析失败，已跳过"));
                }
            }
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints.Distinct().ToList(), facts);
    }
}
