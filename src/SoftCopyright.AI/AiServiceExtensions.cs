using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.AI.Patent;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI;

/// <summary>AI 层注册。</summary>
public static class AiServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightAi(this IServiceCollection services)
    {
        services.AddSingleton<IAiProvider, HttpAiProvider>();
        services.AddSingleton<IAiOutputValidator, AiOutputValidator>();
        services.AddSingleton<IEvidencePackBuilder, EvidencePackBuilder>();
        services.AddSingleton<RuleBasedProjectAnalyzer>();
        services.AddSingleton<MockModelAnalyzer>();
        services.AddSingleton<HttpModelAnalyzer>();
        services.AddSingleton<IProjectAnalyzer, ProjectAnalyzerRouter>();
        services.AddSingleton<IMaterialDraftGenerator, MaterialDraftGenerator>();
        services.AddSingleton<RuleBasedPatentDraftGenerator>();
        services.AddSingleton<HttpPatentDraftGenerator>();
        services.AddSingleton<IPatentDraftGenerator, PatentDraftGeneratorRouter>();

        return services;
    }
}
