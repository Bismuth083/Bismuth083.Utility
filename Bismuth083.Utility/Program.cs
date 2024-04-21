using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth083.Utility
{
  internal class Program
  {
    public static void Main(string[] args)
    {
      // 格納するセーブデータの作成
      DataSet dataSet = new DataSet
        (
          "String",
          true,
          255,
          123.45,
          new int[] { 1, 2, 3, 4, 5 },
          new Coordinate(10, 20)
        );

      var saveDataManager = new SaveDataManager(Directory.GetCurrentDirectory(), SaveMode.Encrypted,"PassWord");

      saveDataManager.Save(dataSet, "User/01");
      saveDataManager.Save(dataSet, "001");

      Console.WriteLine(saveDataManager.Road<DataSet>("User/01"));

      foreach (string slot in saveDataManager.GetSlotNames())
      {
        Console.WriteLine(slot);
      }

      foreach (var slot in saveDataManager.GetSlots<DataSet>())
      {
        Console.WriteLine(slot);
      }

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
}
