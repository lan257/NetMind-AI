using System.Security.Cryptography;

namespace NetMind.WebApi.Security;

public sealed class ApiKeyEncryptionService : IDisposable
{
    private const string Prefix = "rsa-oaep-sha256";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _keyId;
    private readonly string _publicKey;

    public ApiKeyEncryptionService()
    {
        var publicKeyBytes = _rsa.ExportSubjectPublicKeyInfo();
        _publicKey = Convert.ToBase64String(publicKeyBytes);
        _keyId = Convert.ToHexString(SHA256.HashData(publicKeyBytes))[..16].ToLowerInvariant();
    }

    public ApiKeyPublicKeyDto GetPublicKey()
    {
        return new ApiKeyPublicKeyDto
        {
            KeyId = _keyId,
            Algorithm = Prefix,
            PublicKey = _publicKey
        };
    }

    public string? DecryptIfEncrypted(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value.Split(':', 3);
        if (parts.Length != 3 || !parts[0].Equals(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        if (!parts[1].Equals(_keyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("API Key 加密密钥已过期，请刷新页面后重试。");
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(parts[2]);
            var plainBytes = _rsa.Decrypt(cipherBytes, RSAEncryptionPadding.OaepSHA256);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("API Key 密文解析失败，请重新保存设置后重试。", ex);
        }
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }
}

public sealed class ApiKeyPublicKeyDto
{
    public string KeyId { get; init; } = string.Empty;

    public string Algorithm { get; init; } = string.Empty;

    public string PublicKey { get; init; } = string.Empty;
}
