using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Excel2Unity
{

  public delegate void OnJsonUpdateDele(string jsonName);
  public class ExcelToCSharpClass
  {
#if UNITY_EDITOR

    public const string excelTempHeader = "~$";
    public static event OnJsonUpdateDele OnJsonUpdate;

    [MenuItem("Assets/ExcelTool/ExcelToJson")]
    public static void ExcelConvertToJson()
    {
      ExcelConvertToJsonAndClass(false);
    }
    [MenuItem("Assets/ExcelTool/ExcelToJsonAndClass")]
    public static void ExcelConvertToJsonAndClass()
    {
      ExcelConvertToJsonAndClass(true);
    }
    [MenuItem("Assets/ExcelTool/ExcelToJsonAndForceUpdateClass")]
    public static void ExcelConvertToJsonAndForceUpdateClass()
    {
      ExcelConvertToJsonAndClass(true, true);
    }


    private static void ExcelConvertToJsonAndClass(bool ifGenerateClass, bool allowUpdateClass = false)
    {
      var selectedObject = Selection.activeObject;
      if (selectedObject == null)
      {
        Debug.LogError("没有选中需要转换的文件");
        return;
      }
      var assetPath = AssetDatabase.GetAssetPath(selectedObject);
      if (Path.GetExtension(assetPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
      {
        //说明选中的是单个Excel文件
        ExcelFileConvertToJson(assetPath, ifGenerateClass, allowUpdateClass);
      }
      else if (AssetDatabase.IsValidFolder(assetPath))
      {
        //说明选中的是一个文件夹->遍历文件夹下的Excel文件
        var filePaths = Directory.GetFiles(assetPath);
        foreach (var filePath in filePaths)
        {
          if (Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
          {
            ExcelFileConvertToJson(filePath, ifGenerateClass, allowUpdateClass);
          }
        }
      }
      else
      {
        Excel2UnityUtils.LogWarning("选中的文件不符合转换要求");
      }
    }

    //表格格式
    //字段描述 序号 名称
    //字段名   ID   Name
    //字段类型 int  string
    public static void ExcelFileConvertToJson(string filePath, bool ifGenerateClass = false, bool allowUpdateClass = false)
    {
      var file = new FileInfo(filePath);
      var excelName = Path.GetFileNameWithoutExtension(filePath);
      if (!file.Exists || string.IsNullOrEmpty(excelName))
      {
        Excel2UnityUtils.LogWarning($"json转换导表失败!文件不存在:{filePath}");
        return;
      }
      if (excelName.EndsWith(excelTempHeader)) return;
      // 由于一个excel可能有多个不同的sheet对应不同的导表，所以结果是一个config数组
      var configExcelName = excelName.Replace(excelTempHeader, "");
      var xmlConfigs = XmlConfigLoader.LoadConfigsByExcelName(configExcelName);
      if (xmlConfigs == null || xmlConfigs.Count == 0)
      {
        Excel2UnityUtils.LogWarning($"json转换导表失败!请在xml配置文件中正确注册:{excelName}");
        return;
      }
      foreach (var config in xmlConfigs)
      {
        try
        {
          using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          {
            using (ExcelPackage package = new ExcelPackage(stream))
            {
              var worksheetName = config.WorksheetName;
              if (package.Workbook.Worksheets.Any(ws => ws.Name == worksheetName))
              {
                var worksheet = package.Workbook.Worksheets[worksheetName];
                if (worksheet != null)
                  ExcelSheetToJsonData(filePath, worksheet, config, ifGenerateClass, allowUpdateClass);
                package.Save();
              }
              else
              {
                Excel2UnityUtils.LogWarning($"{excelName}没有找到工作表{worksheetName}");
              }
            }
          }

        }
        catch (Exception e)
        {
          Excel2UnityUtils.LogWarning($"导表{excelName}过程出现异常!\n异常信息: " + e);
        }
      }

    }

    private static void ExcelSheetToJsonData(string filePath, ExcelWorksheet worksheet,
     ExcelXmlConfig xmlConfig, bool ifGenerateClass = false, bool allowUpdateClass = false)
    {
      var excelName = Path.GetFileNameWithoutExtension(filePath);
      if (worksheet == null || xmlConfig == null) return;

      // 存储所有非空列： key为列索引，value为(dataName,dataType)
      var colHeadList = new Dictionary<int, Tuple<string, string>>();

      var rowCount = worksheet.Dimension.End.Row;
      var colCount = worksheet.Dimension.End.Column;
      var tableData = new List<Dictionary<string, object>>();
      // Note: 在 Excel 中，行和列的索引都是从 1 开始的，而不是从 0 开始的
      // 先读每一列的数据名、数据类型
      for (var j = 1; j <= colCount; j++)
      {
        // 添加字段信息
        // 第二行 数据名
        var dataName = worksheet.Cells[2, j].Value?.ToString().Trim().Replace(" ", string.Empty); ;
        // 第三行 数据类型
        var dataType = worksheet.Cells[3, j].Value?.ToString().Trim().Replace(" ", string.Empty); ;
        if (!string.IsNullOrEmpty(dataName) && !string.IsNullOrEmpty(dataType))
        {
          //避免重复添加
          if (!colHeadList.ContainsKey(j))
          {
            colHeadList.Add(j, new Tuple<string, string>(dataName, dataType));
          }
        }
      }
      //从第四行开始为实际的数据列
      for (var i = 4; i <= rowCount; i++)
      {
        var rowData = new Dictionary<string, object>();
        var isRowEmpty = true;
        foreach (var j in colHeadList.Keys)
        {
          var dataName = colHeadList[j].Item1;
          var dataType = colHeadList[j].Item2;

          var valueStr = worksheet.Cells[i, j].Value?.ToString().Trim();

          // 只有当第一列（默认为ID列）不为空时，该行才视为有效行；否则跳过该行
          if (j == 1)
          {
            if (!string.IsNullOrEmpty(valueStr))
              isRowEmpty = false;
            else
              break;
          }
          // Debug.Log($"Try parsing {dataName} of type {dataType}, value {valueStr} ");
          rowData[dataName] = ConvertValueByType(valueStr, dataType);
          // Debug.Log($"success");
        }
        if (!isRowEmpty)
        {
          tableData.Add(rowData);
        }
      }

      //1.生成Json文件
      string json = JsonConvert.SerializeObject(tableData, Formatting.Indented);
      var jsonPath = xmlConfig.JsonPath;

      Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");             //修复中文乱码
      json = reg.Replace(json, delegate (Match m) { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); });
      string jsonFileName = jsonPath.EndsWith(".json") ? jsonPath : jsonPath + ".json";
      string JsonFilePath = Path.Combine(ExcelConfig.JsonDataPath, jsonFileName);

      if (File.Exists(JsonFilePath))
      {
        File.Delete(JsonFilePath);
      }
      using (FileStream fileStream = new FileStream(JsonFilePath, FileMode.Create, FileAccess.Write))
      {
        using (TextWriter textWriter = new StreamWriter(fileStream, Encoding.GetEncoding("utf-8")))
        {
          textWriter.Write(json);
        }
      }
      OnJsonUpdate?.Invoke(jsonPath);

      ExcelSOUpdater.UpdateSOAsync(xmlConfig, tableData);
      Excel2UnityUtils.Log($"<color=#00ff00> 导出excel表{excelName} {worksheet.Name}为json成功!</color> ");

      if (ifGenerateClass)
      {
        //2.生成CSharpClass文件
        var dataNameList = new List<string>();
        var dataTypeList = new List<string>();
        foreach (var item in colHeadList)
        {
          dataNameList.Add(item.Value.Item1);
          dataTypeList.Add(item.Value.Item2);
        }
        CreatCSharpForGameData(Path.GetFileNameWithoutExtension(xmlConfig.ClassName), dataNameList, dataTypeList, allowUpdateClass);
      }
      EditorApplication.delayCall += () => AssetDatabase.Refresh();
    }


    private static void CreatCSharpForGameData(string className, List<string> dataNameList, List<string> dataTypeList, bool allowUpdateClass = false)
    {
      //主程序集（Assets/Scripts目录下即是）
      if (!allowUpdateClass && Excel2UnityUtils.TryGetType(className) != null)
      {
        Excel2UnityUtils.Log($"{className}类已存在,忽略脚本生成");
        return;
      }
      //创建一个StringBuilder来构建C#脚本
      StringBuilder scriptStr = new StringBuilder();

      //在脚本开头添加必要的using语句
      scriptStr.AppendLine("using System;");
      scriptStr.AppendLine("using UnityEngine;");
      scriptStr.AppendLine("using Excel2Unity;");
      scriptStr.AppendLine("using System.Collections.Generic;");

      scriptStr.AppendLine("\n[Serializable]");
      // Note:类名设置为与excel表的名称相同
      scriptStr.AppendLine("\npublic class " + className + " : TableDataBase");
      scriptStr.AppendLine("{");

      //遍历dataName和dataType列表以生成类的属性
      for (int i = 0; i < dataNameList.Count; ++i)
      {
        if (dataNameList[i].ToLower() == "id") continue; //ID字段在基类中，不必再声明
        scriptStr.AppendLine($"\tpublic {dataTypeList[i]} {dataNameList[i]};");
      }
      scriptStr.AppendLine("}");

      string path = Path.Combine(ExcelConfig.CSharpPath, className + ".cs");
      if (File.Exists(path))
      {
        File.Delete(path);
      }
      File.WriteAllText(path, scriptStr.ToString());
      Excel2UnityUtils.Log($"C#脚本生成成功：{path}");
    }
#endif

    public static object ConvertValueByType(string valueStr, string dataTypeStr)
    {
      object val = null;
      var lowerDataTypeStr = dataTypeStr.ToLowerInvariant();
      //TODO：这里扩展支持的数据类型
      try
      {
        switch (lowerDataTypeStr)
        {
          case "int":
            val = string.IsNullOrEmpty(valueStr) ? 0 : int.Parse(valueStr);
            break;
          case "float":
            val = string.IsNullOrEmpty(valueStr) ? 0.0f : float.Parse(valueStr);
            break;
          case "bool":
            val = valueStr == "1" || string.Equals(valueStr, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueStr, "t", StringComparison.OrdinalIgnoreCase);
            break;
          case "string":
            val = string.IsNullOrEmpty(valueStr) ? "" : valueStr;
            break;
          default:
            {
              if (lowerDataTypeStr.Contains("list"))
              {
                var innerType = Regex.Replace(dataTypeStr, @"(?i)^list<([^>]+)>$", "$1").Trim();
                // 采用原始类型字符串，尝试解析其类型
                //Excel2UnityUtils.Log($"尝试解析数据类型:{innerType}");
                Type DataType = TryGetType(innerType);
                // 获取到其类型
                if (DataType != null)
                {
                  // Debug.LogWarning($"解析数据类型成功:{DataType}");
                  MethodInfo method = typeof(ExcelToCSharpClass).GetMethod("ParseList", BindingFlags.NonPublic | BindingFlags.Static);
                  MethodInfo genericMethod = method.MakeGenericMethod(DataType);
                  val = genericMethod.Invoke(null, new object[] { valueStr });
                }
                else
                {
                  Excel2UnityUtils.LogError($"解析数据类型失败:{innerType}");
                  // 尝试读取多维lsit
                  //val = ConvertValueByType(valueStr, innerType);
                }
              }
              else
              {
                Type DataType = TryGetType(dataTypeStr);
                // 获取到其类型
                if (DataType != null)
                {
                  //Excel2UnityUtils.Log("解析数据类型成功:" + DataType);
                  if (DataType.IsEnum)
                  {
                    if (Enum.TryParse(DataType, valueStr, true, out object enumValue))
                      val = Enum.ToObject(DataType, enumValue);
                    else
                      val = Enum.GetValues(DataType).GetValue(0); // 默认值通常是第一个枚举成员
                    break;
                  }
                }
                else
                {
                  Excel2UnityUtils.LogWarning($"无法解析数据类型:{dataTypeStr}");
                }
              }
              break;
            }
        }
      }
      catch
      {
        Excel2UnityUtils.LogWarning($"导表过程出现异常!\n出错数据: " + valueStr);
        throw;
      }
      return val;
    }

    private static Type TryGetType(string typeName) => Excel2UnityUtils.TryGetType(typeName);
    private static List<T> ParseList<T>(string valueStr)
    {
      List<T> list = new List<T>();
      if (string.IsNullOrEmpty(valueStr)) return list;
      // 使用String.Split方法和参数数组来指定多个分隔符
      string[] values = valueStr.Split(new string[] { ",", "，", ";" }, StringSplitOptions.RemoveEmptyEntries);
      foreach (string value in values)
      {
        T val = (T)ConvertValueByType(value, typeof(T).Name);
        if (val != null)
        {
          list.Add(val);
        }
      }
      return list;
    }

    public static object[] ParseMechData(string data)
    {
      if (string.IsNullOrEmpty(data)) return new object[0];
      var val = new List<object>();
      var param = data.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var p in param)
      {
        var t = p.Split(new string[] { ",", "，" }, StringSplitOptions.RemoveEmptyEntries);
        val.Add(ConvertValueByType(t[1], t[0]));
      }
      return val.ToArray();
    }
  }
}

