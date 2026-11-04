using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.AI.Patent;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI;

/// <summary>AI 层注册。</summary>
public static class AiServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightAi(this IServiceCollection services)
    {
        // Scoped：模型接入设置按当前账号解析，需要访问账号仓储。
        services.AddScoped<IAiProvider, HttpAiProvider>();
        services.AddScoped<IAiOutputValidator, AiOutputValidator>();
        services.AddScoped<IEvidencePackBuilder, EvidencePackBuilder>();
        services.AddScoped<RuleBasedProjectAnalyzer>();
        services.AddScoped<MockModelAnalyzer>();
        services.AddScoped<HttpModelAnalyzer>();
        services.AddScoped<IProjectAnalyzer, ProjectAnalyzerRouter>();
        services.AddScoped<IMaterialDraftGenerator, MaterialDraftGenerator>();
        services.AddScoped<RuleBasedPatentDraftGenerator>();
        services.AddScoped<HttpPatentDraftGenerator>();
        services.AddScoped<IPatentDraftGenerator, PatentDraftGeneratorRouter>();

        return services;
    }
}
