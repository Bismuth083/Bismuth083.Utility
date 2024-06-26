﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Bismuth083.Utility.Encrypt
{
  public sealed class TextEncryptor
  {
    private readonly byte[] aesKey = null!;
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private readonly byte[] iv = null!;

    public TextEncryptor(string passWord)
    {
      aesKey = PassWordToByteArray(passWord);
      iv = RandomNumberGenerator.GetBytes(BlockSize / 8);
    }

    public string Encrypt(string unEncryptedText)
    {
      using (Aes aes = Aes.Create())
      {
        aes.KeySize = KeySize;
        aes.Key = this.aesKey;
        aes.BlockSize = BlockSize;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using (var ms = new MemoryStream())
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
          ms.Write(aes.IV);
          using (var sw = new StreamWriter(cs))
          {
            sw.Write(unEncryptedText);
          }
          return Convert.ToBase64String(ms.ToArray());
        }
      }
    }

    public string Decrypt(string encryptedText)
    {
      var encryptedBytes = Convert.FromBase64String(encryptedText);
      using (Aes aes = Aes.Create())
      {
        aes.KeySize = KeySize;
        aes.Key = this.aesKey;
        aes.BlockSize = BlockSize;
        aes.IV = encryptedBytes[0..(BlockSize/8)];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using (var ms = new MemoryStream())
        {
          using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
          using (var bw = new BinaryWriter(cs))
          {
            bw.Write(encryptedBytes, aes.IV.Length, encryptedBytes.Length - aes.IV.Length);
          }
          return Encoding.UTF8.GetString(ms.ToArray());
        }
      }
    }

    private static byte[] PassWordToByteArray(string password)
    {　
      var encoding = Encoding.GetEncoding("UTF-8");
      return SHA256.HashData(encoding.GetBytes(password));
    }
  }
}
