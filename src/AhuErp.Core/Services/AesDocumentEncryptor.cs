using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 16 / Improvement #17 — реализация <see cref="IDocumentEncryptor"/>:
    /// AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC). 32-байтный ключ из
    /// <see cref="OrganizationSettings.EncryptionKey"/> используется как
    /// MasterKey, из которого HKDF-подобной схемой выводятся отдельные
    /// ключи для AES и HMAC (через SHA-256 от MasterKey + сольных меток).
    /// Формат шифротекста: <c>enc:v1:&lt;base64(iv|cipher|hmac)&gt;</c>.
    /// </summary>
    public sealed class AesDocumentEncryptor : IDocumentEncryptor
    {
        private const string Prefix = "enc:v1:";
        private const int KeyBytes = 32;          // AES-256.
        private const int IvBytes = 16;           // CBC.
        private const int MacBytes = 32;          // HMAC-SHA256.

        private static readonly byte[] AesKeyLabel = Encoding.UTF8.GetBytes("ahuerp/aes/v1");
        private static readonly byte[] HmacKeyLabel = Encoding.UTF8.GetBytes("ahuerp/hmac/v1");

        private readonly IOrganizationSettingsRepository _settings;

        public AesDocumentEncryptor(IOrganizationSettingsRepository settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsEnabled => TryLoadMasterKey(out _);

        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return plaintext;
            if (!TryLoadMasterKey(out var master)) return plaintext;

            var aesKey = Derive(master, AesKeyLabel);
            var hmacKey = Derive(master, HmacKeyLabel);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey;
                aes.GenerateIV();

                byte[] cipher;
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs, new UTF8Encoding(false)))
                    {
                        sw.Write(plaintext);
                    }
                    cipher = ms.ToArray();
                }

                byte[] mac;
                using (var hmac = new HMACSHA256(hmacKey))
                {
                    var macInput = new byte[aes.IV.Length + cipher.Length];
                    Buffer.BlockCopy(aes.IV, 0, macInput, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, macInput, aes.IV.Length, cipher.Length);
                    mac = hmac.ComputeHash(macInput);
                }

                var payload = new byte[aes.IV.Length + cipher.Length + mac.Length];
                Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, payload, aes.IV.Length, cipher.Length);
                Buffer.BlockCopy(mac, 0, payload, aes.IV.Length + cipher.Length, mac.Length);

                return Prefix + Convert.ToBase64String(payload);
            }
        }

        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
            if (!IsEncryptedPayload(ciphertext)) return ciphertext;

            if (!TryLoadMasterKey(out var master))
            {
                throw new CryptographicException(
                    "Шифр-ключ организации не задан, расшифровка невозможна.");
            }

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(ciphertext.Substring(Prefix.Length));
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("Повреждённый base64 в зашифрованном поле.", ex);
            }

            if (payload.Length < IvBytes + 1 + MacBytes)
            {
                throw new CryptographicException("Слишком короткий зашифрованный блок.");
            }

            var iv = new byte[IvBytes];
            Buffer.BlockCopy(payload, 0, iv, 0, IvBytes);

            var cipherLen = payload.Length - IvBytes - MacBytes;
            var cipher = new byte[cipherLen];
            Buffer.BlockCopy(payload, IvBytes, cipher, 0, cipherLen);

            var mac = new byte[MacBytes];
            Buffer.BlockCopy(payload, IvBytes + cipherLen, mac, 0, MacBytes);

            var aesKey = Derive(master, AesKeyLabel);
            var hmacKey = Derive(master, HmacKeyLabel);

            byte[] expected;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                var macInput = new byte[iv.Length + cipher.Length];
                Buffer.BlockCopy(iv, 0, macInput, 0, iv.Length);
                Buffer.BlockCopy(cipher, 0, macInput, iv.Length, cipher.Length);
                expected = hmac.ComputeHash(macInput);
            }

            if (!ConstantTimeEquals(mac, expected))
            {
                throw new CryptographicException(
                    "MAC не совпал — целостность зашифрованного поля нарушена.");
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, new UTF8Encoding(false)))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public bool IsEncryptedPayload(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public string RotateKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var key = new byte[KeyBytes];
                rng.GetBytes(key);
                var b64 = Convert.ToBase64String(key);

                var s = _settings.Get();
                s.EncryptionKey = b64;
                s.EncryptionKeyGeneratedAt = DateTime.UtcNow;
                _settings.Save(s);

                return b64;
            }
        }

        private bool TryLoadMasterKey(out byte[] key)
        {
            key = null;
            var s = _settings.Get();
            if (string.IsNullOrEmpty(s?.EncryptionKey)) return false;
            try
            {
                var bytes = Convert.FromBase64String(s.EncryptionKey);
                if (bytes.Length != KeyBytes) return false;
                key = bytes;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] Derive(byte[] master, byte[] label)
        {
            using (var sha = SHA256.Create())
            {
                var input = new byte[master.Length + label.Length];
                Buffer.BlockCopy(master, 0, input, 0, master.Length);
                Buffer.BlockCopy(label, 0, input, master.Length, label.Length);
                return sha.ComputeHash(input);
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
