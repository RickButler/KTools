using System;
using System.Security.Cryptography;
using System.Text;
using LoginProxy.Config;
using LoginProxy.Packets.Models;
using Microsoft.Extensions.Options;

namespace LoginProxy.Packets;

public class DesCredentialCipher
{
    private const int LoginPayloadOffset = 16; // Header offset used in EQ login packets
    private readonly ProxySettings _settings;

    public DesCredentialCipher(IOptions<ProxySettings> settings)
    {
        _settings = settings.Value;
    }

    public LoginInfo? TryDecrypt(byte[] buffer)
    {
        try
        {
            if (buffer.Length <= LoginPayloadOffset)
                return null;

            using var des = DES.Create();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.None;
            des.Key = Convert.FromHexString(_settings.DesKey);
            des.IV = Convert.FromHexString(_settings.DesIV);

            var encrypted = buffer[LoginPayloadOffset..];
            var decryptedBytes = des.CreateDecryptor()
                .TransformFinalBlock(encrypted, 0, encrypted.Length);

            var decoded = Encoding.ASCII.GetString(decryptedBytes);
            var parts = decoded.Split('\0', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return null;

            return new LoginInfo
            {
                Username = parts[0],
                Password = parts[1]
            };
        }
        catch
        {
            return null;
        }
    }

    public bool TryEncrypt(byte[] buffer, LoginInfo login)
    {
        try
        {
            var plaintext = Encoding.ASCII.GetBytes(
                login.Username + "\0" + login.Password + "\0");

            // Pad to nearest 8-byte block (DES required)
            var paddedLength = (plaintext.Length + 7) / 8 * 8;
            var padded = new byte[paddedLength];
            Buffer.BlockCopy(plaintext, 0, padded, 0, plaintext.Length);

            using var des = DES.Create();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.None;
            des.Key = Convert.FromHexString(_settings.DesKey);
            des.IV = Convert.FromHexString(_settings.DesIV);

            var encrypted = des.CreateEncryptor()
                .TransformFinalBlock(padded, 0, padded.Length);

            Buffer.BlockCopy(encrypted, 0, buffer, LoginPayloadOffset, encrypted.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }
}