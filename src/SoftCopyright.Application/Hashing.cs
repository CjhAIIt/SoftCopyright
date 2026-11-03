using System.Security.Cryptography;
using System.Text;

namespace SoftCopyright.Application;

/// <summary>规范化哈希工具：用于确认内容哈希与失效判定。</summary>
public static class Hashing
{
    public static string Of(params string?[] parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            builder.Append(part ?? string.Empty);
            builder.Append('\u001F');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    public static string OfBytes(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
