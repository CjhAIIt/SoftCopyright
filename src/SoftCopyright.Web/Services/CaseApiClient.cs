using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SoftCopyright.Application.Api;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Web.Services;

/// <summary>通过真实 HTTP 调用后端 API 的客户端实现。</summary>
public sealed class CaseApiClient(HttpClient http) : ICaseApiClient
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<MaterialFlowDto>> GetFlowsAsync(CancellationToken ct) =>
        await GetAsync<List<MaterialFlowDto>>("/api/flows", ct) ?? [];

    public async Task<MaterialFlowDto> GetFlowAsync(MaterialKind materialKind, CancellationToken ct) =>
        (await GetAsync<MaterialFlowDto>($"/api/flows/{materialKind}", ct))!;

    public async Task<IReadOnlyList<CaseSummaryDto>> GetCasesAsync(CancellationToken ct) =>
        await GetAsync<List<CaseSummaryDto>>("/api/cases", ct) ?? [];

    public async Task<IReadOnlyList<CaseSummaryDto>> GetCasesAsync(MaterialKind materialKind, CancellationToken ct) =>
        await GetAsync<List<CaseSummaryDto>>($"/api/cases/by-kind/{materialKind}", ct) ?? [];

    public async Task<CreateCaseResponse> CreateCaseAsync(string? name, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("/api/cases", new CreateCaseRequest(name), Options, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateCaseResponse>(Options, ct))!;
    }

    public async Task<CreateCaseResponse> CreateCaseAsync(string? name, MaterialKind materialKind, CancellationToken ct)
    {
        // 两条流程各有独立创建端点，材料类型由端点决定。
        var endpoint = materialKind == MaterialKind.Patent
            ? "/api/patent/cases"
            : "/api/softcopyright/cases";

        var response = await http.PostAsJsonAsync(endpoint, new CreateCaseRequest(name), Options, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateCaseResponse>(Options, ct))!;
    }

    public async Task<PatentDisclosureDto> GetPatentDisclosureAsync(Guid id, CancellationToken ct)
    {
        var response = await http.GetAsync($"/api/cases/{id}/patent/disclosure", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PatentDisclosureDto>(Options, ct))!;
    }

    public async Task<PatentDisclosureDto> SavePatentDisclosureAsync(Guid id, PatentDisclosureUpdateRequest request, CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"/api/cases/{id}/patent/disclosure", request, Options, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PatentDisclosureDto>(Options, ct))!;
    }

    public async Task<JobDto> TriggerPatentDraftAsync(Guid id, CancellationToken ct)
    {
        var response = await http.PostAsync($"/api/cases/{id}/patent/draft", null, ct);
        await EnsureSuccessAsync(response, ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var jobId = document.RootElement.GetProperty("jobId").GetGuid();
        return new JobDto(jobId, Domain.JobType.PatentDraft, "专利草稿", Domain.JobStatus.Pending, "等待中", 0, "已排队", null, 0, 3, DateTimeOffset.Now, null);
    }

    public async Task<CaseDetailDto> GetCaseAsync(Guid id, CancellationToken ct) =>
        (await GetAsync<CaseDetailDto>($"/api/cases/{id}", ct))!;

    public async Task<DeleteCaseResponse> DeleteCaseAsync(Guid id, CancellationToken ct)
    {
        var response = await http.DeleteAsync($"/api/cases/{id}", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeleteCaseResponse>(Options, ct))!;
    }

    public async Task<UploadResultDto> UploadAsync(Guid id, string fileName, Stream content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", fileName);

        var response = await http.PostAsync($"/api/cases/{id}/upload", form, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<UploadResultDto>(Options, ct))!;
    }

    public async Task<JobDto> TriggerScanAsync(Guid id, CancellationToken ct)
    {
        var response = await http.PostAsync($"/api/cases/{id}/scan", null, ct);
        await EnsureSuccessAsync(response, ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var jobId = document.RootElement.GetProperty("jobId").GetGuid();
        return new JobDto(jobId, Domain.JobType.Scan, "仓库扫描", Domain.JobStatus.Pending, "等待中", 0, "已排队", null, 0, 3, DateTimeOffset.Now, null);
    }

    public async Task<GitImportResultDto> ImportFromGitAsync(Guid id, GitImportRequest request, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync($"/api/cases/{id}/import-git", request, Options, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GitImportResultDto>(Options, ct))!;
    }

    public async Task<IReadOnlyList<JobDto>> GetJobsAsync(Guid id, CancellationToken ct) =>
        await GetAsync<List<JobDto>>($"/api/cases/{id}/jobs", ct) ?? [];

    public async Task<AnalysisDto> GetAnalysisAsync(Guid id, CancellationToken ct) =>
        (await GetAsync<AnalysisDto>($"/api/cases/{id}/analysis", ct))!;

    public async Task<ApplicantInfoDto?> GetApplicantInfoAsync(Guid id, CancellationToken ct) =>
        await GetAsync<ApplicantInfoDto>($"/api/cases/{id}/applicant-info", ct);

    public async Task<ApplicantInfoDto> SaveApplicantInfoAsync(Guid id, UpdateApplicantInfoRequest request, CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"/api/cases/{id}/applicant-info", request, Options, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ApplicantInfoDto>(Options, ct))!;
    }

    public async Task<IReadOnlyList<CodeCandidateDto>> GetCodeCandidatesAsync(Guid id, CancellationToken ct) =>
        await GetAsync<List<CodeCandidateDto>>($"/api/cases/{id}/code-candidates", ct) ?? [];

    public async Task<string?> PreviewCodeAsync(Guid id, string path, int maxLines, CancellationToken ct)
    {
        var url = $"/api/cases/{id}/code-candidates/preview?path={Uri.EscapeDataString(path)}&maxLines={maxLines}";
        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<PreviewPayload>(Options, ct);
        return payload?.Content;
    }

    public async Task UpdateCodeSelectionAsync(Guid id, UpdateCodeSelectionRequest request, CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"/api/cases/{id}/code-selection", request, Options, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<ConfirmationDto>> GetConfirmationsAsync(Guid id, CancellationToken ct) =>
        await GetAsync<List<ConfirmationDto>>($"/api/cases/{id}/confirmations", ct) ?? [];

    public async Task<ConfirmationDto> ConfirmAsync(Guid id, string stage, string? note, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync($"/api/cases/{id}/confirm/{stage}", new ConfirmStageRequest(note), Options, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ConfirmationDto>(Options, ct))!;
    }

    public async Task<JobDto> TriggerGenerateAsync(Guid id, CancellationToken ct)
    {
        var response = await http.PostAsync($"/api/cases/{id}/generate", null, ct);
        await EnsureSuccessAsync(response, ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var jobId = document.RootElement.GetProperty("jobId").GetGuid();
        return new JobDto(jobId, Domain.JobType.Generate, "材料生成", Domain.JobStatus.Pending, "等待中", 0, "已排队", null, 0, 3, DateTimeOffset.Now, null);
    }

    public async Task<IReadOnlyList<ArtifactDto>> GetArtifactsAsync(Guid id, CancellationToken ct) =>
        await GetAsync<List<ArtifactDto>>($"/api/cases/{id}/artifacts", ct) ?? [];

    public async Task<IReadOnlyList<ValidationDto>> GetValidationAsync(Guid id, CancellationToken ct) =>
        await GetAsync<List<ValidationDto>>($"/api/cases/{id}/validation", ct) ?? [];

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        var response = await http.GetAsync(url, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, ct);

        // 空响应体（服务端对"尚无数据"返回 200 + 空体）不能直接反序列化，否则抛 JSON 解析异常。
        if (response.Content.Headers.ContentLength is 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(Options, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var message = $"请求失败（{(int)response.StatusCode}）。";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var errorMessage))
            {
                message = errorMessage.GetString() ?? message;
            }
        }
        catch (JsonException)
        {
            // 保留默认提示。
        }

        throw new ApiException((int)response.StatusCode, message);
    }

    private sealed record PreviewPayload(string Path, string Content);
}

/// <summary>API 调用失败时抛出的异常，携带后端返回的中文原因。</summary>
public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
