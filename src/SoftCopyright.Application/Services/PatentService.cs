using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 专利用例服务：技术交底书是唯一真源，摘要 / 权利要求 / 说明书都由它派生。
/// </summary>
public sealed class PatentService(
    IRepository<InventionDisclosure> disclosures,
    IRepository<PatentClaim> claims,
    IRepository<ApplicationCase> cases,
    IRepository<ProjectFile> projectFiles,
    IRepository<Evidence> evidences,
    IEvidencePackBuilder packBuilder,
    IProjectFileReaderFactory readerFactory,
    IPatentDraftGenerator generator,
    IFileStore fileStore,
    IOptions<MaterialRules> rulesOptions,
    ILogger<PatentService> logger) : IPatentService
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    public async Task<InventionDisclosure?> GetDisclosureAsync(Guid caseId, CancellationToken ct)
    {
        var list = await disclosures.ListAsync(d => d.CaseId == caseId, ct);
        return list.FirstOrDefault();
    }

    public async Task<InventionDisclosure> SaveDisclosureAsync(
        Guid caseId,
        PatentDisclosureUpdateRequest request,
        CancellationToken ct)
    {
        var existing = await GetDisclosureAsync(caseId, ct);

        var entity = existing ?? new InventionDisclosure
        {
            Id = Guid.NewGuid(),
            CaseId = caseId
        };

        entity.Title = request.Title.Trim();
        entity.TechnicalField = request.TechnicalField.Trim();
        entity.BackgroundTechnology = request.BackgroundTechnology.Trim();
        entity.ProblemStatement = request.ProblemStatement.Trim();
        entity.TechnicalSolution = request.TechnicalSolution.Trim();
        entity.BeneficialEffects = request.BeneficialEffects.Trim();
        entity.DrawingDescription = request.DrawingDescription.Trim();
        entity.PriorArt = request.PriorArt.Trim();
        entity.PriorArtSources = request.PriorArtSources.Trim();
        entity.ApplicantName = request.ApplicantName.Trim();
        entity.InventorNames = request.InventorNames.Trim();
        entity.PatentType = request.PatentType;
        entity.RulesVersion = _rules.Version;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            await disclosures.AddAsync(entity, ct);
        }

        await disclosures.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<IReadOnlyList<PatentClaim>> GetClaimsAsync(Guid caseId, CancellationToken ct)
    {
        var list = await claims.ListAsync(c => c.CaseId == caseId, ct);
        return list.OrderBy(c => c.Number).ToList();
    }

    public async Task<PatentDraftResult> DraftAsync(Guid caseId, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        var disclosure = await GetDisclosureAsync(caseId, ct)
                         ?? throw new ConflictException("请先填写技术交底书，再生成专利草稿。");

        logger.LogInformation("案例 {CaseId} 开始生成专利草稿", caseId);

        var files = await projectFiles.ListAsync(f => f.CaseId == caseId, ct);
        logger.LogInformation("案例 {CaseId} 已载入 {Count} 个项目文件", caseId, files.Count);

        var caseEvidences = await evidences.ListAsync(e => e.CaseId == caseId, ct);
        logger.LogInformation("案例 {CaseId} 已载入 {Count} 条证据", caseId, caseEvidences.Count);

        var reader = readerFactory.Create(fileStore.GetExtractedRoot(caseId));

        var fileEntries = files.Select(f => new ProjectFileEntry(
            f.RelativePath, f.SizeBytes, f.Sha256, f.IsText, f.DetectedEncoding, f.DetectedLanguage,
            f.Category, f.IsExcluded, f.ExclusionReason, f.LineCount, f.SecretRisk, f.IsCodeCandidate)).ToList();

        var evidenceEntries = caseEvidences.Select(e => new EvidenceEntry(
            e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, e.Excerpt, e.Source)).ToList();

        var pack = await packBuilder.BuildAsync(fileEntries, evidenceEntries, reader, ct);
        logger.LogInformation("案例 {CaseId} 证据包已构建：{Items} 项 / {Bytes} 字节", caseId, pack.Items.Count, pack.TotalBytes);

        var result = await generator.DraftAsync(pack, disclosure, ct);
        logger.LogInformation("案例 {CaseId} 草稿生成器已返回：特征 {Features} 项", caseId, result.Features.Count);

        disclosure.Abstract = result.Abstract;
        disclosure.TechnicalFeaturesJson = JsonSerializer.Serialize(result.Features, Mapping.JsonOptions);
        disclosure.EmbodimentsJson = JsonSerializer.Serialize(result.Embodiments, Mapping.JsonOptions);
        disclosure.UpdatedAt = DateTimeOffset.UtcNow;
        await disclosures.SaveChangesAsync(ct);
        logger.LogInformation("案例 {CaseId} 交底书已更新", caseId);

        // 重新生成权利要求：编号连续，替换旧版本。
        var oldClaims = await claims.ListAsync(c => c.CaseId == caseId, ct);
        foreach (var old in oldClaims)
        {
            claims.Remove(old);
        }

        foreach (var draft in result.Claims)
        {
            await claims.AddAsync(new PatentClaim
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Number = draft.Number,
                Type = draft.Type,
                DependsOn = draft.DependsOn,
                Text = draft.Text,
                FeaturesJson = JsonSerializer.Serialize(draft.Features, Mapping.JsonOptions),
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct);
        }

        await claims.SaveChangesAsync(ct);
        logger.LogInformation("案例 {CaseId} 权利要求已保存，共 {Count} 条", caseId, result.Claims.Count);

        logger.LogInformation(
            "案例 {CaseId} 专利草稿已生成：特征 {Features} 项，权利要求 {Claims} 条，实施例 {Embodiments} 个",
            caseId, result.Features.Count, result.Claims.Count, result.Embodiments.Count);

        return result;
    }

    /// <summary>把交底书映射为 DTO。</summary>
    public static PatentDisclosureDto ToDto(InventionDisclosure entity, IReadOnlyList<PatentClaim> claimList)
    {
        var features = Deserialize<TechnicalFeature>(entity.TechnicalFeaturesJson).Select(ToDto).ToList();
        var embodiments = Deserialize<Embodiment>(entity.EmbodimentsJson).Select(ToDto).ToList();
        var terminology = Deserialize<TerminologyEntry>(entity.TerminologyJson).Select(t => t.Term).ToList();

        return new PatentDisclosureDto(
            entity.Title,
            entity.TechnicalField,
            entity.BackgroundTechnology,
            entity.ProblemStatement,
            entity.TechnicalSolution,
            entity.BeneficialEffects,
            entity.DrawingDescription,
            entity.PriorArt,
            entity.PriorArtSources,
            entity.Abstract,
            entity.ApplicantName,
            entity.InventorNames,
            entity.PatentType,
            features,
            embodiments,
            terminology,
            claimList.Select(c => new PatentClaimDto(
                c.Number,
                c.Type,
                c.DependsOn,
                c.Text,
                Deserialize<TechnicalFeature>(c.FeaturesJson).Select(ToDto).ToList())).ToList());
    }

    private static TechnicalFeatureDto ToDto(TechnicalFeature feature) => new(
        feature.Title,
        feature.Description,
        feature.Confirmed,
        feature.Evidence.Select(e => new EvidenceDto(
            e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, string.Empty)).ToList());

    private static EmbodimentDto ToDto(Embodiment embodiment) => new(
        embodiment.Title,
        embodiment.Description,
        embodiment.Confirmed,
        embodiment.Evidence.Select(e => new EvidenceDto(
            e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, string.Empty)).ToList());

    private static List<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Mapping.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
