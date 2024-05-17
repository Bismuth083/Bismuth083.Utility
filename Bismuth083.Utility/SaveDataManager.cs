using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Bismuth083.Utility.Encrypt;

namespace Bismuth083.Utility.Save
{
  public sealed class SaveDataManager
  {
    // TODO: マルチスレッドでも例外が発生しないようにしたい。
    // TODO: テストケースの作成。
    // TODO: Saveに失敗したときの差し戻しを行う、安全のため！！
    // TODO: Documentation Commentを作成する。

    public string DirectoryPath { get; init; }
    private readonly JsonSerializerOptions jsonOptions = new()
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
    /// <param name="directoryLocation">ここで指定したディレクトリ配下にSaveDataディレクトリを作成します。</param>
    /// <param name="password">暗号化する場合は必要です。1-32文字の半角文字で指定してください。パスワードを変更するとセーブデータが復号できなくなります。</param>
    /// <param name="saveMode">デフォルトでEncryptedが指定されます。Encryptedならパスワードによる暗号化が行われ、UnEncryptedならパスワードによる暗号化は行われません。</param>
    /// <param name ="canDeleteAllSlots">デフォルトでfalseが指定されます。DeleteAllSlots()を使う場合のみtrueにしてください。</param>
    /// <param name="SaveDataDirectoryName">directoryLocationで指定したディレクトリ配下に作るディレクトリの名前です。規定では"SaveData"ディレクトリが作成されます。</param>
    /// <exception cref="ArgumentException"></exception>
    public SaveDataManager(string directoryLocation, SaveMode saveMode = SaveMode.UnEncrypted, string password = "", string SaveDataDirectoryName = "SaveData/", bool canDeleteAllSlots = false)
    {
      // ディレクトリの検証、初期化
      string directoryPath = FileUtility.NormalizeDirectoryPath(directoryLocation + SaveDataDirectoryName);
      if (!Directory.Exists(directoryPath))
      {
        Directory.CreateDirectory(directoryPath);
      }
      this.DirectoryPath = directoryPath;

      // パスワードの設定
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

    public IOStatus Save<T>(T record, string slotName, bool shouldCheckSlotName = true, bool shouldCheckSaveData = true)
    {
      // slotNameの検証
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(slotName))
      {
        return IOStatus.InvalidSlotName;
      }
      string filePath = FileUtility.SlotNameToPath(slotName, DirectoryPath);

      // シリアライズ、(暗号化が必要ならば暗号化)
      string saveDataText = JsonSerializer.Serialize(record, this.jsonOptions);
      switch (saveMode)
      {
        case SaveMode.Encrypted:
          saveDataText = textEncryptor!.Encrypt(saveDataText);
          break;
        case SaveMode.UnEncrypted:
          break;
      }

      // ローカルフォルダに保存、ディレクトリが存在しない場合作成
      string directoryToBeSaved = Path.GetDirectoryName(filePath)!;
      try
      {
        Directory.CreateDirectory(directoryToBeSaved);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }

      try
      {
        File.WriteAllText(filePath, saveDataText);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }

      // セーブデータが正しいか検証
      if (shouldCheckSaveData)
      {
        var saved = Road<T>(slotName);
        if(saved.status == 0 && JsonSerializer.Serialize(saved, this.jsonOptions) == saveDataText)
        {
          return IOStatus.Success;
        }
        else
        {
          return IOStatus.UnknownError;
        }
      }

      return IOStatus.Success;

      //using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
      //using (var ws = TextWriter.Synchronized(sw))
      //{
      //  ws.WriteLine(saveDataText);
      //}
      //
      // あかんかったらこれで非同期処理をなんとかする。
    }

    public (IOStatus status, T? saveData) Road<T>(string slotName, bool shouldCheckSlotName = true)
    {
      string readText;
      T? data;
      // slotNameの検証
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(slotName))
      {
        return (IOStatus.InvalidSlotName, default);
      }

      // ファイルの検索
      string filePath = FileUtility.SlotNameToPath(slotName, DirectoryPath);
      if (!File.Exists(filePath))
      {
        return (IOStatus.FileNotFound, default);
      }

      // ファイルの読み込み
      try
      {
       readText = File.ReadAllText(filePath);  
      }
      catch
      {
        return (IOStatus.CouldNotAccess, default);
      }

      // 必要なら復号化
      switch (saveMode)
      {
        case SaveMode.Encrypted:
          try
          {
            readText = textEncryptor!.Decrypt(readText);
          }
          catch
          {
            return (IOStatus.CouldNotDecrypt, default);
          }
          break;
        case SaveMode.UnEncrypted:
          break;
      }

      // JSonのデシリアライズ
      try
      {
       data = JsonSerializer.Deserialize<T?>(readText, jsonOptions);
      }
      catch
      {
        return (IOStatus.InvalidJsonFormat, default);
      }
      return (IOStatus.Success, data);
    }

    public IEnumerable<string> GetSlotNames(string constraint = "*.sav")
    {
      var slotNames = new ConcurrentBag<string>();

      var fileNames = Directory.EnumerateFiles(DirectoryPath, constraint, SearchOption.AllDirectories)
        .Select(x => FileUtility.NormalizeDirectoryPath(x));

      Parallel.ForEach(fileNames, fileName =>
      {
        if (fileName is not null)
        {
          slotNames.Add(FileUtility.FileNameToSlotName(fileName!, DirectoryPath)!);
        }
      }
    );
      return slotNames;
    }

    public IEnumerable<(string slotName,T data)> GetSlots<T>(string constraint = "*.sav")
    {
      var slotNames = GetSlotNames(constraint);
      var slots = new ConcurrentBag<(string, T)>();

      Parallel.ForEach(slotNames, slotname =>
      {
        (var status,var saveData) = Road<T>(slotname);
        if(status == 0)
        {
          slots.Add((slotname, saveData!));
        }
      });
      return slots;
    }

    public IOStatus CopySlot<T>(string slotName, string newSlotName, bool shouldCheckSlotName = true)
    {
      // slotNameの検証
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(slotName)) 
      {
        return IOStatus.InvalidSlotName;
      }
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(newSlotName))
      {
        return IOStatus.InvalidSlotName;
      }

      // RoadおよびSave
      var temp = Road<T>(slotName);
      
      if(temp.status == 0)
      {
        IOStatus status = Save(temp.saveData, newSlotName);
        return status;
      }
      else
      {
        return temp.status;
      }
    }

    public IOStatus DeleteSlot(string slotName, bool shouldCheckSlotName=true)
    {
      // ファイルの名の検証
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(slotName))
      {
        return IOStatus.InvalidSlotName;
      }

      // ファイルの存在確認
      string filePath = FileUtility.SlotNameToPath(slotName, DirectoryPath);

      if (!File.Exists(filePath)) {
        return IOStatus.FileNotFound;
      }

      // 削除
      try
      {
        File.Delete(filePath);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }
      return IOStatus.Success;
    }

    public IOStatus DeleteAllSlots()
    {
      if (!canDeleteAllSlots)
      {
        throw new InvalidOperationException("\"DeleteAllSlots()\"メソッドを使用する場合は、コンストラクターの引数\"canDeleteAllSlots\"をtrueにしてください。");
      }
      try
      {
        Directory.Delete(DirectoryPath, true);
        Directory.CreateDirectory(DirectoryPath);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }
      return IOStatus.Success;
    }
  }

  public enum IOStatus
  {
    Success = 0,
    FileNotFound = 1,
    CouldNotAccess = 2,
    CouldNotDecrypt = 3,
    InvalidJsonFormat = 4,
    InvalidSlotName = 5,
    UnknownError = 6
  }

  public enum SaveMode
  {
    Encrypted,
    UnEncrypted
  }

  internal static class FileUtility
  {
    internal static bool ValidateSlotName(string slotName)
    {
      // 先頭、最後尾に/を付けず、かつフォルダパスとして合法な書き方のみtrue。
      return slotName.Length != 0 &&
         !Regex.IsMatch(slotName, "[\\:*?\"<>|.,]+") &&
         !Regex.IsMatch(slotName, "^/") && !Regex.IsMatch(slotName, "/$") &&
         !Regex.IsMatch(slotName, "//");
    }

    internal static string? FileNameToSlotName(string fileName, string directoryPath)
    {
      string? slotName;
      if (fileName.Contains(".sav"))
      {
        slotName = fileName.Substring(fileName.IndexOf(".sav")).Replace(directoryPath, "");
        return slotName;
      }
      else
      {
        return null;
      }
    }

        internal static string NormalizeDirectoryPath(string path)
    {
      string newPath = path.Replace("\\", "/");
      newPath = newPath.TrimEnd('/');
      newPath += "/";
      return newPath;
    }

    internal static string SlotNameToPath(string slotName, string directoryPath)
    {
      return String.Concat(directoryPath, slotName, ".sav");
    }
  }
}
