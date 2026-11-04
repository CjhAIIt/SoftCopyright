using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Services;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application;

/// <summary>应用层注册。</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICaseWorkflow, CaseWorkflow>();

        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<ICaseUploadService, UploadService>();
        services.AddScoped<IGitImportService, GitImportService>();
        services.AddScoped<IApplicantInfoService, ApplicantInfoService>();
        services.AddScoped<ICodeSelectionService, CodeSelectionService>();
        services.AddScoped<IConfirmationService, ConfirmationService>();
        services.AddScoped<IValidationService, ValidationService>();

        services.AddScoped<ICaseScanWorkflow, ScanWorkflow>();
        services.AddScoped<ICaseAnalyzeWorkflow, AnalyzeWorkflow>();
        services.AddScoped<ICaseGenerationWorkflow, GenerationWorkflow>();

        return services;
    }
}
