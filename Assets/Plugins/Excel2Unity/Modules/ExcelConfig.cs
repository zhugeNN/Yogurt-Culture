using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace Excel2Unity
{
  public class ExcelConfig
  {
    private static string xmlConfigPathAndName = "Plugins/Excel2Unity/Modules/ExcelXmlConfig.xml";
    public static string XmlConfigPathAndName
    {
      get
      {
        return Path.Combine(AssetsPath, xmlConfigPathAndName);
      }
    }

    public static string AssetsPath = Application.dataPath;   // 项目的Assets文件夹的完整路径
    private static string excelTablePath = "Main/DataTable/Excel";
    public static string ExcelTablePath
    {
      get
      {
        var path = Path.Combine(AssetsPath, excelTablePath);
        if (!Directory.Exists(path))
          Directory.CreateDirectory(path);
        return path;
      }
    }

    private static string jsonDataPath = "Resources/DataTable/JsonData"; //json文件的写入路径
    public static string JsonDataPath
    {
      get
      {
        var path = Path.Combine(AssetsPath, jsonDataPath);
        if (!Directory.Exists(path))
          Directory.CreateDirectory(path);
        return path;
      }
    }
    public static string jsonDataResourcesLoadPath = "DataTable/JsonData"; // json文件通过Resources路径读取

    private static string cSharpPath = "Scripts/Excel";
    public static string CSharpPath
    {
      get
      {
        var path = Path.Combine(AssetsPath, cSharpPath);
        if (!Directory.Exists(path))
        {
          Directory.CreateDirectory(path);
        }
        return path;
      }
    }

    public static string PrefabsLoadBasePath = "Prefabs/";
    public static string SOLoadBasePath = "SO/";

  }
}