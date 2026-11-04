using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.Application.Confirmation;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Generation;
using SoftCopyright.Application.MaterialFlows;
using SoftCopyright.Application.Services;
using SoftCopyright.Application.Validation;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application;

/// <summary>应用层注册。</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICaseWorkflow, CaseWorkflow>();

        // 软著与专利两条流程各自的描述符、校验规则集、生成策略与确认门禁。
        // 注册表按接口枚举收集实现，因此每个实现都要同时注册为接口。
        services.AddSingleton<SoftCopyrightMaterialFlow>();
        services.AddSingleton<PatentMaterialFlow>();
        services.AddSingleton<MaterialFlowRegistry>();

        services.AddScoped<SoftCopyrightValidationRuleSet>();
        services.AddScoped<PatentValidationRuleSet>();
        services.AddScoped<IMaterialValidationRuleSet>(sp => sp.GetRequiredService<SoftCopyrightValidationRuleSet>());
        services.AddScoped<IMaterialValidationRuleSet>(sp => sp.GetRequiredService<PatentValidationRuleSet>());
        services.AddScoped<MaterialValidationRuleSets>();

        services.AddScoped<SoftCopyrightGenerationStrategy>();
        services.AddScoped<PatentGenerationStrategy>();
        services.AddScoped<IMaterialGenerationStrategy>(sp => sp.GetRequiredService<SoftCopyrightGenerationStrategy>());
        services.AddScoped<IMaterialGenerationStrategy>(sp => sp.GetRequiredService<PatentGenerationStrategy>());
        services.AddScoped<MaterialGenerationStrategies>();

        services.AddScoped<SoftCopyrightConfirmationGate>();
        services.AddScoped<PatentConfirmationGate>();
        services.AddScoped<IConfirmationGate>(sp => sp.GetRequiredService<SoftCopyrightConfirmationGate>());
        services.AddScoped<IConfirmationGate>(sp => sp.GetRequiredService<PatentConfirmationGate>());
        services.AddScoped<ConfirmationGateRegistry>();

        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<ICaseUploadService, UploadService>();
        services.AddScoped<IGitImportService, GitImportService>();
        services.AddScoped<IApplicantInfoService, ApplicantInfoService>();
        services.AddScoped<ICodeSelectionService, CodeSelectionService>();
        services.AddScoped<IConfirmationService, ConfirmationService>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICurrentAccountContext, CurrentAccountContext>();
        services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();

        services.AddScoped<ICaseScanWorkflow, ScanWorkflow>();
        services.AddScoped<ICaseAnalyzeWorkflow, AnalyzeWorkflow>();
        services.AddScoped<ICaseGenerationWorkflow, GenerationWorkflow>();
        services.AddScoped<ICasePatentDraftWorkflow, PatentDraftWorkflow>();
        services.AddScoped<IPatentService, PatentService>();

        return services;
    }
}
