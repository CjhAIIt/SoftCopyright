using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Api.Endpoints;

/// <summary>管理端 API：账号列表、删除账号与数据总览。仅管理员可访问。</summary>
public static class AdminEndpoints
{
    /// <summary>管理员角色名（写入登录 Cookie 的角色声明）。</summary>
    public const string AdminRole = "Admin";

    public static void MapAdminApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(policies => policies.RequireRole(AdminRole));

        group.MapGet("/accounts", async (IAccountService accounts, CancellationToken ct) =>
        {
            var views = await accounts.ListAccountsAsync(ct);
            return Results.Ok(views.Select(v => new AdminAccountDto(
                v.Account.Id,
                v.Account.UserName,
                v.Account.DisplayName,
                v.IsAdmin,
                v.Account.OnboardingCompleted,
                v.Account.AiAssistantEnabled,
                v.AiMode,
                v.CaseCount,
                v.Account.CreatedAt,
                v.Account.LastLoginAt)).ToList());
        });

        group.MapDelete("/accounts/{id:guid}", async (
            Guid id,
            IAccountService accounts,
            ICurrentAccountContext current,
            CancellationToken ct) =>
        {
            var requesterId = current.UserId;
            if (requesterId is null)
            {
                return Results.Unauthorized();
            }

            var result = await accounts.DeleteAccountAsync(id, requesterId.Value, ct);
            return result.Success
                ? Results.Ok(new { deleted = true })
                : Results.Conflict(new { error = new { code = "conflict", message = result.Error } });
        });

        group.MapGet("/overview", async (IAccountService accounts, CancellationToken ct) =>
        {
            var overview = await accounts.GetOverviewAsync(ct);
            return Results.Ok(new AdminOverviewDto(
                overview.TotalAccounts,
                overview.AdminAccounts,
                overview.OnlineAccounts,
                overview.TotalCases,
                overview.SoftCopyrightCases,
                overview.PatentCases,
                overview.TotalArtifacts,
                overview.PendingJobs,
                overview.FailedJobs,
                overview.SuspectedSecretFiles));
        });
    }
}
