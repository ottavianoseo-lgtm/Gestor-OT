using System.Security.Cryptography;
using System.Text;
using GestorOT.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestorOT.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
    {
        _logger = logger;
        var secret = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        
        if (string.IsNullOrEmpty(secret))
        {
            secret = configuration["EncryptionKey"];
        }

        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("EncryptionKey not found in environment or configuration. Using insecure fallback for development.");
            secret = "0123456789ABCDEF0123456789ABCDEF"; // Fallback for dev only
        }
        
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        var iv = aes.IV;

        using var ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length);

        using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;
            var iv = new byte[aes.BlockSize / 8];
            
            if (fullCipher.Length < iv.Length)
            {
                _logger.LogError("Cipher text is too short to contain IV.");
                return "ERROR_DECRYPTING";
            }

            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error decrypting text. Falling back to plain text (this is expected for manual DB entries in Dev).");
            return cipherText; // Devuelve el texto original si no está encriptado
        }
    }
}

