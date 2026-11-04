using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.ProjectScan.Adapters;
using SoftCopyright.ProjectScan.Files;
using SoftCopyright.ProjectScan.Git;
using SoftCopyright.ProjectScan.Scanners;
using SoftCopyright.ProjectScan.Zip;

namespace SoftCopyright.ProjectScan;

/// <summary>扫描层注册。</summary>
public static class ProjectScanServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightProjectScan(this IServiceCollection services)
    {
        services.AddSingleton<ISafeArchiveExtractor, ZipExtractionService>();
        services.AddSingleton<IProjectFileReaderFactory, LocalProjectFileReaderFactory>();
        services.AddSingleton<IGitRepositoryImporter, GitRepositoryImporter>();

        services.AddSingleton<IProjectAdapter, DotNetAdapter>();
        services.AddSingleton<IProjectAdapter, NodeAdapter>();
        services.AddSingleton<IProjectAdapter, PythonAdapter>();
        services.AddSingleton<IProjectAdapter, JavaAdapter>();
        services.AddSingleton<IProjectAdapter, MiniProgramAdapter>();
        services.AddSingleton<IProjectAdapter, GenericAdapter>();

        services.AddSingleton<IProjectScanService, ProjectScanService>();

        return services;
    }
}
