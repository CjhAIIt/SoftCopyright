using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Files;

/// <summary>按解包根目录创建只读文件访问器。</summary>
public sealed class LocalProjectFileReaderFactory : IProjectFileReaderFactory
{
    public IProjectFileReader Create(string extractedRoot) => new LocalProjectFileReader(extractedRoot);
}
