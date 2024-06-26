﻿using System;
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
  /// <summary>
  /// セーブデータの管理を行うクラスです。
  /// </summary>
  /// <remarks>
  /// このクラスにおけるスロット名とは、DirectoryPath以下のパスから拡張子を除いたものです。
  /// 例: <c>C:/SomeApplication/Save/Player1/config.sav</c>で<c>>C:/SomeApplication/Save/</c>をdirectoryPathに指定した場合 -> <c>Player1/config</c>
  /// </remarks>
  public sealed class SaveDataManager
  {
    // TODO: テストケースの作成。
    // TODO: SaveからSerialize、LoadからDeserializeを分離する。
    // TODO: そもそもクラス自体をシリアライザとマネージャに分離したい。
    // TODO: クラスのExample、メソッドのTparamの説明。その他説明手直し。

		public string DirectoryPath { get; init; }
    private readonly JsonSerializerOptions jsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
		private readonly SaveMode saveMode;
    private readonly bool canDeleteAllSlots;
    private readonly TextEncryptor? textEncryptor;
    private const string EXTENTION = ".sav";

    /// <summary>
    /// SaveDataManagerのコンストラクター。ディレクトリのパスとPassWordを指定してください。
    /// </summary>
    /// <param name="directoryPath">ここで指定したディレクトリ下にセーブデータやサブディレクトリが作成されます。</param>
    /// <param name="password">暗号化する場合は必要です。十分な強度のパスワードを指定してください。パスワードを変更するとセーブデータが復号できなくなります。</param>
    /// <param name="saveMode">デフォルトでEncryptedが指定されます。Encryptedならパスワードによる暗号化が行われ、UnEncryptedならパスワードによる暗号化は行われません。</param>
    /// <param name ="canDeleteAllSlots">デフォルトでfalseが指定されます。DeleteAllSlots()を使う場合のみtrueにしてください。</param>
    /// <param name="SaveDataDirectoryName">directoryLocationで指定したディレクトリ配下に作るディレクトリの名前です。規定では"SaveData"ディレクトリが作成されます。</param>
    /// <exception cref="ArgumentException">Passwordの設定が誤っているか、無効なディレクトリパスです。</exception>
    public SaveDataManager(string directoryPath, SaveMode saveMode = SaveMode.UnEncrypted, string password = "", bool canDeleteAllSlots = false)
    {
      // ディレクトリの検証、初期化
      try
      {
        directoryPath = FileUtility.NormalizeDirectoryPath(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
          Directory.CreateDirectory(directoryPath);
        }
        this.DirectoryPath = directoryPath;
      }
      catch(Exception e)
      {
        throw new ArgumentException("無効なフォルダパスです。",nameof(directoryPath), e);
      }

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
          throw new Exception("解釈できない列挙子を検出しました。");
      }

      this.saveMode = saveMode;
      this.canDeleteAllSlots = canDeleteAllSlots;
    }

    /// <summary>
    /// 指定されたスロット名で、recodeに指定したオブジェクトを保存します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="record">保存するオブジェクトです。JSON形式にシリアライズされるため、複雑なオブジェクトでは一部データが保存されない可能性があります。</param>
    /// <param name="slotName">セーブするファイルのスロットネーム。半角英数字と、区切り文字として/が使用できます。</param>
    /// <param name="shouldCheckSlotName">slotNameが正しいかをチェックします。規定ではtrueです。</param>
    /// <param name="shouldCheckSaveData">セーブしたファイルが読み込めるか検証します。規定ではtrueです。</param>
    /// <returns></returns>
    public IOStatus Save<T>(T record, string slotName, bool shouldCheckSlotName = true, bool shouldCheckSaveData = true)
    {
      // slotNameの検証
      if (shouldCheckSlotName && !FileUtility.ValidateSlotName(slotName))
      {
        return IOStatus.InvalidSlotName;
      }
      string filePath = FileUtility.SlotNameToPath(slotName, DirectoryPath);

      // シリアライズ、(暗号化が必要ならば暗号化)
      string rawSaveDataText = JsonSerializer.Serialize(record, this.jsonOptions);
      string saveDataText = string.Empty;
      switch (saveMode)
      {
        case SaveMode.Encrypted:
          saveDataText = textEncryptor!.Encrypt(rawSaveDataText);
          break;
        case SaveMode.UnEncrypted:
          saveDataText = rawSaveDataText;
          break;
      }

      // TempFileに保存、ディレクトリが存在しない場合作成
      string directoryToBeSaved = FileUtility.NormalizeDirectoryPath(Path.GetDirectoryName(filePath)!);
      try
      {
        Directory.CreateDirectory(directoryToBeSaved);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }

      string tempFilePath = string.Empty;

      for (int i = 0; i < 100; i++)
      {
        tempFilePath = directoryToBeSaved + $"TEMPFILE_{slotName.Replace("/","_")}{EXTENTION}";
        if (!File.Exists(tempFilePath)!) break;
      }

      try
      {
        File.WriteAllText(tempFilePath, saveDataText);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }

      // セーブデータが正しいか検証。正しければ目的のファイルに保存する。
      if (shouldCheckSaveData)
      {
        var saved = Load<T>(FileUtility.FileNameToSlotName(tempFilePath , DirectoryPath)!,false);
        if (saved.status == IOStatus.Success && JsonSerializer.Serialize(saved.saveData, jsonOptions) == rawSaveDataText){ }
        else
        {
          File.Delete(tempFilePath);
          return IOStatus.UnknownError;
        }
      }
      File.Copy(tempFilePath, filePath, true);
      File.Delete(tempFilePath);
      return IOStatus.Success;
    }

    /// <summary>
    /// 指定したスロット名のセーブデータを読み込みます。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="slotName">ロードするファイルのスロットネーム。半角英数字と、区切り文字として/が使用できます。</param>
    /// <param name="shouldCheckSlotName">slotNameが正しいかをチェックします。規定ではtrueです。</param>
    /// <returns></returns>
    public (IOStatus status, T? saveData) Load<T>(string slotName, bool shouldCheckSlotName = true)
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

    /// <summary>
    /// directoryPathで指定されたフォルダ中の全セーブデータのスロット名を返します。
    /// </summary>
    /// <param name="constraint">読み込むセーブデータの条件です。規定では.savファイルのみを読み込みます。条件にワイルドカードが使用できます。</param>
    /// <returns></returns>
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

    /// <summary>
    /// directoryPathで指定されたフォルダ中の全セーブデータを返します。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="constraint">読み込むセーブデータの条件です。規定では.savファイルのみを読み込みます。条件にワイルドカードが使用できます。</param>
    /// <returns></returns>
    public IEnumerable<(string slotName,T data)> GetSlots<T>(string constraint = "*.sav")
    {
      var slotNames = GetSlotNames(constraint);
      var slots = new ConcurrentBag<(string, T)>();

      Parallel.ForEach(slotNames, slotname =>
      {
        (var status,var saveData) = Load<T>(slotname);
        if(status == 0)
        {
          slots.Add((slotname, saveData!));
        }
      });
      return slots;
    }

    /// <summary>
    /// ファイルをコピーします。
    /// </summary>
    /// <param name="slotName">コピー元ファイルのスロットネーム。半角英数字と、区切り文字として/が使用できます。</param>
    /// <param name="newSlotName">コピー先ファイルのスロットネーム。半角英数字と、区切り文字として/が使用できます。</param>
    /// <param name="allowOverWrite">trueならコピー先ファイルが既に存在するとき上書きします。falseなら何も行いません。</param>
    /// <param name="shouldCheckSlotName">slotNameが正しいかをチェックします。規定ではTrueです。</param>
    /// <returns></returns>
    public IOStatus CopySlot(string slotName, string newSlotName,bool allowOverWrite = false , bool shouldCheckSlotName = true)
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

      string filePath = FileUtility.SlotNameToPath(slotName, DirectoryPath);
      string newFilePath = FileUtility.SlotNameToPath(newSlotName, DirectoryPath);

      // ファイルの検証およびコピー
      if (!File.Exists(filePath)) return IOStatus.FileNotFound;
      else if (File.Exists(newFilePath) && !allowOverWrite) return IOStatus.AlreadyExists;
      try {
        File.Copy(filePath, newFilePath,true);
      }
      catch
      {
        return IOStatus.CouldNotAccess;
      }

      return IOStatus.Success;
    }

    /// <summary>
    /// slotNameで指定されたファイルを、存在すれば削除します。
    /// </summary>
    /// <param name="slotName">削除するファイルのスロットネーム。半角英数字と、区切り文字として/が使用できます。</param>
    /// <param name="shouldCheckSlotName">slotNameが正しいかをチェックします。規定ではTrueです。</param>
    /// <returns></returns>
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

    /// <summary>
    /// directoryPathで指定されたフォルダを削除します。
    /// 安全のため、コンストラクターでcanDeleteAllSlotsが有効である時にのみ利用できます。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">canDeleteAllSlotsがfalseになっています。利用する場合はtrueにしてください。</exception>
    public IOStatus DeleteAllSlots()
    {
      if (!canDeleteAllSlots)
      {
        throw new InvalidOperationException("\"DeleteAllSlots()\"メソッドを使用する場合は、コンストラクターの引数\"canDeleteAllSlots\"をtrueにしてください。");
      }
      try
      {
        Directory.Delete(DirectoryPath, true);
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
    UnknownError = 6,
    AlreadyExists = 7,
  }

  public enum SaveMode
  {
    Encrypted,
    UnEncrypted
  }

  internal static class FileUtility
  {
    private const string EXTENTION = ".sav";
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
        slotName = fileName.Substring(0, fileName.IndexOf(EXTENTION)).Replace(directoryPath, "");
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
      return String.Concat(directoryPath, slotName, EXTENTION);
    }
  }
}
