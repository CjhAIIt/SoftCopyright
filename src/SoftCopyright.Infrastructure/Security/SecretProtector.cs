using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Infrastructure.Storage;

namespace SoftCopyright.Infrastructure.Security;

/// <summary>
/// 敏感配置保护：AES-256-GCM。
///
/// 机器密钥放在数据目录（默认 %LOCALAPPDATA%\SoftCopyright\secret.key），
/// 首次使用时随机生成。它不在仓库内、不进日志、也不会随数据库一起被拷走而失效。
/// </summary>
public sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;
    private readonly ILogger<SecretProtector> _logger;

    public SecretProtector(IFileStore fileStore, ILogger<SecretProtector> logger)
    {
        _logger = logger;
        _key = LoadOrCreateKey(Path.Combine(fileStore.DataRoot, "secret.key"));
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        // nonce + tag + 密文
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        try
        {
            var raw = Convert.FromBase64String(ciphertext);
            if (raw.Length < NonceSize + TagSize)
            {
                return null;
            }

            var nonce = raw[..NonceSize];
            var tag = raw[NonceSize..(NonceSize + TagSize)];
            var cipher = raw[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];

            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // 密钥文件被换掉或密文损坏时不应让整个流程崩掉。
            _logger.LogWarning("敏感配置解密失败，已按未配置处理。");
            return null;
        }
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            var existing = Convert.FromBase64String(File.ReadAllText(path).Trim());
            if (existing.Length == KeySize)
            {
                return existing;
            }
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllText(path, Convert.ToBase64String(key));
        return key;
    }
}
