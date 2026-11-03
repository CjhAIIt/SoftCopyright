using System.IO.Compression;
using System.Text;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Documents;

/// <summary>材料包打包器：按约定目录结构输出 ZIP。</summary>
public sealed class MaterialPackageBuilder : IMaterialPackageBuilder
{
    public async Task<byte[]> BuildAsync(IReadOnlyList<GeneratedDocument> documents, CancellationToken ct)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var document in documents)
            {
                ct.ThrowIfCancellationRequested();

                var entryName = document.RelativePath.Replace('\\', '/');
                if (!entryName.StartsWith("申请案例/", StringComparison.Ordinal))
                {
                    entryName = "申请案例/" + entryName;
                }

                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(document.Content, ct);
            }
        }

        return buffer.ToArray();
    }
}
