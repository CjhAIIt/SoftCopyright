using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.ProjectScan.Zip;

/// <summary>安全解包服务：只做只读解析与落盘，绝不执行仓库中的任何内容。</summary>
public sealed class ZipExtractionService(
    IOptions<ScanOptions> options,
    ILogger<ZipExtractionService> logger) : ISafeArchiveExtractor
{
    public async Task<ArchiveExtractionResult> ExtractAsync(string archivePath, string destinationRoot, CancellationToken ct)
    {
        var opts = options.Value;

        if (!File.Exists(archivePath))
        {
            throw new ArchiveRejectedException("上传的压缩包不存在或已被清理。");
        }

        var archiveInfo = new FileInfo(archivePath);
        if (archiveInfo.Length > opts.MaxArchiveBytes)
        {
            throw new ArchiveRejectedException(
                $"压缩包大小超过限制（当前 {archiveInfo.Length / 1024 / 1024} MB，上限 {opts.MaxArchiveBytes / 1024 / 1024} MB）。");
        }

        Directory.CreateDirectory(destinationRoot);
        var rootFull = Path.GetFullPath(destinationRoot);
        var rootPrefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var skipped = new List<string>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int entryCount = 0;
        long totalBytes = 0;

        await using var fs = File.OpenRead(archivePath);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(fs, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new ArchiveRejectedException("上传的文件不是有效的 ZIP 压缩包。");
        }

        using (archive)
        {

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            entryCount++;

            if (entryCount > opts.MaxEntryCount)
            {
                throw new ArchiveRejectedException($"压缩包条目数超过上限（最多 {opts.MaxEntryCount} 个）。");
            }

            var rawName = entry.FullName;
            if (string.IsNullOrEmpty(rawName))
            {
                continue;
            }

            if (IsSymbolicLink(entry))
            {
                throw new ArchiveRejectedException($"压缩包包含符号链接条目，已拒绝：{rawName}");
            }

            var isDirectory = rawName.EndsWith('/') || rawName.EndsWith('\\');
            var relative = ArchivePathValidator.Normalize(rawName);

            if (relative.Length == 0)
            {
                continue;
            }

            if (!seen.Add(relative))
            {
                throw new ArchiveRejectedException($"压缩包包含重复路径条目，已拒绝（避免覆盖已有文件）：{relative}");
            }

            if (relative.Split('/').Length > opts.MaxPathDepth)
            {
                throw new ArchiveRejectedException($"压缩包包含超过 {opts.MaxPathDepth} 层的目录，已拒绝：{relative}");
            }

            if (isDirectory)
            {
                Directory.CreateDirectory(CombineWithinRoot(rootPrefix, relative));
                continue;
            }

            if (entry.Length > opts.MaxFileBytes)
            {
                skipped.Add($"{relative}（超过单文件大小上限）");
                warnings.Add($"文件 {relative} 超过单文件大小上限，已跳过。");
                continue;
            }

            if (entry.CompressedLength > 0 && entry.Length > 0)
            {
                var ratio = (double)entry.Length / entry.CompressedLength;
                if (entry.Length > opts.CompressionRatioMinBytes && ratio > opts.MaxCompressionRatio)
                {
                    throw new ArchiveRejectedException($"压缩包存在异常压缩比条目（疑似压缩炸弹），已拒绝：{relative}");
                }
            }

            totalBytes += entry.Length;
            if (totalBytes > opts.MaxTotalExtractedBytes)
            {
                throw new ArchiveRejectedException("压缩包解压后的总大小超过上限，已拒绝。");
            }

            var target = CombineWithinRoot(rootPrefix, relative);
            var targetDirectory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            await using (var source = entry.Open())
            await using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await source.CopyToAsync(destination, ct);
            }
        }

        logger.LogInformation("安全解包完成：条目 {Entries}，解压后 {Bytes} 字节", entryCount, totalBytes);

        return new ArchiveExtractionResult(entryCount, totalBytes, skipped, warnings);
        }
    }

    /// <summary>组合目标路径并确认其仍位于根目录内（防路径穿越的最后一道防线）。</summary>
    private static string CombineWithinRoot(string rootPrefix, string relative)
    {
        var combined = Path.GetFullPath(Path.Combine(rootPrefix, relative));
        if (!combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArchiveRejectedException($"压缩包路径越界，已拒绝：{relative}");
        }

        return combined;
    }

    /// <summary>识别 ZIP 中的符号链接与特殊文件条目。</summary>
    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        // Unix 文件类型存放在 ExternalAttributes 的高位（16 位），S_IFLNK = 0xA000。
        var unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
        if (unixMode == 0)
        {
            return false;
        }

        var fileType = unixMode & 0xF000;
        return fileType is 0xA000 or 0x2000 or 0x6000 or 0x1000;
    }
}
