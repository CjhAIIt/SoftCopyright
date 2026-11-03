using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>Python 适配器：解析 pyproject.toml / requirements.txt / setup.py / manage.py。</summary>
public sealed class PythonAdapter : IProjectAdapter
{
    public string Id => "python";

    public int Priority => 30;

    public bool CanHandle(ProjectFileIndex index) =>
        index.HasAny("pyproject.toml", "requirements.txt", "setup.py", "setup.cfg", "manage.py", "Pipfile");

    public async Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct)
    {
        var evidences = new List<EvidenceEntry>();
        var technologies = new List<string> { "Python" };
        var entrypoints = new List<string>();
        var facts = new List<AdapterFact>();
        var summary = "未发现可解析的 Python 项目配置。";

        var pyproject = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "pyproject.toml", StringComparison.OrdinalIgnoreCase));

        if (pyproject is not null)
        {
            var text = await context.Reader.ReadTextAsync(pyproject.RelativePath, context.Options.MaxReadableFileBytes, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var (name, version, description, dependencies) = ParsePyProject(text);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    facts.Add(new AdapterFact("Name", name, pyproject.RelativePath, null));
                    summary = $"发现 Python 项目 {name}";
                }

                if (!string.IsNullOrWhiteSpace(version))
                {
                    facts.Add(new AdapterFact("Version", version, pyproject.RelativePath, null));
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    facts.Add(new AdapterFact("Description", description, pyproject.RelativePath, null));
                }

                foreach (var dependency in dependencies)
                {
                    facts.Add(new AdapterFact("Dependency", dependency, pyproject.RelativePath, null));
                    technologies.Add(dependency.Split('=')[0].Trim());
                }

                evidences.Add(AdapterSupport.Evidence(context.Index, pyproject.RelativePath, "项目配置",
                    $"Python 项目 {name}（版本 {version ?? "未声明"}）"));
            }
        }

        var requirements = context.Index.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePath), "requirements.txt", StringComparison.OrdinalIgnoreCase));

        if (requirements is not null)
        {
            var text = await context.Reader.ReadTextAsync(requirements.RelativePath, context.Options.MaxReadableFileBytes, ct);
            var packages = (text ?? string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .Take(30)
                .ToList();

            foreach (var package in packages)
            {
                facts.Add(new AdapterFact("Requirement", package, requirements.RelativePath, null));
                technologies.Add(package.Split('=')[0].Split('>')[0].Split('<')[0].Trim());
            }

            evidences.Add(AdapterSupport.Evidence(context.Index, requirements.RelativePath, "依赖清单",
                packages.Count == 0 ? "依赖清单为空" : string.Join(", ", packages.Take(10))));
        }

        foreach (var candidate in new[] { "__main__.py", "main.py", "app.py", "wsgi.py", "run.py", "manage.py" })
        {
            var entry = context.Index.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f.RelativePath), candidate, StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                entrypoints.Add(entry.RelativePath);
                evidences.Add(AdapterSupport.Evidence(context.Index, entry.RelativePath, "入口点候选", entry.RelativePath));
            }
        }

        return new AdapterAnalysis(Id, true, summary, evidences, technologies.Distinct().ToList(), entrypoints, facts);
    }

    /// <summary>解析 pyproject.toml 的 [project] 段（只做轻量行解析，不引入 TOML 依赖）。</summary>
    internal static (string? Name, string? Version, string? Description, List<string> Dependencies) ParsePyProject(string text)
    {
        string? name = null;
        string? version = null;
        string? description = null;
        var dependencies = new List<string>();

        var lines = text.Split('\n');
        var inProject = false;
        var inDependencies = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                inProject = line.Contains("project", StringComparison.OrdinalIgnoreCase) &&
                             !line.Contains("optional", StringComparison.OrdinalIgnoreCase);
                inDependencies = line.Contains("dependencies", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inDependencies)
            {
                var dependencyName = line.Trim('"', '\'', ',');
                if (dependencyName.Length > 0 && !dependencyName.StartsWith('#'))
                {
                    dependencies.Add(dependencyName);
                }

                continue;
            }

            if (!inProject)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'', ',');

            switch (key)
            {
                case "name":
                    name ??= value;
                    break;
                case "version":
                    version ??= value;
                    break;
                case "description":
                    description ??= value;
                    break;
            }
        }

        return (name, version, description, dependencies);
    }
}
