using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Excel2Unity
{

  public static class DataTableUtils
  {
    /// <summary>
    /// 根据类型加载对于的Json数据，并返回字典形式的数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Dictionary<string, T> DeserializeJsonData<T>() where T : TableDataBase
    {
      Dictionary<string, T> result = new Dictionary<string, T>();
      // 如果未查找到配置，则默认json的名称与类名相同
      string jsonName = typeof(T).Name;
      var config = XmlConfigLoader.LoadConfigByClassName(typeof(T).Name);
      if (config != null)
        jsonName = Path.GetFileNameWithoutExtension(config.JsonPath);
      // string jsonPath = ExcelConfig.JsonDataPath + "/" + jsonName + ".json";
      // if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
      // {
      //   jsonPath = jsonPath.Replace('/', '\\');
      // }
      //string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
      // 从Resources文件夹加载JSON文件
      TextAsset textAsset = Resources.Load<TextAsset>(ExcelConfig.jsonDataResourcesLoadPath + "/" + jsonName);
      if (textAsset == null)
      {
        Debug.LogError("Failed to load JSON from Resources folder.");
        return null;
      }
      string jsonContent = textAsset.text;
      try
      {
        List<T> classInfo = JsonConvert.DeserializeObject<List<T>>(jsonContent);
        Dictionary<string, T> idclassDic = classInfo.ToDictionary(classInfo => classInfo.ID, classInfo => classInfo);
        return idclassDic;
      }
      catch (Exception e)
      {
        Excel2UnityUtils.LogWarning(e.Message);
        return null;
      }
    }
    public static Dictionary<Type, Dictionary<string, object>> JsonDataCache = new Dictionary<Type, Dictionary<string, object>>();
    /// <summary>
    /// 公开的数据表读表接口，实现了数据缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    public static T LoadDataTable<T>(string id) where T : TableDataBase
    {
      if (string.IsNullOrEmpty(id))
      {
        Excel2UnityUtils.Log($"无效的数据表ID查询！数据表类型：{typeof(T).Name}");
        return null;
      }
      //先查找数据表缓存，再尝试loadJson
      JsonDataCache.TryGetValue(typeof(T), out var dic);
      if (dic == null)
      {
        var Tdic = DataTableUtils.DeserializeJsonData<T>();
        if (Tdic == null)
        {
          Excel2UnityUtils.LogError($"无法加载json数据{typeof(T).Name} ID:{id},获取jsonData失败");
          return null;
        }
        var newDic = new Dictionary<string, object>();
        foreach (var item in Tdic)
          newDic.Add(item.Key, item.Value);
        JsonDataCache.Add(typeof(T), newDic);
        dic = newDic;
      }
      if (dic != null && dic.TryGetValue(id, out var e))
        return (T)e;
      Excel2UnityUtils.LogWarning($"无法找到数据表{typeof(T).Name} ID:{id},获取jsonData失败");
      return null;
    }
  }
}