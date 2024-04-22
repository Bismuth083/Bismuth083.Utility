using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Bismuth083.Utility.Encrypt;

namespace Bismuth083.Utility.Save
{
  public sealed class SaveDataManager
  {
    // TODO: マルチスレッドでも例外が発生しないようにしたい

    public string DirectoryPath { get; init; }
    private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly SaveMode saveMode;
    private readonly bool canDeleteAllSlots;
    private readonly TextEncryptor? textEncryptor;

    /// <summary>
    /// SaveDataManagerのコンストラクター。ディレクトリのパスとPassWordを指定してください。
    /// </summary>
    /// <param name="directoryPath">ここで指定したディレクトリにSaveDataディレクトリを作成します。</param>
    /// <param name="password">暗号化する場合は必要です。1-32文字の半角文字で指定してください。パスワードを変更するとセーブデータが復号できなくなります。</param>
    /// <param name="saveMode">デフォルトでEncryptedが指定されます。Encryptedならパスワードによる暗号化が行われ、UnEncryptedならパスワードによる暗号化は行われません。</param>
    /// <param name ="canDeleteAllSlots">デフォルトでfalseが指定されます。DeleteAllSlots()を使う場合のみtrueにしてください。</param>
    /// <exception cref="ArgumentException"></exception>
    public SaveDataManager(string directoryLocation, SaveMode saveMode = SaveMode.Encrypted, string password = "", bool canDeleteAllSlots = false)
    {
      // ディレクトリの検証、初期化
      const string SaveDataDirectoryName = "SaveData/";
      string directoryPath = NormalizeDirectoryPath(directoryLocation) + SaveDataDirectoryName;
      if (!Directory.Exists(directoryPath))
      {
        Directory.CreateDirectory(directoryPath);
      }
      this.DirectoryPath = directoryPath;

      // パスワードの検証、初期化
      switch (saveMode)
      {
        case SaveMode.Encrypted:
          textEncryptor = new TextEncryptor(password);
          break;
        case SaveMode.UnEncrypted:
          if (password.Length != 0)
          {
            throw new ArgumentException("SaveModeがUnEncryptedになっているにもかかわらず、passwordが設定されています。passwordの指定を外すか、saveModeをEncryptedにしてください。", nameof(saveMode));
          }
          textEncryptor = null;
          break;
          throw new ArgumentException("解釈できない列挙子を検出しました。", nameof(saveMode));
      }

      this.saveMode = saveMode;
      this.canDeleteAllSlots = canDeleteAllSlots;
    }

    public void Save<T>(T record, string slotName)
    {
      if (!ValidateSlotName(slotName))
      {
        throw new ArgumentException("スロット名が不正です。区切り文字として/のみを利用し、最初と最後の文字に/を使用しないでください。", nameof(slotName));
      }

      string saveDataText = JsonSerializer.Serialize(record, this.jsonOptions);
      switch (saveMode)
      {
        case SaveMode.Encrypted:
          saveDataText = textEncryptor!.Encrypt(saveDataText);
          break;
        case SaveMode.UnEncrypted:
          break;
      }

      string filePath = SlotNameToPath(slotName);
      if (!File.Exists(Path.GetDirectoryName(filePath)))
      {
         Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
      }

      using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
      using (var ws = TextWriter.Synchronized(sw))
      {
        ws.WriteLine(saveDataText);
      }
    }

    public (bool, T?) Road<T>(string slotName)
    {
      if(!ValidateSlotName(slotName))
      {
        throw new ArgumentException("スロット名が不正です。区切り文字として/のみを利用し、最初と最後の文字に/を使用しないでください。", nameof(slotName));
      }

      string filePath = SlotNameToPath(slotName);

      if (!File.Exists(filePath))
      {
        return (false, default(T));
      }

      string saveDataText;

      using (var sr = new StreamReader(filePath, Encoding.UTF8))
      using (var tr = TextReader.Synchronized(sr))
      {
        saveDataText = tr.ReadToEnd();
      }

      switch (saveMode)
      {
        case SaveMode.Encrypted:
          saveDataText = textEncryptor!.Decrypt(saveDataText);
          break;
        case SaveMode.UnEncrypted:
          break;
      }
      T? t = JsonSerializer.Deserialize<T>(saveDataText, jsonOptions);

      return (true, t);
    }

    public IEnumerable<string> GetSlotNames(string constraint = "*")
    {
      var fileNames = Directory.EnumerateFiles(DirectoryPath, constraint+".sav", SearchOption.AllDirectories)
        .Select(x => NormalizeDirectoryPath(x));
      var SlotNames = new HashSet<string>();
      foreach (var fileName in fileNames!)
      {
        if (fileName is not null)
        {
          SlotNames.Add(FileNameToSlotName(fileName!)!);
        }
      }
      return SlotNames;
    }

    public IEnumerable<(string, T)> GetSlots<T>(string constraint = "*")
    {
      var slotNames = GetSlotNames(constraint);
      var values = new HashSet<(string, T)>();
      foreach (var slotName in slotNames)
      {
        var data = this.Road<T>(slotName);
        values.Add((slotName, data.Item2!));
      }
      return values;
    }
    
    public void CopySlot<T>(string slotName, string newSlotName)
    {
      if (!ValidateSlotName(slotName))
      {
        throw new ArgumentException("スロット名が不正です。区切り文字として/のみを利用し、最初と最後の文字に/を使用しないでください。", nameof(slotName));
      }
      if (!ValidateSlotName(newSlotName))
      {
        throw new ArgumentException("スロット名が不正です。区切り文字として/のみを利用し、最初と最後の文字に/を使用しないでください。", nameof(newSlotName));
      }

      T? roadedData = this.Road<T>(slotName).Item2;
      if (roadedData is not null)
      {
        this.Save(roadedData, newSlotName);
      }
    }

    public bool DeleteSlot(string slotName)
    {
      if (!ValidateSlotName(slotName))
      {
        throw new ArgumentException("スロット名が不正です。区切り文字として/のみを利用し、最初と最後の文字に/を使用しないでください。", nameof(slotName));
      }
      string filePath = SlotNameToPath(slotName);

      if (!File.Exists(filePath))
      {
        return false;
      }
      File.Delete(filePath);
      return true;
    }

    public void DeleteAllSlots()
    {
      if (!canDeleteAllSlots)
      {
        throw new InvalidOperationException("\"DeleteAllSlots()\"メソッドを使用する場合は、コンストラクターの引数\"canDeleteAllSlots\"をtrueにしてください。");
      }
      Directory.Delete(DirectoryPath, true);
    }

    private string NormalizeDirectoryPath(string path)
    {
      string newPath = path.Replace("\\", "/");
      newPath = newPath.TrimEnd('/');
      newPath += "/";
      return newPath;
    }

    private string SlotNameToPath(string slotName)
    {
      return String.Concat(DirectoryPath, slotName, ".sav");
    }

    private string? FileNameToSlotName(string fileName)
    {
      string? slotName = null;
      if (fileName.Contains(".sav")) {
        slotName = fileName.Substring(0,fileName.IndexOf(".sav")).Replace(this.DirectoryPath,"");
        return slotName;
      }
      else
      {
        return null;
      }
    }

    private bool ValidateSlotName(string slotName)
    {
      // 先頭、最後尾に/を付けず、かつフォルダパスとして合法な書き方のみtrue。
     return slotName.Length != 0 &&
        !Regex.IsMatch(slotName, "[\\:*?\"<>|.,]+") &&
        !Regex.IsMatch(slotName, "^/") && !Regex.IsMatch(slotName, "/$") &&
        !Regex.IsMatch(slotName, "//"); 
    }
  }

  public enum SaveMode
  {
    Encrypted,
    UnEncrypted
  }
}
