using System;
using System.Collections.Generic;
using System.Reflection;

namespace Excel2Unity
{

  public static class Excel2UnityUtils
  {
    public static void Log(string Message)
    {
      UnityEngine.Debug.Log($"Excel2Unity : {Message}");
    }
    public static void LogWarning(string Message)
    {
      UnityEngine.Debug.LogWarning($"Excel2Unity : {Message}");
    }
    public static void LogError(string Message)
    {
      UnityEngine.Debug.LogError($"Excel2Unity : {Message}");
    }
    // 创建一个字典来存储常见类型的名称和对应的Type对象
    private static readonly Dictionary<string, Type> commonTypes = new Dictionary<string, Type>
    {
        { "int", typeof(int) },
        { "string", typeof(string) },
        { "float", typeof(float) },
        { "double", typeof(double) },
        { "bool", typeof(bool) },
        // todo:添加更多常用类型
    };
    private static Assembly MainAssembly = Assembly.Load("Assembly-CSharp");
    public static Type TryGetType(string typeName)
    {
      // 首先尝试从字典中获取类型
      if (commonTypes.TryGetValue(typeName.ToLower(), out Type type))
      {
        return type;
      }
      try
      {
        type = MainAssembly.GetType(typeName, true, true);
        if (type == null)
        {
          type = Type.GetType(typeName, true, true);
        }
      }
      catch (Exception ex)
      {
        Excel2UnityUtils.LogWarning("无法解析数据类型:" + typeName + "\n" + ex.Message);
        return null;
      }
      if (type == null)
        Excel2UnityUtils.LogWarning("无法解析数据类型:" + typeName);
      return type;
    }
  }
}