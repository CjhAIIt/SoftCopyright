using System.Text;

namespace SoftCopyright.ProjectScan.Files;

/// <summary>文本 / 二进制判定与编码识别。</summary>
public static class TextDetection
{
    static TextDetection()
    {
        // 注册代码页提供程序，才能识别 GB18030 等中文编码。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public const string Utf8 = "utf-8";
    public const string Utf8Bom = "utf-8-bom";
    public const string Utf16Le = "utf-16le";
    public const string Utf16Be = "utf-16be";
    public const string Gb18030 = "gb18030";
    public const string Latin1 = "latin1";

    /// <summary>判定字节内容是否为文本，并给出编码名称。</summary>
    public static (bool IsText, string Encoding) Detect(ReadOnlySpan<byte> sample)
    {
        if (sample.Length == 0)
        {
            return (true, Utf8);
        }

        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
        {
            return (true, Utf8Bom);
        }

        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
        {
            return (true, Utf16Le);
        }

        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
        {
            return (true, Utf16Be);
        }

        // UTF-16 无 BOM 时通常会出现大量 NUL 字节。
        if (HasNulByte(sample))
        {
            return (false, "binary");
        }

        if (HasHighRatioOfControlCharacters(sample))
        {
            return (false, "binary");
        }

        try
        {
            var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
            strict.GetString(sample);
            return (true, Utf8);
        }
        catch (DecoderFallbackException)
        {
            // 继续尝试中文常用编码。
        }

        try
        {
            var gb = Encoding.GetEncoding("gb18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            gb.GetString(sample);
            return (true, Gb18030);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException or NotSupportedException)
        {
            // 无法识别为中文编码时按单字节编码处理，保证内容不丢失。
        }

        return (true, Latin1);
    }

    /// <summary>按编码名称解码内容。</summary>
    public static string Decode(ReadOnlySpan<byte> bytes, string encodingName)
    {
        Encoding encoding = encodingName switch
        {
            Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Utf16Le => Encoding.Unicode,
            Utf16Be => Encoding.BigEndianUnicode,
            Gb18030 => Encoding.GetEncoding("gb18030"),
            Latin1 => Encoding.Latin1,
            _ => new UTF8Encoding(false)
        };

        var offset = encodingName == Utf8Bom && bytes.Length >= 3 ? 3 : 0;
        return encoding.GetString(bytes[offset..]);
    }

    private static bool HasNulByte(ReadOnlySpan<byte> data)
    {
        var limit = Math.Min(data.Length, 4096);
        for (var i = 0; i < limit; i++)
        {
            if (data[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHighRatioOfControlCharacters(ReadOnlySpan<byte> data)
    {
        var limit = Math.Min(data.Length, 4096);
        if (limit == 0)
        {
            return false;
        }

        var control = 0;
        for (var i = 0; i < limit; i++)
        {
            var b = data[i];
            if (b < 0x09 || (b > 0x0D && b < 0x20))
            {
                control++;
            }
        }

        return (double)control / limit > 0.30;
    }
}
