using System.Xml.Linq;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>Java 适配器：解析 pom.xml / build.gradle。</summary>
public sealed class JavaAdapter : IProjectAdapter
{
    public string Id => "java";

    public int Priority => 40;

    public bool CanHandle(ProjectFileIndex index) =>
        index.HasAny("pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle");

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string> { "Java" };
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "未发现可解析的 Java 构建文件。";

        var pom = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "pom.xml", StringComparison.OrdinalIgnoreCase));

        if (pom is not null)
        {
            var text = await context.Reader.ReadTextAsync(pom.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var doc = XDocument.Parse(text);
                    var rootElement = doc.Root;
                    if (rootElement is not null)
                    {
                        var artifactId = LocalValue(rootElement, "artifactId");
                        var groupId = LocalValue(rootElement, "groupId");
                        var version = LocalValue(rootElement, "version");

                        if (!string.IsNullOrWhiteSpace(artifactId))
                        {
                            facts.Add(new AdapterFact("Name", artifactId, pom.RelativePath, null));
                            summary = $"发现 Maven 项目 {artifactId}";
                        }

                        if (!string.IsNullOrWhiteSpace(groupId))
                        {
                            facts.Add(new AdapterFact("GroupId", groupId, pom.RelativePath, null));
                        }

                        if (!string.IsNullOrWhiteSpace(version))
                    {
                        facts.Add(new AdapterFact("Version", version, pom.RelativePath, null));
                    }

                        var dependencies = doc.Descendants().Where(e => e.Name.LocalName == "dependency")
                            .Select(e => LocalValue(e, "artifactId"))
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Take(20)
                            .ToList();

                        foreach (var dependency in dependencies)
                        {
                            facts.Add(new AdapterFact("Dependency", dependency, pom.RelativePath, null));
                            technologies.Add(dependency!);
                        }

                        evidences.Add(AdapterSupport.Evidence(context.Index, pom.RelativePath, "构建配置",
                            $"Maven 项目 {artifactId}（版本 {version ?? "未声明"}）"));
                    }
                }
                catch (System.Xml.XmlException)
                {
                    evidences.Add(AdapterSupport.Evidence(context.Index, pom.RelativePath, "构建配置", "pom.xml 解析失败，已跳过"));
                }
            }
        }

        var gradle = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "build.gradle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(f.RelativePath), "build.gradle.kts", StringComparison.OrdinalIgnoreCase));

        if (gradle is not null)
        {
            var text = await context.Reader.ReadTextAsync(gradle.RelativePath, context.Options.MaxReadableFileBytes, ct);
            var group = FindAssignment(text ?? string.Empty, "group");
            var version = FindAssignment(text ?? string.Empty, "version");

            if (!string.IsNullOrWhiteSpace(group))
            {
                facts.Add(new AdapterFact("Group", group, gradle.RelativePath, null));
                summary = string.IsNullOrEmpty(summary) ? "发现 Gradle 项目" : summary;
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                facts.Add(new AdapterFact("Version", version, gradle.RelativePath, null));
            }

            evidences.Add(AdapterSupport.Evidence(context.Index, gradle.RelativePath, "构建配置", "Gradle 构建脚本"));
        }

        foreach (var entry in context.Index.Files.Where(f => f.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase)).Take(5))
        {
            entrypoints.Add(entry.RelativePath);
            evidences.Add(AdapterSupport.Evidence(context.Index, entry.RelativePath, "源码候选", entry.RelativePath));
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints, facts);
    }

    private static string? LocalValue(XElement root, string localName)
    {
        var element = root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        return element is null ? null : element.Value;
    }

    private static string? FindAssignment(string text, string key)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim().Trim('"', '\'', ' ');
            if (value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }
}
