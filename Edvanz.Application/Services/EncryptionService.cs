using Edvanz.Application.IservicesContract;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Edvanz.Application.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(IConfiguration config)
        {
            var key = config["Encryption:Key"];
            var iv = config["Encryption:IV"];

            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "Encryption:Key is not configured. " +
                    "Add it to appsettings.Development.json locally or to Key Vault in production.");

            if (string.IsNullOrWhiteSpace(iv))
                throw new InvalidOperationException(
                    "Encryption:IV is not configured. " +
                    "Add it to appsettings.Development.json locally or to Key Vault in production.");

            _key = Convert.FromBase64String(key);
            _iv = Convert.FromBase64String(iv);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(cipherBytes);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
