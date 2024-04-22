using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bismuth083.Utility.Save;

namespace Bismuth083.Utility
{
  internal class Program
  {
    public static async Task Main(string[] args)
    {

      // 格納するセーブデータの作成
      DataSet dataSet = new DataSet
        (
          "string",
          true,
          255,
          123.45,
          new int[] { 1, 2, 3, 4, 5 },
          new Coordinate(10, 20)
        );

      var saveDataManager = new SaveDataManager(Directory.GetCurrentDirectory(), SaveMode.Encrypted,"PassWord",true);

      System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew(); sw.Start();//Timer

      Console.WriteLine(saveDataManager.Road<DataSet>("User/01"));

      foreach (string slot in saveDataManager.GetSlotNames())
      {
        Console.WriteLine(slot);
      }

      saveDataManager.Save(dataSet, "001");
      saveDataManager.Save(dataSet, "User/01");

      foreach (var slot in saveDataManager.GetSlots<DataSet>())
      {
        Console.WriteLine(slot);
      }

      saveDataManager.CopySlot<DataSet>("User/01","User/02");

      // スレッドセーフかの検証
      /*
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
      */

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
      Coordinate Coord,
      string Str1 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str2 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str3 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str4 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str5 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str6 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str7 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str8 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str9 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890",
     string Str10 = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"
    );
  internal record Coordinate
    (
      int X,
      int Y
    );
}
