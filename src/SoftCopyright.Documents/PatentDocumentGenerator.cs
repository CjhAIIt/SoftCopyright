using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Documents.Docx;

namespace SoftCopyright.Documents;

/// <summary>
/// 专利申请文件生成器。
///
/// 产出四份文件：权利要求书、说明书、摘要、技术交底书。
/// 内容全部来自技术交底书与已生成的权利要求，不做任何编造；
/// 每一处技术特征都附带证据引用，便于代理人核对。
/// </summary>
public sealed class PatentDocumentGenerator(ILogger<PatentDocumentGenerator> logger) : IPatentDocumentGenerator
{
    public Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(
        PatentDocumentRequest request,
        CancellationToken ct)
    {
        var documents = new List<GeneratedDocument>
        {
            BuildClaimsDocument(request),
            BuildSpecificationDocument(request),
            BuildAbstractDocument(request),
            BuildDisclosureDocument(request)
        };

        logger.LogInformation(
            "已生成 {Count} 份专利申请文件（权利要求 {Claims} 条）",
            documents.Count,
            request.Claims.Count);

        return Task.FromResult<IReadOnlyList<GeneratedDocument>>(documents);
    }

    private static GeneratedDocument BuildClaimsDocument(PatentDocumentRequest request)
    {
        using var document = DocxWriter.Create();

        DocxWriter.AddHeading(document, "权利要求书", 1);
        DocxWriter.AddParagraph(document, $"（{request.Title}）", justification: JustificationValues.Center);

        if (request.Claims.Count == 0)
        {
            DocxWriter.AddParagraph(document, "【尚未生成权利要求，请在工作台生成专利草稿】");
        }
        else
        {
            foreach (var claim in request.Claims.OrderBy(c => c.Number))
            {
                // 编号统一由文档生成器输出，正文只保留权利要求内容，避免编号与正文不一致。
                DocxWriter.AddParagraph(document, $"{claim.Number}. {claim.Text}");
            }
        }

        DocxWriter.AddHeading(document, "权利要求校验", 2);
        foreach (var result in request.ValidationResults.Where(r => r.RuleId.StartsWith("P-", StringComparison.Ordinal)))
        {
            DocxWriter.AddParagraph(document, $"· {result.RuleId}：{result.Message}");
        }

        var bytes = document.ToBytes();
        return new GeneratedDocument(
            "权利要求书.docx",
            "正式资料/权利要求书.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            bytes);
    }

    private static GeneratedDocument BuildSpecificationDocument(PatentDocumentRequest request)
    {
        var disclosure = request.Disclosure;
        using var document = DocxWriter.Create();

        DocxWriter.AddHeading(document, "说明书", 1);
        DocxWriter.AddParagraph(document, $"{request.Title}", justification: JustificationValues.Center);

        DocxWriter.AddHeading(document, "技术领域", 2);
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.TechnicalField, "【待申请人补充技术领域】"));

        DocxWriter.AddHeading(document, "背景技术", 2);
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.BackgroundTechnology, "【待申请人补充背景技术】"));
        if (!string.IsNullOrWhiteSpace(disclosure.PriorArt))
        {
            DocxWriter.AddParagraph(document, $"现有技术：{disclosure.PriorArt}");
            if (!string.IsNullOrWhiteSpace(disclosure.PriorArtSources))
            {
                DocxWriter.AddParagraph(document, $"现有技术来源：{disclosure.PriorArtSources}");
            }
        }

        DocxWriter.AddHeading(document, "发明内容", 2);
        DocxWriter.AddParagraph(document, "本申请所要解决的技术问题是：");
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.ProblemStatement, "【待申请人补充要解决的技术问题】"));
        DocxWriter.AddParagraph(document, "为解决上述技术问题，本申请采用如下技术方案：");
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.TechnicalSolution, "【待申请人补充技术方案】"));

        var features = Deserialize<TechnicalFeature>(disclosure.TechnicalFeaturesJson);
        if (features.Count > 0)
        {
            DocxWriter.AddParagraph(document, "进一步地，所述技术方案包括以下技术特征：");
            var index = 1;
            foreach (var feature in features)
            {
                var evidence = feature.Evidence.FirstOrDefault();
                var suffix = evidence is null
                    ? "【未找到可回溯证据，待申请人核对】"
                    : $"【依据：{evidence.RelativePath}{(evidence.StartLine is null ? string.Empty : $" 第 {evidence.StartLine} 行")}】";
                DocxWriter.AddParagraph(document, $"{index}）{feature.Description}{suffix}");
                index++;
            }
        }

        DocxWriter.AddParagraph(document, "本申请的有益效果是：");
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.BeneficialEffects, "【待申请人补充有益效果】"));

        DocxWriter.AddHeading(document, "附图说明", 2);
        DocxWriter.AddParagraph(document, OrPlaceholder(disclosure.DrawingDescription, "【待申请人补充附图说明】"));
        DocxWriter.AddParagraph(document, "注：本工具暂不自动生成附图，图号与附图请由申请人补充后对应本节。");

        DocxWriter.AddHeading(document, "具体实施方式", 2);
        var embodiments = Deserialize<Embodiment>(disclosure.EmbodimentsJson);
        if (embodiments.Count == 0)
        {
            DocxWriter.AddParagraph(document, "【尚未生成实施例，请在技术交底书中补充具体实施方式】");
        }
        else
        {
            foreach (var embodiment in embodiments)
            {
                DocxWriter.AddHeading(document, embodiment.Title, 3);
                DocxWriter.AddParagraph(document, embodiment.Description);
                foreach (var evidence in embodiment.Evidence)
                {
                    DocxWriter.AddParagraph(
                        document,
                        $"【实现依据：{evidence.RelativePath}{(evidence.StartLine is null ? string.Empty : $" 第 {evidence.StartLine} 行")}，SHA-256 {evidence.Sha256[..Math.Min(16, evidence.Sha256.Length)]}】",
                        DocxWriter.CodeFont,
                        18);
                }
            }
        }

        DocxWriter.AddHeading(document, "说明", 2);
        DocxWriter.AddParagraph(document, "本文档由知识产权申请材料工具依据技术交底书与仓库证据自动生成，属于申请文件初稿；权利要求的保护范围、新颖性与创造性必须由专利代理人复核。");
        DocxWriter.AddParagraph(document, request.RulesSource);

        var bytes = document.ToBytes();
        return new GeneratedDocument(
            "说明书.docx",
            "正式资料/说明书.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            bytes);
    }

    private static GeneratedDocument BuildAbstractDocument(PatentDocumentRequest request)
    {
        using var document = DocxWriter.Create();

        DocxWriter.AddHeading(document, "摘要", 1);
        DocxWriter.AddParagraph(document, OrPlaceholder(request.Disclosure.Abstract, "【尚未生成摘要】"));

        var length = request.Disclosure.Abstract.Length;
        DocxWriter.AddParagraph(
            document,
            $"（摘要字数：{length}，上限 {300} 字）",
            DocxWriter.BodyFont,
            18);

        DocxWriter.AddParagraph(document, "注：摘要文字不得包含商业性宣传用语，且不得超过规定字数。");

        var bytes = document.ToBytes();
        return new GeneratedDocument(
            "摘要.docx",
            "正式资料/摘要.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            bytes);
    }

    private static GeneratedDocument BuildDisclosureDocument(PatentDocumentRequest request)
    {
        var disclosure = request.Disclosure;
        using var document = DocxWriter.Create();

        DocxWriter.AddHeading(document, "技术交底书", 1);
        DocxWriter.AddParagraph(document, $"（{request.Title}）", justification: JustificationValues.Center);

        AddRow(document, "发明名称", disclosure.Title);
        AddRow(document, "专利类型", disclosure.PatentType == PatentType.Invention ? "发明专利" : "实用新型");
        AddRow(document, "申请人", disclosure.ApplicantName);
        AddRow(document, "发明人", disclosure.InventorNames);
        AddRow(document, "版本", request.Version);
        AddRow(document, "技术领域", disclosure.TechnicalField);
        AddRow(document, "要解决的技术问题", disclosure.ProblemStatement);
        AddRow(document, "技术方案", disclosure.TechnicalSolution);
        AddRow(document, "有益效果", disclosure.BeneficialEffects);
        AddRow(document, "附图说明", disclosure.DrawingDescription);
        AddRow(document, "现有技术", disclosure.PriorArt);
        AddRow(document, "现有技术来源", disclosure.PriorArtSources);

        DocxWriter.AddHeading(document, "技术特征与证据对应表", 2);
        var features = Deserialize<TechnicalFeature>(disclosure.TechnicalFeaturesJson);
        if (features.Count == 0)
        {
            DocxWriter.AddParagraph(document, "【尚未提取技术特征】");
        }
        else
        {
            var index = 1;
            foreach (var feature in features)
            {
                var evidenceText = feature.Evidence.Count == 0
                    ? "【无可回溯证据，待申请人核对】"
                    : string.Join("；", feature.Evidence.Select(e =>
                        $"{e.RelativePath}{(e.StartLine is null ? string.Empty : $" 第 {e.StartLine} 行")}（SHA-256 {e.Sha256[..Math.Min(16, e.Sha256.Length)]}）"));

                DocxWriter.AddParagraph(document, $"{index}）{feature.Description}");
                DocxWriter.AddParagraph(document, $"    依据：{evidenceText}", DocxWriter.CodeFont, 18);
                index++;
            }
        }

        DocxWriter.AddHeading(document, "实施例", 2);
        var embodiments = Deserialize<Embodiment>(disclosure.EmbodimentsJson);
        if (embodiments.Count == 0)
        {
            DocxWriter.AddParagraph(document, "【尚未生成实施例】");
        }
        else
        {
            foreach (var embodiment in embodiments)
            {
                DocxWriter.AddHeading(document, embodiment.Title, 3);
                DocxWriter.AddParagraph(document, embodiment.Description);
            }
        }

        DocxWriter.AddHeading(document, "生成信息", 2);
        DocxWriter.AddParagraph(document, $"生成时间：{request.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        DocxWriter.AddParagraph(document, $"材料规则版本：{request.RulesVersion}");
        DocxWriter.AddParagraph(document, request.RulesSource);

        var bytes = document.ToBytes();
        return new GeneratedDocument(
            "技术交底书.docx",
            "正式资料/技术交底书.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            bytes);
    }

    private static void AddRow(DocxDocument document, string label, string? value)
    {
        DocxWriter.AddParagraph(document, $"{label}：{OrPlaceholder(value, "【待补充】")}");
    }

    private static string OrPlaceholder(string? value, string placeholder) =>
        string.IsNullOrWhiteSpace(value) ? placeholder : value;

    private static List<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
