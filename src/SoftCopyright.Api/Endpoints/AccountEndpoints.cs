using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Api.Endpoints;

/// <summary>注册、登录与账号设置 API。</summary>
public static class AccountEndpoints
{
    /// <summary>认证 Cookie 的名称。</summary>
    public const string CookieName = "SoftCopyright.Auth";

    public static void MapAccountApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").RequireAuthorization();

        group.MapGet("/me", async (IAccountService accounts, ICurrentAccountContext current, CancellationToken ct) =>
        {
            var userId = current.UserId;
            if (userId is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "未登录。" } });
            }

            var profile = await accounts.GetAsync(userId.Value, ct);
            return profile is null
                ? Results.NotFound(new { error = new { code = "not_found", message = "账号不存在。" } })
                : Results.Ok(ToDto(profile));
        });

        // 模型接入设置：引导流程与账号设置页共用同一组端点。
        group.MapPost("/ai", async (
            [FromForm] string? baseUrl,
            [FromForm] string? model,
            [FromForm] string? apiKey,
            [FromForm] string? returnTo,
            IAccountService accounts,
            ICurrentAccountContext current,
            CancellationToken ct) =>
        {
            var userId = current.UserId;
            if (userId is null)
            {
                return Results.Redirect("/login?error=" + Uri.EscapeDataString("登录状态已失效，请重新登录。"));
            }

            // 三项缺一不可：只填密钥无法调用模型，界面也不能显示为"已接入"。
            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(baseUrl) ||
                string.IsNullOrWhiteSpace(model))
            {
                return Results.Redirect(SafeReturn(returnTo, "/account") +
                    "?error=" + Uri.EscapeDataString("接口地址、模型名称与 API 密钥都要填写才能启用模型。"));
            }

            await accounts.SaveAiSettingsAsync(userId.Value, baseUrl, model, apiKey, ct);
            return Results.Redirect(SafeReturn(returnTo, "/"));
        });

        // 改为完全离线的免费功能（不保存任何模型配置）。
        group.MapPost("/ai/offline", async (
            [FromForm] string? returnTo,
            IAccountService accounts,
            ICurrentAccountContext current,
            CancellationToken ct) =>
        {
            var userId = current.UserId;
            if (userId is null)
            {
                return Results.Redirect("/login?error=" + Uri.EscapeDataString("登录状态已失效，请重新登录。"));
            }

            await accounts.DisableAiAssistantAsync(userId.Value, ct);
            return Results.Redirect(SafeReturn(returnTo, "/"));
        });
    }

    /// <summary>只允许站内相对路径，避免开放重定向。</summary>
    private static string SafeReturn(string? returnTo, string fallback)
    {
        if (string.IsNullOrWhiteSpace(returnTo))
        {
            return fallback;
        }

        var value = returnTo.Trim();
        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
        {
            return fallback;
        }

        return value;
    }

    /// <summary>注册端点：注册成功后直接签发登录 Cookie。</summary>
    public static void MapAuthApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            [FromForm] string? userName,
            [FromForm] string? displayName,
            [FromForm] string? password,
            IAccountService accounts,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await accounts.RegisterAsync(userName ?? string.Empty, displayName ?? string.Empty, password ?? string.Empty, ct);
            if (!result.Success || result.Profile is null)
            {
                return Results.Redirect("/register?error=" + Uri.EscapeDataString(result.Error ?? "注册失败。"));
            }

            await SignInAsync(context, result.Profile);
            return Results.Redirect(result.Profile.OnboardingCompleted ? "/" : "/onboarding");
        });

        group.MapPost("/login", async (
            [FromForm] string? userName,
            [FromForm] string? password,
            IAccountService accounts,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await accounts.LoginAsync(userName ?? string.Empty, password ?? string.Empty, ct);
            if (!result.Success || result.Profile is null)
            {
                return Results.Redirect("/login?error=" + Uri.EscapeDataString(result.Error ?? "登录失败。"));
            }

            await accounts.TouchLoginAsync(result.Profile.Id, ct);
            await SignInAsync(context, result.Profile);
            return Results.Redirect(result.Profile.OnboardingCompleted ? "/" : "/onboarding");
        });

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });
    }

    private static async Task SignInAsync(HttpContext context, AccountProfile profile)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new(ClaimTypes.Name, profile.UserName)
        };

        if (profile.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AdminEndpoints.AdminRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static AccountDto ToDto(AccountProfile profile) => new(
        profile.Id,
        profile.UserName,
        profile.DisplayName,
        profile.OnboardingCompleted,
        profile.AiAssistantEnabled,
        profile.HasAiConfiguration,
        profile.IsAdmin,
        profile.CreatedAt,
        profile.LastLoginAt);
}
