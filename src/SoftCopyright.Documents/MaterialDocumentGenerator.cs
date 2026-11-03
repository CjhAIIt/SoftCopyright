using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.Documents.Docx;

namespace SoftCopyright.Documents;

/// <summary>材料文档生成器：所有代码内容都来自用户确认的真实源码。</summary>
public sealed class MaterialDocumentGenerator(
    IOptions<MaterialRules> rulesOptions,
    ILogger<MaterialDocumentGenerator> logger) : IMaterialDocumentGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(MaterialGenerationRequest request, CancellationToken ct)
    {
        var rules = rulesOptions.Value;
        var documents = new List<GeneratedDocument>();

        documents.Add(BuildApplicationFormInfo(request, rules));
        documents.Add(BuildOperationManual(request, rules));
        documents.Add(BuildCodeMaterial(request, rules));
        documents.Add(BuildJson("草稿/项目画像.json", request));
        documents.Add(BuildJson("草稿/证据清单.json", request));
        documents.Add(BuildApplicantDraft(request));
        documents.Add(BuildValidationReport(request));

        var manifest = BuildManifest(documents, request, rules);
        documents.Add(manifest);

        logger.LogInformation("已生成 {Count} 份材料文档", documents.Count);

        return Task.FromResult<IReadOnlyList<GeneratedDocument>>(documents);
    }

    private static GeneratedDocument BuildApplicationFormInfo(MaterialGenerationRequest request, MaterialRules rules)
    {
        var builder = new StringBuilder();
        builder.AppendLine("软件著作权登记申请信息（草稿，需申请人按官方最新要求核对）");
        builder.AppendLine(new string('=', 56));
        builder.AppendLine($"软件全称：{request.SoftwareFullName}");
        builder.AppendLine($"版本号：{request.SoftwareVersion}");
        builder.AppendLine($"著作权人：{request.CopyrightOwner}");
        builder.AppendLine($"开发完成日期：{request.CompletionDate}");
        builder.AppendLine($"首次发表状态：{request.FirstPublicationStatus}");
        builder.AppendLine($"首次发表日期：{request.FirstPublicationDate}");
        builder.AppendLine($"开发方式：{request.DevelopmentMode}");
        builder.AppendLine($"权利取得方式：{request.RightsAcquisitionMethod}");
        builder.AppendLine($"开发环境：{request.DevelopmentEnvironment}");
        builder.AppendLine($"运行环境：{request.RuntimeEnvironment}");
        builder.AppendLine($"编程语言：{request.ProgrammingLanguages}");
        builder.AppendLine($"源程序量：{request.SourceProgramAmount}");
        builder.AppendLine($"软件用途：{request.Purpose}");
        builder.AppendLine();
        builder.AppendLine("功能简介：");
        builder.AppendLine(request.FunctionSummary);
        builder.AppendLine();
        builder.AppendLine("检测到的技术栈：" + (request.Technologies.Count == 0 ? "未识别" : string.Join("、", request.Technologies)));
        builder.AppendLine("识别到的入口点：" + (request.Entrypoints.Count == 0 ? "未识别" : string.Join("、", request.Entrypoints)));
        builder.AppendLine();
        builder.AppendLine($"规则版本：{request.RulesVersion}");
        builder.AppendLine($"生成时间：{request.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"说明：{request.RulesSource}");

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
        return new GeneratedDocument("申请表信息.txt", "正式资料/申请表信息.txt", "text/plain; charset=utf-8", bytes);
    }

    private static GeneratedDocument BuildOperationManual(MaterialGenerationRequest request, MaterialRules rules)
    {
        using var document = DocxWriter.Create();
        {
            DocxWriter.AddCover(document, request.SoftwareFullName, $"版本 {request.SoftwareVersion}", new[]
            {
                "软件著作权登记申请材料 · 操作手册",
                $"生成时间：{request.GeneratedAt:yyyy-MM-dd HH:mm:ss}",
                $"规则版本：{request.RulesVersion}"
            });

            DocxWriter.AddHeading(document, "一、软件概述", 1, pageBreakBefore: true);
            DocxWriter.AddParagraph(document, string.IsNullOrWhiteSpace(request.Purpose)
                ? "【待申请人补充：软件用途】"
                : request.Purpose);

            DocxWriter.AddHeading(document, "二、功能说明", 1);
            if (request.Features.Count == 0)
            {
                DocxWriter.AddParagraph(document, "【待申请人补充：功能清单】");
            }
            else
            {
                var index = 1;
                foreach (var feature in request.Features)
                {
                    DocxWriter.AddHeading(document, $"2.{index} {feature.Value}", 2);
                    var source = feature.Status switch
                    {
                        FactStatus.Observed => "来源：仓库证据",
                        FactStatus.ApplicantConfirmed => "来源：申请人确认",
                        FactStatus.Inferred => "来源：推断，需申请人确认",
                        _ => "来源：未知，需申请人补充"
                    };

                    DocxWriter.AddParagraph(document, source);
                    foreach (var evidence in feature.Evidence)
                    {
                        DocxWriter.AddParagraph(document, $"证据：{evidence.RelativePath}" +
                            (evidence.StartLine is null ? string.Empty : $"（第 {evidence.StartLine} 行起）") +
                            $"，SHA-256 {evidence.Sha256[..Math.Min(16, evidence.Sha256.Length)]}");
                    }

                    if (feature.Status is FactStatus.Inferred or FactStatus.Unknown)
                    {
                        DocxWriter.AddParagraph(document, "【待申请人补充或确认本节内容】");
                    }

                    index++;
                }
            }

            DocxWriter.AddHeading(document, "三、运行环境", 1);
            DocxWriter.AddParagraph(document, $"开发环境：{request.DevelopmentEnvironment}");
            DocxWriter.AddParagraph(document, $"运行环境：{request.RuntimeEnvironment}");
            DocxWriter.AddParagraph(document, $"编程语言：{request.ProgrammingLanguages}");
            DocxWriter.AddParagraph(document, "检测到的技术栈：" + (request.Technologies.Count == 0 ? "未识别" : string.Join("、", request.Technologies)));

            DocxWriter.AddHeading(document, "四、安装与启动", 1);
            if (request.Entrypoints.Count == 0)
            {
                DocxWriter.AddParagraph(document, "【待申请人补充：安装与启动步骤】");
            }
            else
            {
                DocxWriter.AddParagraph(document, "本软件的程序入口点如下（取自仓库真实文件）：");
                foreach (var entry in request.Entrypoints)
                {
                    DocxWriter.AddParagraph(document, "· " + entry);
                }

                DocxWriter.AddParagraph(document, "【待申请人补充：具体安装步骤、环境变量与启动命令】");
            }

            DocxWriter.AddHeading(document, "五、注意事项", 1);
            DocxWriter.AddParagraph(document, "本文档由软著智能申请工具依据仓库证据与申请人确认信息自动生成，"
                + "内容仅供申请材料准备参考，须由申请人核对后再提交官方系统。");
            DocxWriter.AddParagraph(document, request.RulesSource);

            var bytes = document.ToBytes();
            return new GeneratedDocument("操作手册.docx", "正式资料/操作手册.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", bytes);
        }
    }

    private static GeneratedDocument BuildCodeMaterial(MaterialGenerationRequest request, MaterialRules rules)
    {
        var linesPerPage = Math.Max(10, rules.CodeMaterial.LinesPerPage);
        var pages = BuildCodePages(request, linesPerPage, rules.CodeMaterial.IncludeFileBoundaryMarkers);
        var totalPages = pages.Count;

        using var document = DocxWriter.Create();
        {
            DocxWriter.AddCover(document, request.SoftwareFullName, $"版本 {request.SoftwareVersion} · 程序鉴别材料", new[]
            {
                $"代码文件数：{request.CodeBlocks.Count}",
                $"代码总行数：{request.CodeBlocks.Sum(b => b.Lines.Count)}",
                $"材料页数：{totalPages}（每页 {linesPerPage} 行）",
                $"生成时间：{request.GeneratedAt:yyyy-MM-dd HH:mm:ss}",
                $"规则版本：{request.RulesVersion}"
            });

            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var page = pages[pageIndex];
                DocxWriter.AddParagraph(
                    document,
                    $"第 {pageIndex + 1} 页 / 共 {totalPages} 页",
                    DocxWriter.BodyFont,
                    20,
                    justification: JustificationValues.Center,
                    pageBreakBefore: pageIndex > 0);

                if (page.Marker is not null)
                {
                    DocxWriter.AddParagraph(document, page.Marker, DocxWriter.CodeFont, 18);
                }

                foreach (var line in page.Lines)
                {
                    DocxWriter.AddParagraph(document, line, DocxWriter.CodeFont, 18);
                }
            }

            DocxWriter.AddHeading(document, "材料说明", 1, pageBreakBefore: true);
            DocxWriter.AddParagraph(document, "本材料中的全部代码均逐行摘自申请人确认的仓库源文件，未做任何改写或补写。");
            DocxWriter.AddParagraph(document, request.RulesSource);

            var bytes = document.ToBytes();
            return new GeneratedDocument("代码鉴别材料.docx", "正式资料/代码鉴别材料.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", bytes);
        }
    }

    public static List<CodePage> BuildCodePages(MaterialGenerationRequest request, int linesPerPage, bool includeMarkers)
    {
        var pages = new List<CodePage>();
        var current = new List<string>();
        string? marker = null;

        foreach (var block in request.CodeBlocks)
        {
            if (includeMarkers)
            {
                var end = block.StartLine + Math.Max(0, block.Lines.Count - 1);
                var sha = block.Sha256.Length > 16 ? block.Sha256[..16] : block.Sha256;
                marker = $"// 文件：{block.RelativePath}（起止行 {block.StartLine}-{end}，SHA-256 {sha}）";
            }

            foreach (var line in block.Lines)
            {
                if (current.Count >= linesPerPage)
                {
                    pages.Add(new CodePage(marker, current));
                    current = [];
                    marker = null;
                }

                current.Add(line);
            }
        }

        if (current.Count > 0)
        {
            pages.Add(new CodePage(marker, current));
        }

        return pages;
    }

    private static GeneratedDocument BuildJson(string relativePath, MaterialGenerationRequest request)
    {
        var payload = new
        {
            软件全称 = request.SoftwareFullName,
            版本号 = request.SoftwareVersion,
            用途 = request.Purpose,
            功能 = request.Features.Select(f => new
            {
                描述 = f.Value,
                状态 = f.Status.ToString(),
                说明 = f.Explanation,
                证据 = f.Evidence
            }),
            技术栈 = request.Technologies,
            入口点 = request.Entrypoints,
            证据清单 = request.Evidences.Select(e => new
            {
                文件 = e.RelativePath,
                起始行 = e.StartLine,
                结束行 = e.EndLine,
                类型 = e.EvidenceType,
                哈希 = e.Sha256,
                摘要 = e.Excerpt,
                来源 = e.Source.ToString()
            }),
            规则版本 = request.RulesVersion,
            生成时间 = request.GeneratedAt,
            说明 = request.RulesSource
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var name = Path.GetFileName(relativePath);
        return new GeneratedDocument(name, relativePath, "application/json; charset=utf-8", bytes);
    }

    private static GeneratedDocument BuildApplicantDraft(MaterialGenerationRequest request)
    {
        var payload = new
        {
            软件全称 = request.SoftwareFullName,
            版本号 = request.SoftwareVersion,
            著作权人 = request.CopyrightOwner,
            开发完成日期 = request.CompletionDate,
            首次发表状态 = request.FirstPublicationStatus,
            首次发表日期 = request.FirstPublicationDate,
            开发方式 = request.DevelopmentMode,
            权利取得方式 = request.RightsAcquisitionMethod,
            开发环境 = request.DevelopmentEnvironment,
            运行环境 = request.RuntimeEnvironment,
            编程语言 = request.ProgrammingLanguages,
            源程序量 = request.SourceProgramAmount,
            功能简介 = request.FunctionSummary
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        return new GeneratedDocument("申请信息草稿.json", "草稿/申请信息草稿.json", "application/json; charset=utf-8", bytes);
    }

    private static GeneratedDocument BuildValidationReport(MaterialGenerationRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
        builder.AppendLine("<title>材料校验报告</title><style>");
        builder.AppendLine("body{font-family:'Microsoft YaHei',sans-serif;margin:32px;color:#222;line-height:1.7}");
        builder.AppendLine("h1{font-size:22px;border-bottom:2px solid #333;padding-bottom:8px}");
        builder.AppendLine("table{border-collapse:collapse;width:100%;margin-top:16px}");
        builder.AppendLine("th,td{border:1px solid #ddd;padding:8px 10px;text-align:left;font-size:14px;vertical-align:top}");
        builder.AppendLine(".pass{background:#e8f5e9}.warn{background:#fff8e1}.action{background:#ffebee}");
        builder.AppendLine(".note{margin-top:24px;padding:12px;background:#f5f5f5;font-size:13px;color:#555}");
        builder.AppendLine("</style></head><body>");
        builder.AppendLine("<h1>软著申请材料校验报告</h1>");
        builder.AppendLine($"<p>软件：{Escape(request.SoftwareFullName)}（版本 {Escape(request.SoftwareVersion)}）<br>");
        builder.AppendLine($"生成时间：{request.GeneratedAt:yyyy-MM-dd HH:mm:ss} ｜ 规则版本：{Escape(request.RulesVersion)}</p>");

        builder.AppendLine("<table><thead><tr><th>规则</th><th>级别</th><th>检查项</th><th>说明</th><th>处理建议</th></tr></thead><tbody>");

        foreach (var result in request.ValidationResults)
        {
            var css = result.Level switch
            {
                ValidationLevel.Pass => "pass",
                ValidationLevel.Warning => "warn",
                _ => "action"
            };

            var level = result.Level switch
            {
                ValidationLevel.Pass => "通过",
                ValidationLevel.Warning => "警告",
                _ => "需申请人处理"
            };

            builder.AppendLine($"<tr class=\"{css}\"><td>{Escape(result.RuleId)}</td><td>{level}</td>" +
                $"<td>{Escape(result.Title)}</td><td>{Escape(result.Message)}</td>" +
                $"<td>{Escape(result.FixHint ?? "-")}</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine($"<p class=\"note\">本报告仅表示本工具自动化规则的检查结果，不代表登记审查结论，也不构成法律意见。{Escape(request.RulesSource)}</p>");
        builder.AppendLine("</body></html>");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return new GeneratedDocument("校验报告.html", "校验报告.html", "text/html; charset=utf-8", bytes);
    }

    private static GeneratedDocument BuildManifest(
        IReadOnlyList<GeneratedDocument> documents,
        MaterialGenerationRequest request,
        MaterialRules rules)
    {
        var entries = documents.Select(d => new
        {
            文件名 = d.FileName,
            路径 = d.RelativePath,
            大小 = d.Content.Length,
            哈希 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(d.Content)).ToLowerInvariant()
        });

        var payload = new
        {
            软件全称 = request.SoftwareFullName,
            版本号 = request.SoftwareVersion,
            规则版本 = request.RulesVersion,
            生成时间 = request.GeneratedAt,
            产物 = entries,
            说明 = request.RulesSource
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        return new GeneratedDocument("生成清单.json", "生成清单.json", "application/json; charset=utf-8", bytes);
    }

    private static string Escape(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    public sealed record CodePage(string? Marker, IReadOnlyList<string> Lines);
}
