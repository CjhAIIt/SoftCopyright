using System.Xml.Linq;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>.NET 适配器：解析 .sln / .csproj / Directory.Build.*，不执行任何工具链。</summary>
public sealed class DotNetAdapter : IProjectAdapter
{
    public string Id => "dotnet";

    public int Priority => 10;

    public bool CanHandle(ProjectFileIndex index) =>
        index.HasAny(".sln", ".csproj", "Directory.Build.props", "Directory.Build.targets") ||
        index.WithExtension(".csproj").Any();

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string> { ".NET" };
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "未在仓库中发现 .NET 项目文件。";

        var sln = context.Index.Files.FirstOrDefault(f => f.RelativePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        if (sln is not null)
        {
            var text = await context.Reader.ReadTextAsync(sln.RelativePath, context.Options.MaxReadableFileBytes, ct);
            var name = AdapterSupport.FirstNonEmptyLine(text);
            if (name.Length > 0)
            {
                facts.Add(new AdapterFact("SolutionName", name, sln.RelativePath, 1));
                evidences.Add(AdapterSupport.Evidence(context.Index, sln.RelativePath, "解决方案名称", name));
                summary = $"发现解决方案 {name}";
            }
        }

        // 项目文件按路径排序，并优先选择可执行应用工程（跳过测试工程），保证结果稳定可预期。
        var projectFiles = context.Index.WithExtension(".csproj")
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        var projectCandidates = new List<(ProjectFileEntry File, string? OutputType, bool IsTest)>();
        foreach (var project in projectFiles)
        {
            var text = await context.Reader.ReadTextAsync(project.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            try
            {
                var doc = XDocument.Parse(text);
                var root = doc.Root;
                if (root is null)
                {
                    continue;
                }

                var outputType = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;
                var isTest = root.Descendants().Any(e =>
                    e.Name.LocalName == "IsTestProject" && string.Equals(e.Value, "true", StringComparison.OrdinalIgnoreCase));

                projectCandidates.Add((project, outputType, isTest));
            }
            catch (System.Xml.XmlException)
            {
                // 解析失败的项目文件跳过，不影响其他项目。
            }
        }

        var ordered = projectCandidates
            .OrderByDescending(c => c.OutputType is not null &&
                                     string.Equals(c.OutputType, "Exe", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => !c.IsTest)
            .ThenBy(c => c.File.RelativePath, StringComparer.Ordinal);

        foreach (var (project, outputType, _) in ordered)
        {
            var text = await context.Reader.ReadTextAsync(project.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var root = XDocument.Parse(text).Root;
            if (root is null)
            {
                continue;
            }

            var assemblyName = Value(root, "AssemblyName");
            var rootNamespace = Value(root, "RootNamespace");
            var version = Value(root, "Version") ?? Value(root, "AssemblyVersion");
            var targetFramework = root.Descendants().Where(e => e.Name.LocalName == "TargetFramework").Select(e => e.Value).FirstOrDefault();
            var packages = root.Descendants().Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => e.Attribute("Include")?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            var displayName = assemblyName ?? rootNamespace ?? Path.GetFileNameWithoutExtension(project.RelativePath);
            facts.Add(new AdapterFact("Name", displayName, project.RelativePath, null));
            if (!string.IsNullOrWhiteSpace(version))
            {
                facts.Add(new AdapterFact("Version", version, project.RelativePath, null));
            }

            if (!string.IsNullOrWhiteSpace(targetFramework))
            {
                technologies.Add($"目标框架 {targetFramework}");
            }

            if (!string.IsNullOrWhiteSpace(outputType))
            {
                facts.Add(new AdapterFact("OutputType", outputType, project.RelativePath, null));
            }

            foreach (var package in packages.Take(20))
            {
                facts.Add(new AdapterFact("PackageReference", package, project.RelativePath, null));
            }

            evidences.Add(AdapterSupport.Evidence(
                context.Index, project.RelativePath, "项目文件",
                $"项目 {displayName}" +
                (string.IsNullOrWhiteSpace(targetFramework) ? string.Empty : $"，目标框架 {targetFramework}") +
                (string.IsNullOrWhiteSpace(version) ? string.Empty : $"，版本 {version}")));
        }

        foreach (var candidate in new[] { "Program.cs", "Startup.cs", "Main.cs", "Program.fs" })
        {
            var entry = context.Index.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f.RelativePath), candidate, StringComparison.OrdinalIgnoreCase) &&
                f.Category == FileCategory.Code);
            if (entry is not null)
            {
                entrypoints.Add(entry.RelativePath);
                evidences.Add(AdapterSupport.Evidence(context.Index, entry.RelativePath, "入口点候选", entry.RelativePath));
            }
        }

        if (entrypoints.Count == 0)
        {
            summary = string.IsNullOrEmpty(summary) ? summary : summary + "；未发现标准入口文件。";
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints, facts);
    }

    private static string? Value(XElement root, string localName) =>
        root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value is { Length: > 0 } v ? v : null;
}
