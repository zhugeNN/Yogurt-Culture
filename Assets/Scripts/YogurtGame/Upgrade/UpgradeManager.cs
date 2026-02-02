using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity;

/// <summary>
/// 升级管理器，负责管理所有可升级物品的查找和操作
/// </summary>
public class UpgradeManager : Singleton<UpgradeManager>
{
    /// <summary>
    /// 类型查找缓存，提高查找性能
    /// </summary>
    private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

    /// <summary>
    /// 升级物品实例缓存
    /// </summary>
    private static readonly Dictionary<string, IUpgradeItem> upgradeItemCache = new Dictionary<string, IUpgradeItem>();

    /// <summary>
    /// 升级数据缓存
    /// </summary>
    private static Dictionary<string, UpgradeData> upgradeDataCache;

    /// <summary>
    /// 升级数据结构
    /// </summary>
    [Serializable]
    public class UpgradeData
    {
        public string ID;
        public string Description;
        public float Price;
        public string MethodName;
        public string Params;
    }

    protected override void Awake()
    {
        base.Awake();
        InitializeTypeCache();
    }

    /// <summary>
    /// 初始化类型缓存
    /// </summary>
    private void InitializeTypeCache()
    {
        Assembly assembly = typeof(UpgradeManager).Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (typeof(IUpgradeItem).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
                string typeName = type.Name;
                if (!typeCache.ContainsKey(typeName))
                {
                    typeCache[typeName] = type;
                }
            }
        }
    }

    /// <summary>
    /// 根据类名查找并创建升级物品实例，然后执行升级操作
    /// </summary>
    /// <param name="upgradeItemName">升级物品类名</param>
    /// <param name="operationData">操作数据（预留参数，可用于传递升级参数）</param>
    /// <returns>操作是否成功</returns>
    public bool ExecuteUpgrade(string upgradeItemName, string operationData = null)
    {
        // 1. 检查并加载升级数据
        if (upgradeDataCache == null)
        {
            LoadUpgradeData();
        }

        // 2. 获取升级数据
        if (!upgradeDataCache.TryGetValue(upgradeItemName, out UpgradeData upgradeData))
        {
            Debug.LogError($"找不到升级数据: {upgradeItemName}");
            return false;
        }

        // 3. 尝试消费价格
        if (EconomyManager.Instance != null)
        {
            if (!EconomyManager.Instance.TrySpend(upgradeData.Price))
            {
                Debug.LogError($"金钱不足，无法购买升级: {upgradeItemName} (需要 {upgradeData.Price})");
                return false;
            }
        }

        // 4. 执行升级
        IUpgradeItem upgradeItem = GetOrCreateUpgradeItem(upgradeItemName);
        if (upgradeItem == null)
        {
            Debug.LogError($"找不到升级物品: {upgradeItemName}");
            return false;
        }

        upgradeItem.Upgrade(operationData ?? upgradeData.Params);
        Debug.Log($"成功升级: {upgradeItemName} (花费 {upgradeData.Price})");
        return true;
    }

    public void DebugExecute(){
        ExecuteUpgrade("StirBar");
    }

    /// <summary>
    /// 获取或创建升级物品实例
    /// </summary>
    private IUpgradeItem GetOrCreateUpgradeItem(string upgradeItemName)
    {
        if (upgradeItemCache.TryGetValue(upgradeItemName, out IUpgradeItem cachedItem))
        {
            return cachedItem;
        }

        if (!typeCache.TryGetValue(upgradeItemName, out Type upgradeType))
        {
            upgradeType = FindTypeByName(upgradeItemName);
            if (upgradeType != null && !typeCache.ContainsKey(upgradeItemName))
            {
                typeCache[upgradeItemName] = upgradeType;
            }
        }

        if (upgradeType == null)
        {
            return null;
        }

        IUpgradeItem newItem = (IUpgradeItem)Activator.CreateInstance(upgradeType);
        upgradeItemCache[upgradeItemName] = newItem;
        return newItem;
    }

    /// <summary>
    /// 通过名称查找类型
    /// </summary>
    private Type FindTypeByName(string typeName)
    {
        Assembly assembly = typeof(UpgradeManager).Assembly;

        Type type = assembly.GetType(typeName);
        if (type != null && typeof(IUpgradeItem).IsAssignableFrom(type))
        {
            Debug.Log("hit");
            return type;
        }

        string fullName = $"YogurtGame.Upgrade.{typeName}";
        type = assembly.GetType(fullName);
        if (type != null && typeof(IUpgradeItem).IsAssignableFrom(type))
        {
            return type;
        }

        foreach (Type t in assembly.GetTypes())
        {
            if (t.Name == typeName && typeof(IUpgradeItem).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// 加载升级数据
    /// </summary>
    private void LoadUpgradeData()
    {
        upgradeDataCache = new Dictionary<string, UpgradeData>();

        try
        {
            // 从Resources加载JSON文件
            TextAsset jsonFile = Resources.Load<TextAsset>("DataTable/JsonData/UpgradeData");
            if (jsonFile == null)
            {
                Debug.LogError("找不到升级数据文件: UpgradeData.json");
                return;
            }

            // 解析JSON - 由于JSON是数组格式，需要包装一下
            string jsonContent = "{\"items\":" + jsonFile.text + "}";
            UpgradeDataWrapper wrapper = JsonUtility.FromJson<UpgradeDataWrapper>(jsonContent);
            UpgradeData[] upgradeArray = wrapper.items;

            // 构建字典
            foreach (UpgradeData data in upgradeArray)
            {
                if (!upgradeDataCache.ContainsKey(data.ID))
                {
                    upgradeDataCache[data.ID] = data;
                }
            }

            Debug.Log($"成功加载 {upgradeDataCache.Count} 个升级数据项");
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载升级数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// JSON包装器类
    /// </summary>
    [Serializable]
    private class UpgradeDataWrapper
    {
        public UpgradeData[] items;
    }
}
