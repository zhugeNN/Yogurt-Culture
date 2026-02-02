using System.Collections.Generic;
using UnityEngine;

public class IngredientManager : Singleton<IngredientManager>
{

    [Header("Prefab列表")]
    [Tooltip("可用的配料Prefab列表")]
    [SerializeField] private List<GameObject> ingredientPrefabs = new List<GameObject>();

    [Header("生成位置")]
    [Tooltip("实例化Prefab的目标位置")]
    [SerializeField] private Transform spawnPoint;

    [Header("父节点（可选）")]
    [Tooltip("生成的实例将挂载到此父节点下，留空则挂载到场景根节点")]
    [SerializeField] private Transform parentTransform;

    [Header("IngredientController 默认设置")]
    [Tooltip("默认目标位置（世界坐标）- 将应用到新创建的 IngredientController")]
    [SerializeField] private Vector3 defaultTargetPosition = Vector3.zero;

    [Tooltip("默认目标缩放（等比例，xyz统一）- 将应用到新创建的 IngredientController")]
    [SerializeField] private float defaultTargetScale = 2f;

    [Tooltip("默认动画时间（秒）- 将应用到新创建的 IngredientController")]
    [SerializeField] private float defaultAnimationDuration = 0.5f;

    [Tooltip("默认位置变化动画曲线 - 将应用到新创建的 IngredientController")]
    [SerializeField] private AnimationCurve defaultPositionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("默认缩放变化动画曲线 - 将应用到新创建的 IngredientController")]
    [SerializeField] private AnimationCurve defaultScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private GameObject defaultBowl;

    /// <summary>
    /// 在指定位置创建指定索引的Prefab实例（供按钮调用）
    /// </summary>
    /// <param name="prefabIndex">Prefab列表中的索引</param>
    public void CreateIngredient(int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= ingredientPrefabs.Count)
        {
            // Debug.LogWarning($"IngredientManager: Prefab索引 {prefabIndex} 超出范围（0-{ingredientPrefabs.Count - 1}）。");
            return;
        }

        GameObject prefab = ingredientPrefabs[prefabIndex];
        if (prefab == null)
        {
            // Debug.LogWarning($"IngredientManager: 索引 {prefabIndex} 的Prefab为空。");
            return;
        }

        CreateIngredientInstance(prefab);
    }

    /// <summary>
    /// 在指定位置创建指定名称的Prefab实例（供按钮调用）
    /// </summary>
    /// <param name="prefabName">Prefab的名称</param>
    public void CreateIngredientByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            // Debug.LogWarning("IngredientManager: Prefab名称为空。");
            return;
        }

        GameObject prefab = ingredientPrefabs.Find(p => p != null && p.name == prefabName);
        if (prefab == null)
        {
            // Debug.LogWarning($"IngredientManager: 未找到名为 '{prefabName}' 的Prefab。");
            return;
        }

        CreateIngredientInstance(prefab);
    }

    /// <summary>
    /// 获取Prefab列表
    /// </summary>
    public List<GameObject> GetPrefabList()
    {
        return ingredientPrefabs;
    }

    /// <summary>
    /// 创建配料实例的通用方法
    /// </summary>
    /// <param name="prefab">要实例化的Prefab</param>
    private void CreateIngredientInstance(GameObject prefab)
    {
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Transform parent = parentTransform != null ? parentTransform : transform;

        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
        // Debug.Log($"IngredientManager: 已创建 {prefab.name} 实例在位置 {spawnPosition}。");

        // 设置碰撞检测
        SetupIngredientCollision(instance);
    }

    /// <summary>
    /// 获取默认设置（供 IngredientController 使用）
    /// </summary>
    public DefaultSettings? GetDefaultSettings()
    {
        return new DefaultSettings
        {
            targetPosition = defaultTargetPosition,
            targetScale = defaultTargetScale,
            animationDuration = defaultAnimationDuration,
            positionCurve = defaultPositionCurve,
            scaleCurve = defaultScaleCurve,
            bowl = defaultBowl,
        };
    }

    /// <summary>
    /// 默认设置结构体
    /// </summary>
    public struct DefaultSettings
    {
        public Vector3 targetPosition;
        public float targetScale;
        public float animationDuration;
        public AnimationCurve positionCurve;
        public AnimationCurve scaleCurve;
        public GameObject bowl;
    }


    /// <summary>
    /// 为生成的实例添加碰撞检测组件和 IngredientController
    /// </summary>
    private void SetupIngredientCollision(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        // 检查是否已有 IngredientController 组件
        IngredientController ingredientController = instance.GetComponent<IngredientController>();
        if (ingredientController == null)
        {
            ingredientController = instance.AddComponent<IngredientController>();

            // 应用默认设置
            var defaultSettings = GetDefaultSettings();
            if (defaultSettings.HasValue)
            {
                ingredientController.SetTargetPosition(defaultSettings.Value.targetPosition);
                ingredientController.SetTargetScale(defaultSettings.Value.targetScale);
                ingredientController.SetAnimationSettings(
                    defaultSettings.Value.animationDuration,
                    defaultSettings.Value.positionCurve,
                    defaultSettings.Value.scaleCurve
                );
                ingredientController.SetBowl(defaultSettings.Value.bowl);
            }
        }
        else
        {
            // 如果 IngredientController 已存在，也要设置 bowl
            var defaultSettings = GetDefaultSettings();
            if (defaultSettings.HasValue)
            {
                ingredientController.SetBowl(defaultSettings.Value.bowl);
            }
        }

        // 检查是否已有碰撞检测组件
        IngredientCollisionHandler handler = instance.GetComponent<IngredientCollisionHandler>();
        if (handler == null)
        {
            handler = instance.AddComponent<IngredientCollisionHandler>();
        }

        // 确保有Collider2D组件
        Collider2D col = instance.GetComponent<Collider2D>();
        if (col == null)
        {
            // Debug.LogWarning($"IngredientManager: 实例 {instance.name} 没有Collider2D组件，无法检测碰撞。");
        }
        else
        {
            // 确保Collider2D是触发器
            col.isTrigger = true;
        }
    }
}

/// <summary>
/// 配料碰撞处理器：附加到每个生成的prefab实例上
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IngredientCollisionHandler : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 通知 IngredientController 处理碰撞
        IngredientController ingredientController = GetComponent<IngredientController>();
        if (ingredientController != null)
        {
            ingredientController.OnToolEnter(other);
        }
    }
}

