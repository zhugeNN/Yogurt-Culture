using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 进度控制器：管理和驱动一组加工进度条（Slider）及其对应的 Ingredient（例如酸奶）对象的生命周期。
/// 职责：
/// - 在 Inspector 中配置并实例化 Slider Prefab，维护已加载的 Slider 列表；
/// - 按顺序激活并运行每个 Slider 对应的 Ingredient 的加工逻辑（协同 Ingredient.Initialize / InnerExecute）；
/// - 在所有 Slider 完成后收集加工结果并实例化最终成品（YogurtProduct），同时触发完成事件 `onFinish`；
/// - 提供对外 API：添加 Slider（按索引/名称/枚举/Prefab）、启动/停止进度、刷新与清理已加载的 Slider，以及查询当前正在运行的进度对象。
/// 该类为游戏内加工流程的中心协调者，界面与游戏逻辑可通过其事件与方法进行解耦订阅与控制。
/// </summary>
public class ProgressController : Singleton<ProgressController>
{
    /// <summary>
    /// 静态事件：外部（如 IngredientController）可以通过触发该事件请求开始进度。
    /// ProgressController 会在自身 Awake 时订阅此事件并决定是否真正启动进度（解耦调用方与实现）。
    /// </summary>
    public static System.Action RequestStartProgress;
    /// <summary>
    /// 配料实例创建完成后的回调，用于加载额外效应
    /// </summary>
    public System.Action<Ingredient> OnIngredientCreated;
    [Header("Slider Prefab列表")]
    [Tooltip("在此列表中添加Slider Prefab，添加后点击Inspector中的'生成/更新 Enum类型'按钮")]
    [SerializeField] private List<Slider> sliderPrefabs = new List<Slider>();

    [Header("已加载的Slider列表")]
    [Tooltip("实际加载到场景中的Slider实例列表，可手动添加或通过脚本添加")]
    [SerializeField] private List<Slider> loadedSliders = new List<Slider>();

    [Header("容器设置")]
    [SerializeField] private Transform sliderContainer;
    [SerializeField] private Transform spawnPoint;

    [Header("事件")]
    [SerializeField] private UnityEvent onFinish;

    private int currentSliderIndex = -1;
    private Ingredient currentYogurtProgress;
    private bool isProgressRunning;
    
    /// <summary>
    /// 获取进度是否正在运行（公共属性）
    /// </summary>
    public bool IsProgressRunning => isProgressRunning;
    
    /// <summary>
    /// 获取当前正在运行的Yogurt Progress（供外部调用）
    /// </summary>
    public Ingredient GetCurrentYogurtProgress()
    {
        return currentYogurtProgress;
    }

    protected override void Awake()
    {
        base.Awake();
        if (sliderContainer == null)
        {
            sliderContainer = transform;
        }
        // 订阅静态请求事件：当外部请求开始进度时，由实例决定是否启动
        RequestStartProgress += () =>
        {
            if (Instance != null)
            {
                Instance.StartProgress();
            }
        };
    }

    private void Update()
    {
        if (isProgressRunning && currentYogurtProgress != null)
        {
            currentYogurtProgress.InnerExecute();
        }
    }

    /// <summary>
    /// 根据Prefab索引添加Slider
    /// </summary>
    public Slider AddSliderByIndex(int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= sliderPrefabs.Count)
        {
            // Debug.LogWarning($"ProgressController: Prefab索引 {prefabIndex} 超出范围。");
            return null;
        }

        Slider prefab = sliderPrefabs[prefabIndex];
        if (prefab == null)
        {
            // Debug.LogWarning($"ProgressController: 索引 {prefabIndex} 的Prefab为空。");
            return null;
        }

        return AddSlider(prefab);
    }

    /// <summary>
    /// 根据Prefab名称添加Slider
    /// </summary>
    public Slider AddSliderByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            // Debug.LogWarning("ProgressController: Prefab名称为空。");
            return null;
        }

        Slider prefab = sliderPrefabs.FirstOrDefault(s => s != null && s.name.Replace("(Clone)", "") == prefabName.Replace("(Clone)", ""));
        if (prefab == null)
        {
            // Debug.LogWarning($"ProgressController: 未找到名为 '{prefabName}' 的Prefab。");
            return null;
        }

        return AddSlider(prefab);
    }

    /// <summary>
    /// 根据生成的Enum类型添加Slider（需要先生成Enum）
    /// </summary>
    public Slider AddSliderByEnum(System.Enum operationType)
    {
        string enumName = operationType.ToString();
        return AddSliderByName(enumName);
    }

    /// <summary>
    /// 直接使用Prefab添加Slider
    /// </summary>
    public Slider AddSlider(Slider prefab)
    {
        if (prefab == null)
        {
            // Debug.LogWarning("ProgressController: Prefab为空。");
            return null;
        }

        if (sliderContainer == null)
        {
            sliderContainer = transform;
        }

        Slider newSlider = Instantiate(prefab, sliderContainer);
        newSlider.transform.SetAsLastSibling();
        
        if (!loadedSliders.Contains(newSlider))
        {
            loadedSliders.Add(newSlider);
        }

        newSlider.value = 0;

        Ingredient yogurt = newSlider.GetComponent<Ingredient>();
        if (yogurt != null)
        {
            yogurt.Initialize(this, newSlider);
        }
        else
        {
            // Debug.LogWarning($"ProgressController: {newSlider.name} 上未找到 Yogurt 派生脚本。");
        }

        // 触发回调，便于加载额外效应
        OnIngredientCreated?.Invoke(yogurt);

        return newSlider;
    }

    public void DebugAdd(){
        AddSliderByIndex(0);
    }

    /// <summary>
    /// 刷新所有Slider（清理无效引用，重新排序）
    /// </summary>
    public void RefreshSliders()
    {
        // 清理无效引用
        loadedSliders = loadedSliders.Where(s => s != null && s.gameObject.activeInHierarchy).ToList();

        // 按在场景中的顺序重新排序
        if (sliderContainer != null)
        {
            loadedSliders = sliderContainer.GetComponentsInChildren<Slider>(false).ToList();
        }

        // Debug.Log($"ProgressController: 已刷新，当前有 {loadedSliders.Count} 个Slider。");
    }

    /// <summary>
    /// 移除指定Slider
    /// </summary>
    public void RemoveSlider(Slider slider)
    {
        if (slider == null)
            return;

        if (loadedSliders.Contains(slider))
        {
            loadedSliders.Remove(slider);
        }

        if (slider != null && slider.gameObject != null)
        {
            Destroy(slider.gameObject);
        }
    }

    /// <summary>
    /// 清空所有Slider
    /// </summary>
    public void ClearAllSliders()
    {
        foreach (Slider slider in loadedSliders)
        {
            if (slider != null && slider.gameObject != null)
            {
                Destroy(slider.gameObject);
            }
        }
        loadedSliders.Clear();
    }

    /// <summary>
    /// 获取Prefab列表
    /// </summary>
    public List<Slider> GetPrefabList()
    {
        return sliderPrefabs;
    }

    /// <summary>
    /// 获取已加载的Slider列表
    /// </summary>
    public List<Slider> GetLoadedSliders()
    {
        return loadedSliders;
    }

    public void StartProgress()
    {
        // 如果进度已经在运行，不再重新启动（避免重置当前进度）
        if (isProgressRunning && currentYogurtProgress != null)
        {
            return;
        }

        RefreshSliders();

        if (loadedSliders == null || loadedSliders.Count == 0)
        {
            // Debug.LogWarning("ProgressController: 当前没有可用的 Slider。");
            return;
        }

        currentSliderIndex = loadedSliders.Count;
        isProgressRunning = true;

        if (!TryActivateNextYogurt())
        {
            Finish();
        }
    }

    internal void OnYogurtOperationCompleted(Ingredient yogurt)
    {
        if (yogurt != null && yogurt != currentYogurtProgress)
        {
            return;
        }

        if (!TryActivateNextYogurt())
        {
            Finish();
        }
    }

    private bool TryActivateNextYogurt()
    {
        if (loadedSliders == null || loadedSliders.Count == 0)
        {
            return false;
        }

        for (int i = currentSliderIndex - 1; i >= 0; i--)
        {
            Slider slider = loadedSliders[i];
            if (slider == null)
            {
                continue;
            }

            Ingredient yogurt = slider.GetComponent<Ingredient>();
            if (yogurt == null)
            {
                // Debug.LogWarning($"ProgressController: Slider {slider.name} 未挂载 Yogurt 脚本，跳过。");
                continue;
            }

            // 如果找到的 yogurt 就是当前的 currentYogurtProgress，不重置进度
            bool isCurrentYogurt = yogurt == currentYogurtProgress;

            currentSliderIndex = i;
            currentYogurtProgress = yogurt;
            currentYogurtProgress.Initialize(this, slider);
            
            // 只有在不是当前正在运行的 yogurt 时才重置进度
            if (!isCurrentYogurt)
            {
                currentYogurtProgress.ResetOperation();
            }
            
            return true;
        }

        // currentYogurtProgress = null;
        return false;
    }

    public void Finish()
    {
        if (!isProgressRunning)
        {
            return;
        }

        isProgressRunning = false;

        // 收集所有Slider上的 ingredient 实例
        List<Ingredient> completedIngredients = new List<Ingredient>();
        foreach (Slider slider in loadedSliders)
        {
            if (slider == null)
            {
                continue;
            }

            Ingredient ingredient = slider.GetComponent<Ingredient>();
            if (ingredient != null)
            {
                completedIngredients.Add(ingredient);
            }
        }

        // 实例化最终成品（例如 ShopItem）
        if (currentYogurtProgress != null && currentYogurtProgress.prefab != null)
        {
            GameObject yogurtPrefab = Instantiate(currentYogurtProgress.prefab, spawnPoint ?? sliderContainer);
            yogurtPrefab.transform.localPosition = Vector3.zero;
            YogurtProduct shopItem = yogurtPrefab.GetComponent<YogurtProduct>();
            if (shopItem != null)
            {
                shopItem.SetIngredients(new List<Ingredient>(completedIngredients));
                // 将当前 Ingredient 的 flavor 写入生成的成品
                shopItem.SetFlavor(currentYogurtProgress != null ? currentYogurtProgress.Flavor : 0f);
                // Debug: 展示当前 Ingredient 的 flavor 值
                Debug.Log($"[ProgressController] Generated product flavor: {currentYogurtProgress?.Flavor}");
            }
        }

        currentYogurtProgress = null;
        currentSliderIndex = -1;

        // 清除所有当前活跃的 slider，恢复到 StartProgress 之前的状态
        ClearAllSliders();

        // Debug.Log("ProgressController: 全部Slider已完成，触发 Win。");
        onFinish?.Invoke();
    }
}

