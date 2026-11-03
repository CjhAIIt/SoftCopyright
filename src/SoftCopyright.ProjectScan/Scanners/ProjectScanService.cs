using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.ProjectScan.Adapters;
using SoftCopyright.ProjectScan.Files;

namespace SoftCopyright.ProjectScan.Scanners;

/// <summary>通用仓库扫描器：建立文件索引、提取证据、调用适配器并生成项目画像。</summary>
public sealed class ProjectScanService(
    IOptions<ScanOptions> options,
    IEnumerable<IProjectAdapter> adapters,
    ILogger<ProjectScanService> logger) : IProjectScanService
{
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".rst", ".adoc", ".org", ".markdownx"
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".config", ".properties", ".xml",
        ".csproj", ".fsproj", ".vbproj", ".props", ".targets", ".sln", ".env.example"
    };

    private static readonly HashSet<string> ConfigFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "pyproject.toml", "requirements.txt", "setup.py", "pom.xml",
        "build.gradle", "settings.gradle", "dockerfile", "makefile", "project.config.json",
        "app.json", "global.json", "directory.build.props", "cargo.toml"
    };

    private static readonly HashSet<string> KnownProgrammingLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "C#", "VB.NET", "F#", "JavaScript", "TypeScript", "Vue", "Svelte", "Python", "Java",
        "Kotlin", "Go", "Rust", "Ruby", "PHP", "C", "C++", "Swift", "Objective-C", "Dart",
        "Scala", "Shell", "PowerShell", "SQL", "批处理", "WXML", "WXSS", "WXS"
    };

    public async Task<ProjectScanResult> ScanAsync(ProjectScanRequest request, CancellationToken ct)
    {
        var opts = options.Value;
        var root = request.ExtractedRoot;
        var reader = new LocalProjectFileReader(root);

        var files = new List<ProjectFileEntry>();
        var warnings = new List<string>();

        if (!Directory.Exists(root))
        {
            return EmptyResult("解包目录不存在，无法扫描。");
        }
        foreach (var fullPath in EnumerateFiles(root))
        {
            ct.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            var info = new FileInfo(fullPath);

            byte[] bytes = [];
            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var buffer = new MemoryStream();
                var readBuffer = new byte[81920];
                var read = await stream.ReadAsync(readBuffer, ct);
                buffer.Write(readBuffer, 0, read);
                bytes = buffer.ToArray();
            }
            catch (IOException ex)
            {
                warnings.Add($"文件 {relative} 读取失败，已跳过：{ex.Message}");
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                warnings.Add($"文件 {relative} 无读取权限，已跳过。");
                continue;
            }

            var (isText, encoding) = TextDetection.Detect(bytes);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            var userExclusion = ExclusionRules.EvaluateUserExclusions(relative, opts, request.UserExcludedDirectories);
            var isExcluded = userExclusion.Excluded;
            var exclusionReason = userExclusion.Reason;

            // 安全类排除不可取消。
            if (!isExcluded && !isText)
            {
                isExcluded = true;
                exclusionReason = "二进制文件，不参与文本分析";
            }

            if (!isExcluded && info.Length > opts.MaxReadableFileBytes)
            {
                isExcluded = true;
                exclusionReason = "文件过大，仅记录元数据";
            }

            var headText = isText && !isExcluded
                ? TextDetection.Decode(bytes[..Math.Min(bytes.Length, opts.TextSniffBytes)], encoding)
                : null;

            var language = isText && !isExcluded ? LanguageDetector.Detect(relative, headText) : null;
            var category = DetermineCategory(relative, isText, language);
            var lineCount = isText ? CountLines(bytes, encoding) : (int?)null;

            var secretRisk = SecretRisk.None;
            if (!isExcluded)
            {
                if (SecretDetector.IsSensitiveFileName(relative) ||
                    (headText is not null && SecretDetector.ContainsSecretContent(headText)))
                {
                    secretRisk = SecretRisk.Suspected;
                    isExcluded = true;
                    exclusionReason = "疑似凭据文件，已排除并禁止发送给模型";
                }
            }

            var isCodeCandidate = !isExcluded
                                  && category == FileCategory.Code
                                  && language is not null
                                  && KnownProgrammingLanguages.Contains(language);

            files.Add(new ProjectFileEntry(
                relative,
                info.Length,
                sha,
                isText,
                encoding,
                language,
                category,
                isExcluded,
                exclusionReason,
                lineCount,
                secretRisk,
                isCodeCandidate));
        }

        var index = new ProjectFileIndex { Files = files };
        var evidences = new List<EvidenceEntry>();
        var adapterResults = new List<AdapterAnalysis>();

        var context = new ProjectScanContext
        {
            Index = index,
            RootPath = root,
            CaseId = request.CaseId.ToString("D"),
            Reader = reader,
            Options = opts
        };

        foreach (var adapter in adapters.OrderBy(a => a.Priority))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!adapter.CanHandle(index))
                {
                    continue;
                }

                var result = await adapter.AnalyzeAsync(context, ct);
                if (result.Matched)
                {
                    adapterResults.Add(result);
                    evidences.AddRange(result.Evidences);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"适配器 {adapter.Id} 执行失败，已跳过：{ex.Message}");
                logger.LogWarning(ex, "适配器 {AdapterId} 执行失败", adapter.Id);
            }
        }

        evidences.AddRange(await CollectReadmeEvidenceAsync(index, reader, ct));

        var (profile, openQuestions) = BuildProfile(index, adapterResults, evidences);

        var coverage = BuildCoverage(index, adapterResults, profile);

        logger.LogInformation(
            "扫描完成：文件 {Total}，纳入 {Included}，代码候选 {Candidates}，证据 {Evidence}",
            files.Count,
            files.Count(f => !f.IsExcluded),
            files.Count(f => f.IsCodeCandidate),
            evidences.Count);

        return new ProjectScanResult(
            files,
            evidences,
            adapterResults,
            profile,
            coverage,
            openQuestions,
            AnalyzerVersion,
            warnings);
    }

    public const string AnalyzerVersion = "1.0.0";

    private static ProjectScanResult EmptyResult(string warning) =>
        new(
            [],
            [],
            [],
            new ProjectProfile(
                Fact<string>.Unknown("无扫描结果"),
                Fact<string>.Unknown("无扫描结果"),
                Fact<string>.Unknown("无扫描结果"),
                [],
                [],
                [],
                ["仓库内容为空，请重新上传压缩包。"],
                AnalyzerVersion),
            new CoverageSummary([]),
            ["仓库内容为空，请重新上传压缩包。"],
            AnalyzerVersion,
            [warning]);

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;

            try
            {
                files = Directory.EnumerateFiles(current).ToList();
                directories = Directory.EnumerateDirectories(current).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var directory in directories)
            {
                stack.Push(directory);
            }
        }
    }

    private static FileCategory DetermineCategory(string relativePath, bool isText, string? language)
    {
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(relativePath);

        if (!isText)
        {
            return FileCategory.Binary;
        }

        if (fileName.StartsWith("README", StringComparison.OrdinalIgnoreCase) ||
            DocumentExtensions.Contains(extension))
        {
            return FileCategory.Doc;
        }

        if (ConfigExtensions.Contains(extension) || ConfigFileNames.Contains(fileName))
        {
            return FileCategory.Config;
        }

        if (language is not null && KnownProgrammingLanguages.Contains(language))
        {
            return FileCategory.Code;
        }

        return FileCategory.Unknown;
    }

    private static int CountLines(byte[] bytes, string encoding)
    {
        var text = TextDetection.Decode(bytes, encoding);
        if (text.Length == 0)
        {
            return 0;
        }

        var count = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static async Task<List<EvidenceEntry>> CollectReadmeEvidenceAsync(
        ProjectFileIndex index,
        IProjectFileReader reader,
        CancellationToken ct)
    {
        var result = new List<EvidenceEntry>();

        var readme = index.Files.FirstOrDefault(f =>
            f.Category == FileCategory.Doc &&
            Path.GetFileName(f.RelativePath).StartsWith("README", StringComparison.OrdinalIgnoreCase));

        if (readme is null)
        {
            return result;
        }

        var text = await reader.ReadTextAsync(readme.RelativePath, 64 * 1024, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var title = GenericAdapter.ExtractTitle(text);
        if (!string.IsNullOrWhiteSpace(title))
        {
            result.Add(AdapterSupport.Evidence(index, readme.RelativePath, "README 标题", title, EvidenceSource.Scanner));
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("### ", StringComparison.Ordinal))
            {
                continue;
            }

            var heading = line.TrimStart('#').Trim();
            if (heading.Length == 0)
            {
                continue;
            }

            result.Add(AdapterSupport.Evidence(
                index,
                readme.RelativePath,
                "功能条目",
                heading,
                EvidenceSource.Scanner,
                i + 1,
                i + 1));
        }

        return result;
    }

    private static (ProjectProfile Profile, List<string> OpenQuestions) BuildProfile(
        ProjectFileIndex index,
        IReadOnlyList<AdapterAnalysis> adapters,
        IReadOnlyList<EvidenceEntry> evidences)
    {
        var questions = new List<string>();

        var nameFact = ResolveFact(adapters, evidences, "Name", FactStatus.Observed);
        var versionFact = ResolveFact(adapters, evidences, "Version", FactStatus.Observed);

        if (nameFact.Value is null)
        {
            var readmeTitle = adapters
                .SelectMany(a => a.Facts)
                .FirstOrDefault(f => f.Key == "ReadmeTitle");
            if (!string.IsNullOrWhiteSpace(readmeTitle?.Value))
            {
                var evidence = evidences.FirstOrDefault(e =>
                    e.EvidenceType == "README 标题" &&
                    string.Equals(e.RelativePath, readmeTitle.EvidencePath, StringComparison.OrdinalIgnoreCase));

                nameFact = new Fact<string>(
                    readmeTitle.Value,
                    FactStatus.Observed,
                    evidence is null ? [] : [ToRef(evidence)],
                    "取自 README 标题");
            }
        }

        if (nameFact.Value is null)
        {
            nameFact = Fact<string>.Unknown("未在仓库中发现软件名称");
            questions.Add("未在仓库中发现软件名称，请申请人填写软件全称。");
        }

        if (versionFact.Value is null)
        {
            versionFact = Fact<string>.Unknown("未在仓库中发现版本号");
            questions.Add("未在仓库中发现版本号，请申请人确认版本号。");
        }

        var descriptionFact = ResolveFact(adapters, evidences, "Description", FactStatus.Observed);
        var purposeFact = descriptionFact.Value is not null
            ? descriptionFact
            : ResolveFact(adapters, evidences, "ReadmeDescription", FactStatus.Observed);

        if (purposeFact.Value is null)
        {
            purposeFact = Fact<string>.Unknown("仓库中缺少可用的用途说明");
            questions.Add("仓库中没有可用的用途说明，请申请人补充软件用途与功能简介。");
        }

        var features = BuildFeatures(index, evidences);
        if (features.Count == 0)
        {
            questions.Add("未能从文档中提取到功能条目，请申请人补充功能清单。");
        }

        var technologies = adapters
            .SelectMany(a => a.Technologies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entrypoints = adapters
            .SelectMany(a => a.Entrypoints)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        if (entrypoints.Count == 0)
        {
            questions.Add("未识别到程序入口点，请申请人补充运行方式。");
        }

        var profile = new ProjectProfile(
            nameFact,
            versionFact,
            purposeFact,
            features,
            technologies,
            entrypoints,
            questions,
            AnalyzerVersion);

        return (profile, questions);
    }

    private static Fact<string> ResolveFact(
        IReadOnlyList<AdapterAnalysis> adapters,
        IReadOnlyList<EvidenceEntry> evidences,
        string key,
        FactStatus status)
    {
        foreach (var adapter in adapters)
        {
            var fact = adapter.Facts.FirstOrDefault(f => f.Key == key && !string.IsNullOrWhiteSpace(f.Value));
            if (fact is null)
            {
                continue;
            }

            var evidence = evidences.FirstOrDefault(e =>
                string.Equals(e.RelativePath, fact.EvidencePath, StringComparison.OrdinalIgnoreCase));

            var refs = evidence is null ? [] : new List<EvidenceRef> { ToRef(evidence) };
            return new Fact<string>(fact.Value, status, refs, $"来自适配器 {adapter.AdapterId} 抽取的配置");
        }

        return Fact<string>.Unknown($"未抽取到 {key}");
    }

    private static List<Fact<string>> BuildFeatures(ProjectFileIndex index, IReadOnlyList<EvidenceEntry> evidences)
    {
        var features = new List<Fact<string>>();

        var sectionEvidences = evidences
            .Where(e => e.EvidenceType == "功能条目")
            .ToList();

        foreach (var evidence in sectionEvidences.Take(12))
        {
            features.Add(new Fact<string>(
                evidence.Excerpt,
                FactStatus.Observed,
                [ToRef(evidence)],
                "来自 README 章节标题"));
        }

        if (features.Count == 0)
        {
            var readmeEvidence = evidences.FirstOrDefault(e => e.EvidenceType == "README 标题");
            if (readmeEvidence is not null)
            {
                features.Add(new Fact<string>(
                    "项目说明与功能概述（详见 README）",
                    FactStatus.Observed,
                    [ToRef(readmeEvidence)],
                    "来自 README 文档"));
            }
        }

        return features;
    }

    private static CoverageSummary BuildCoverage(
        ProjectFileIndex index,
        IReadOnlyList<AdapterAnalysis> adapters,
        ProjectProfile profile)
    {
        var entrypoints = adapters.SelectMany(a => a.Entrypoints).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var hasReadme = index.Files.Any(f =>
            f.Category == FileCategory.Doc &&
            Path.GetFileName(f.RelativePath).StartsWith("README", StringComparison.OrdinalIgnoreCase));
        var topLevelDirectories = index.Files
            .Select(f => f.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var codeCandidates = index.Files.Count(f => f.IsCodeCandidate);
        var configFiles = index.Files.Count(f => f.Category == FileCategory.Config);
        var hasVersion = profile.Version.Value is not null;

        var items = new List<CoverageItem>
        {
            new("程序入口点", entrypoints.Count > 0, entrypoints.Count > 0 ? $"识别到 {entrypoints.Count} 个入口候选" : "未识别到入口点"),
            new("项目文档", hasReadme, hasReadme ? "已找到 README" : "仓库中没有 README"),
            new("主要模块", topLevelDirectories.Count >= 2, $"顶层目录 {topLevelDirectories.Count} 个"),
            new("业务流程", profile.Features.Count > 0, profile.Features.Count > 0 ? $"提取到 {profile.Features.Count} 条功能描述" : "未能提取功能描述"),
            new("代码候选", codeCandidates > 0, $"代码候选 {codeCandidates} 个"),
            new("运行配置", configFiles > 0, $"配置文件 {configFiles} 个"),
            new("版本信息", hasVersion, hasVersion ? $"版本 {profile.Version.Value}" : "未发现版本号")
        };

        return new CoverageSummary(items);
    }

    private static EvidenceRef ToRef(EvidenceEntry evidence) =>
        new(evidence.RelativePath, evidence.StartLine, evidence.EndLine, evidence.EvidenceType, evidence.Sha256);
}
