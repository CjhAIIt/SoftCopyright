using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoftCopyright.Tests.Support;

/// <summary>
/// 测试用认证处理器：所有请求都以固定测试账号身份通过。
///
/// 应用已启用 Cookie 认证与账号隔离，集成测试需要稳定可复现的登录态，
/// 因此在测试宿主里替换默认认证方案，避免每次请求都走一遍注册登录流程。
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TestAccount testAccount) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, testAccount.UserId.ToString()),
                new Claim(ClaimTypes.Name, testAccount.UserName)
            ],
            testAccount.SchemeName);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, testAccount.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>测试账号。</summary>
/// <param name="UserId">账号 ID。</param>
/// <param name="UserName">登录名。</param>
/// <param name="SchemeName">认证方案名。</param>
public sealed record TestAccount(Guid UserId, string UserName, string SchemeName);
