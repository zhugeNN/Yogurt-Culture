using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Excel2Unity
{
  public class ExcelXmlConfig
  {
    public string ExcelPath { get; set; }
    public string JsonPath { get; set; }
    public string WorksheetName { get; set; }
    public string ClassName { get; set; }
    /// <summary>
    /// 需要更新的SO的共同路径（如果有），在Resources文件夹下，格式类似"GeneData/"
    /// </summary>
    public string SOPath { get; set; }
    public string SOClassName { get; set; }
  }

  public class ExcelXmlConfiguration
  {
    public ExcelXmlConfig[] ExcelXmlConfigs { get; set; }
  }

  public class XmlConfigLoader
  {
    public static ExcelXmlConfiguration xmlCacheConfiguration;
    public static ExcelXmlConfiguration LoadConfigs()
    {
      XmlSerializer serializer = new XmlSerializer(typeof(ExcelXmlConfiguration));
      bool isXmlOpen = false;
      try
      {
        var xmlPath = ExcelConfig.XmlConfigPathAndName;
        using (StreamReader reader = new(xmlPath))
        {
          isXmlOpen = true;
          var configuration = (ExcelXmlConfiguration)serializer.Deserialize(reader);
          if (configuration != null)
          {
            xmlCacheConfiguration = configuration;
            return configuration;
          }
          return null;
        }

      }
      catch (Exception ex)
      {
        UnityEngine.Debug.LogWarning((isXmlOpen ? "xml配置文件解析失败，请检查xml文件格式！" : "xml配置文件加载失败！")
        + Environment.NewLine + "异常信息：" + ex.Message);
        return null;
      }
    }
    public static ExcelXmlConfig LoadConfigByExcelName(string excelName)
    {
      ExcelXmlConfiguration configs = xmlCacheConfiguration != null ? xmlCacheConfiguration : LoadConfigs();
      excelName = Path.GetFileNameWithoutExtension(excelName);
      if (configs?.ExcelXmlConfigs != null)
      {
        var config = configs.ExcelXmlConfigs.FirstOrDefault(info => Path.GetFileNameWithoutExtension(info.ExcelPath) == excelName);
        if (config != null)
          return config;
      }
      UnityEngine.Debug.LogWarning($"查找 {excelName} xml配置文件失败!");
      return null;
    }
    public static List<ExcelXmlConfig> LoadConfigsByExcelName(string excelName)
    {
      ExcelXmlConfiguration configs = xmlCacheConfiguration != null ? xmlCacheConfiguration : LoadConfigs();
      excelName = Path.GetFileNameWithoutExtension(excelName);
      if (configs?.ExcelXmlConfigs != null)
      {
        var config = configs.ExcelXmlConfigs.Where(info => Path.GetFileNameWithoutExtension(info.ExcelPath) == excelName).ToList();
        if (config != null)
          return config;
      }
      UnityEngine.Debug.LogWarning($"查找 {excelName} xml配置文件失败!");
      return null;
    }
    public static ExcelXmlConfig LoadConfigByClassName(string className)
    {
      ExcelXmlConfiguration configs = xmlCacheConfiguration != null ? xmlCacheConfiguration : LoadConfigs();
      if (configs?.ExcelXmlConfigs != null)
      {
        var config = configs.ExcelXmlConfigs.FirstOrDefault(info => Path.GetFileNameWithoutExtension(info.ClassName) == className);
        if (config != null)
          return config;
      }
      UnityEngine.Debug.LogWarning($"查找 {className} xml配置文件失败!");
      return null;
    }
  }
}