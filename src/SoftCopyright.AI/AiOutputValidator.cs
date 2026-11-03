using System.Text.Json;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI;

/// <summary>模型输出校验：强校验 JSON 结构，并回验每条证据引用确实存在于证据包中。</summary>
public sealed class AiOutputValidator(ILogger<AiOutputValidator> logger) : IAiOutputValidator
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    public AiOutputValidationResult Validate(string json, EvidencePack pack)
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AiOutputValidationResult(false, [], [], ["模型未返回内容。"], "模型输出为空。");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, ParseOptions);
        }
        catch (JsonException ex)
        {
            return new AiOutputValidationResult(false, [], [], [], $"模型输出不是合法 JSON：{ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new AiOutputValidationResult(false, [], [], [], "模型输出根节点不是 JSON 对象。");
            }

            var features = new List<Fact<string>>();
            if (root.TryGetProperty("features", out var featureArray) && featureArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in featureArray.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var value = item.TryGetProperty("value", out var v) ? v.GetString() : null;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    var status = ParseStatus(item.TryGetProperty("status", out var s) ? s.GetString() : null);
                    var explanation = item.TryGetProperty("explanation", out var e) ? e.GetString() : null;
                    var refs = new List<EvidenceRef>();

                    if (item.TryGetProperty("evidence", out var evidenceArray) && evidenceArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var reference in evidenceArray.EnumerateArray())
                        {
                            if (reference.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            var path = reference.TryGetProperty("relativePath", out var p) ? p.GetString() : null;
                            if (string.IsNullOrWhiteSpace(path))
                            {
                                continue;
                            }

                            var sha = reference.TryGetProperty("sha256", out var sh) ? sh.GetString() : string.Empty;
                            var startLine = reference.TryGetProperty("startLine", out var sl) && sl.ValueKind == JsonValueKind.Number ? sl.GetInt32() : (int?)null;
                            var endLine = reference.TryGetProperty("endLine", out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : (int?)null;
                            var type = reference.TryGetProperty("evidenceType", out var t) ? t.GetString() ?? "未分类" : "未分类";

                            // 回验：证据必须真实存在于证据包中。
                            var match = pack.Items.FirstOrDefault(i => string.Equals(i.RelativePath, path, StringComparison.OrdinalIgnoreCase));
                            if (match is null)
                            {
                                warnings.Add($"模型引用的证据路径不存在于证据包，已丢弃该引用：{path}");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(sha) &&
                                !string.Equals(match.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                            {
                                warnings.Add($"模型引用的证据哈希与实际内容不一致，已丢弃该引用：{path}");
                                continue;
                            }

                            refs.Add(new EvidenceRef(path, startLine ?? match.StartLine, endLine ?? match.EndLine, type, match.Sha256));
                        }
                    }

                    if (refs.Count == 0 && status == FactStatus.Observed)
                    {
                        // 没有可验证证据的功能陈述不得写成已验证事实。
                        status = FactStatus.Inferred;
                        explanation = string.IsNullOrWhiteSpace(explanation)
                            ? "模型未提供可验证的证据引用，已降级为推断。"
                            : explanation + "（缺少可验证证据引用，已降级为推断）";
                        warnings.Add($"功能陈述「{Truncate(value)}」缺少有效证据引用，已降级为推断。");
                    }

                    features.Add(new Fact<string>(value, status, refs, explanation));
                }
            }

            var sections = new List<string>();
            if (root.TryGetProperty("operationSections", out var sectionArray) && sectionArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sectionArray.EnumerateArray())
                {
                    var text = section.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sections.Add(text.Trim());
                    }
                }
            }

            if (features.Count == 0 && sections.Count == 0)
            {
                return new AiOutputValidationResult(false, [], [], warnings, "模型未返回可用的功能描述或章节。");
            }

            return new AiOutputValidationResult(true, features, sections, warnings, null);
        }
    }

    public ProjectAnalysisResult Fallback(ProjectProfile baseline, string reason)
    {
        logger.LogWarning("模型分析不可用，已回退到规则分析：{Reason}", reason);
        return new ProjectAnalysisResult(baseline, [reason]);
    }

    private static FactStatus ParseStatus(string? raw) => raw switch
    {
        "Observed" => FactStatus.Observed,
        "Inferred" => FactStatus.Inferred,
        "ApplicantConfirmed" => FactStatus.ApplicantConfirmed,
        _ => FactStatus.Inferred
    };

    private static string Truncate(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
