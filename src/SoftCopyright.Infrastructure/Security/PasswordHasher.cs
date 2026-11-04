using System.Security.Cryptography;
using System.Text;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Infrastructure.Security;

/// <summary>
/// 密码派生：PBKDF2-HMAC-SHA256。
///
/// 只保存派生值与盐，不保存明文；校验时重新派生并做定长比较。
/// 格式：<c>PBKDF2-SHA256$迭代次数$盐(Base64)$派生值(Base64)</c>。
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int DefaultIterations = 210_000;
    private const string Prefix = "PBKDF2-SHA256";

    /// <summary>生成密码派生值（默认迭代次数）。</summary>
    public string Hash(string password) => Hash(password, DefaultIterations);

    private static string Hash(string password, int iterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>校验密码是否与派生值匹配。</summary>
    public bool Verify(string password, string? stored)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
