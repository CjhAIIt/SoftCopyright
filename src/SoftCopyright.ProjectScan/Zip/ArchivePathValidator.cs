using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Zip;

/// <summary>压缩包条目路径校验与规范化。</summary>
public static class ArchivePathValidator
{
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// 把归档条目名规范化为安全的相对路径（统一使用 "/" 分隔）。
    /// 任何越界或可疑路径都直接拒绝，而不是静默跳过。
    /// </summary>
    public static string Normalize(string rawName)
    {
        if (rawName.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArchiveRejectedException("压缩包条目名包含非法字符，已拒绝整个压缩包。");
        }

        var name = rawName.Replace('\\', '/');

        if (name.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArchiveRejectedException($"压缩包包含 UNC 网络路径条目，已拒绝：{rawName}");
        }

        if (name.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArchiveRejectedException($"压缩包包含绝对路径条目，已拒绝：{rawName}");
        }

        if (name.Length >= 2 && char.IsLetter(name[0]) && name[1] == ':')
        {
            throw new ArchiveRejectedException($"压缩包包含磁盘驱动器路径条目，已拒绝：{rawName}");
        }

        var segments = new List<string>();
        foreach (var rawSegment in name.Split('/'))
        {
            if (rawSegment.Length == 0 || rawSegment == ".")
            {
                continue;
            }

            if (rawSegment == "..")
            {
                throw new ArchiveRejectedException($"压缩包包含路径穿越条目（..），已拒绝：{rawName}");
            }

            if (rawSegment.Contains(':', StringComparison.Ordinal))
            {
                throw new ArchiveRejectedException($"压缩包条目包含非法冒号字符，已拒绝：{rawName}");
            }

            // Windows 会忽略结尾的空格与点，这里直接规整，避免出现同名不同形的覆盖。
            var trimmed = rawSegment.TrimEnd(' ', '.');
            if (trimmed.Length == 0)
            {
                continue;
            }

            var deviceName = Path.GetFileNameWithoutExtension(trimmed).ToUpperInvariant();
            if (ReservedDeviceNames.Contains(deviceName))
            {
                throw new ArchiveRejectedException($"压缩包包含 Windows 保留设备名条目，已拒绝：{rawName}");
            }

            segments.Add(trimmed);
        }

        if (segments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('/', segments);
    }
}
