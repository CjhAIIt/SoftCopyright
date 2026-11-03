using System.Text.Json;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>Node / Web 适配器：解析 package.json 与页面、路由候选。</summary>
public sealed class NodeAdapter : IProjectAdapter
{
    public string Id => "node";

    public int Priority => 20;

    public bool CanHandle(ProjectFileIndex index) =>
        index.HasAny("package.json", "next.config.js", "next.config.mjs", "vite.config.ts", "vite.config.js", "nuxt.config.ts");

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string>();
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "未发现可解析的 Node 项目配置。";

        var packageFile = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "package.json", StringComparison.OrdinalIgnoreCase));

        if (packageFile is not null)
        {
            var text = await context.Reader.ReadTextAsync(packageFile.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("name", out var nameElement) && nameElement.GetString() is { Length: > 0 } name)
                    {
                        facts.Add(new AdapterFact("Name", name, packageFile.RelativePath, null));
                        summary = $"发现 Node 项目 {name}";
                    }

                    if (root.TryGetProperty("version", out var versionElement) && versionElement.GetString() is { Length: > 0 } version)
                    {
                        facts.Add(new AdapterFact("Version", version, packageFile.RelativePath, null));
                    }

                    if (root.TryGetProperty("description", out var descElement) && descElement.GetString() is { Length: > 0 } description)
                    {
                        facts.Add(new AdapterFact("Description", description, packageFile.RelativePath, null));
                    }

                    if (root.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var script in scripts.EnumerateObject())
                        {
                            facts.Add(new AdapterFact("Script", $"{script.Name}: {script.Value.GetString()}", packageFile.RelativePath, null));
                        }
                    }

                    foreach (var group in new[] { "dependencies", "devDependencies" })
                    {
                        if (root.TryGetProperty(group, out var deps) && deps.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var dep in deps.EnumerateObject())
                            {
                                facts.Add(new AdapterFact("Dependency", $"{dep.Name}@{dep.Value.GetString()}", packageFile.RelativePath, null));
                                technologies.Add(dep.Name);
                            }
                        }
                    }

                    evidences.Add(AdapterSupport.Evidence(context.Index, packageFile.RelativePath, "项目配置",
                        $"Node 项目 {facts.FirstOrDefault(f => f.Key == "Name")?.Value}"));
                }
                catch (JsonException)
                {
                    evidences.Add(AdapterSupport.Evidence(context.Index, packageFile.RelativePath, "项目配置", "package.json 解析失败，已跳过"));
                }
            }
        }

        foreach (var candidate in new[] { "index.js", "index.ts", "server.js", "app.js", "main.js", "app.ts", "main.ts" })
        {
            var entry = context.Index.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f.RelativePath), candidate, StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                entrypoints.Add(entry.RelativePath);
                evidences.Add(AdapterSupport.Evidence(context.Index, entry.RelativePath, "入口点候选", entry.RelativePath));
            }
        }

        foreach (var directory in new[] { "pages", "app", "src/pages", "src/routes", "src/views" })
        {
            var match = context.Index.Files.FirstOrDefault(f =>
                f.RelativePath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                entrypoints.Add(match.RelativePath);
                technologies.Add($"前端目录 {directory}");
            }
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints, facts);
    }
}
