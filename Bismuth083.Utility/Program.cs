using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bismuth083.Utility.Save;

internal class Program
{
  public async static Task Main(string[] args)
  {

    // 格納するセーブデータの作成
    DataSet dataSet = new DataSet
      (
        "string",
        true,
        255,
        123.45,
        new int[] { 100, 200, 300, 400, 500 },
        new Coordinate(10, 20)
      );

    var saveDataManager = new SaveDataManager(Directory.GetCurrentDirectory() + "/SaveData", SaveMode.Encrypted, "PassWord", canDeleteAllSlots:true);

    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew(); sw.Start();//Timer

    // セーブ
    saveDataManager.Save<DataSet>(dataSet, "01");
    saveDataManager.Save<DataSet>(dataSet, "User/01");

    // ロード
    DataSet dataset = saveDataManager.Road<DataSet>("01").Item2!;

    // ロードデータの確認
    Console.WriteLine(dataset);
    foreach(int i in dataset.Nums)
    {
      Console.Write(i+", ");
    }
    Console.WriteLine();

    // ファイル名一覧
    foreach (string slot in saveDataManager.GetSlotNames())
    {
      Console.WriteLine(slot);
    }

    // ファイル一覧
    foreach (var slot in saveDataManager.GetSlots<DataSet>())
    {
      Console.WriteLine(slot);
    }


    saveDataManager.CopySlot("User/01", "User/03");

    // スレッドセーフかの検証
    
    Task t1 = Task.Run(() => saveDataManager.Save(dataSet, "User/01"));
    Task t2 = Task.Run(() => saveDataManager.Road<DataSet>("User/01"));
    Task t3 = Task.Run(() => saveDataManager.Save(dataSet, "User/01"));
    Task t4 = Task.Run(() => saveDataManager.Road<DataSet>("User/01"));
    Task t5 = Task.Run(() => saveDataManager.Save(dataSet, "User/01"));
    Task t6 = Task.Run(() => saveDataManager.Road<DataSet>("User/01"));
    Task t7 = Task.Run(() => saveDataManager.Save(dataSet, "User/01"));
    Task t8 = Task.Run(() => saveDataManager.Road<DataSet>("User/01"));

    await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8);
    Console.WriteLine(saveDataManager.Road<DataSet>("User/01"));

    // 削除の検証
    Console.WriteLine(saveDataManager.DeleteSlot("User/01").ToString());
    Console.WriteLine(saveDataManager.DeleteSlot("User/04").ToString());
    Console.WriteLine(saveDataManager.DeleteSlot("User.01").ToString());

    saveDataManager.DeleteAllSlots();

    sw.Stop(); Console.WriteLine(sw.Elapsed); //Timer


  }
}

internal record DataSet
  (
    string Str,
    bool Boolean,
    int Num,
    double Decimal,
    int[] Nums,
    Coordinate Coord
  );
internal record Coordinate
  (
    int X,
    int Y
  );
