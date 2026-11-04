using Microsoft.Extensions.DependencyInjection;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Documents;

/// <summary>文档层注册。</summary>
public static class DocumentsServiceExtensions
{
    public static IServiceCollection AddSoftCopyrightDocuments(this IServiceCollection services)
    {
        services.AddSingleton<IMaterialDocumentGenerator, MaterialDocumentGenerator>();
        services.AddSingleton<IPatentDocumentGenerator, PatentDocumentGenerator>();
        services.AddSingleton<IMaterialPackageBuilder, MaterialPackageBuilder>();

        return services;
    }
}
