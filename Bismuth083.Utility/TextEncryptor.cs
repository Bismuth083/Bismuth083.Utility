﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth083.Utility
{
  internal class TextEncryptor
  {
    private readonly byte[] aesKey = null!;
    private const int KeySize = 256;
    private const int BlockSize = 128;

    public TextEncryptor(string passWord)
    {
      aesKey = PassWordToByteArray(passWord);
    }

    public string Encrypt(string unEncryptedText)
    {
      using (Aes aes = Aes.Create())
      {
        aes.KeySize = KeySize;
        aes.Key = this.aesKey;
        aes.BlockSize = BlockSize;
        aes.IV = RandomNumberGenerator.GetBytes(BlockSize / 8);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var encryptor = aes.CreateDecryptor(aes.Key, aes.IV);

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
        Array.Copy(encryptedBytes, 0, aes.IV, 0, BlockSize / 8);
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
      List<byte> temp = Encoding.UTF8.GetBytes(password).ToList();
      if (temp.Count > 32 || temp.Count == 0)
      {
        throw new ArgumentException("Passwordは半角かつ、長さが1-32文字になるように指定してください。", nameof(password));
      }
      while (temp.Count < 32)
      {
        temp.Add(0);
      }
      return temp.ToArray();
    }
  }
}