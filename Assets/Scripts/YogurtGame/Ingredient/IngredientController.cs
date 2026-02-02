using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配料控制器：管理单个配料的交互生命周期与加工流程。
/// 主要职责（已按单一职责分层）：
/// - 负责工具触发的放大/缩小动画与过渡（UI/交互层）；
/// - 管理并显示该配料下的 Topping（加载、显示、隐藏与本地清理）；
/// - 管理光标限制与交互范围，供外部工具（如 StickController）使用；
/// - 发出“请求开始加工”的事件（通过 ProgressController.RequestStartProgress），而不直接启动加工流程（将决策权交给 ProgressController）。
/// 
/// 注：ProgressController 负责真正的进度流程启动/驱动，IngredientController 仅负责触发与注册回调（解耦）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IngredientController : MonoBehaviour
{
    [Header("动画设置")]
    [Tooltip("触发效果时的目标位置（世界坐标）")]
    [SerializeField] private Vector3 targetPosition = Vector3.zero;

    [Tooltip("触发效果时的目标缩放（等比例，xyz统一）")]
    [SerializeField] private float targetScale = 2f;

    [Tooltip("位置变化动画时间（秒）")]
    [SerializeField] private float animationDuration = 0.5f;

    [Tooltip("位置变化动画曲线")]
    [SerializeField] private AnimationCurve positionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("缩放变化动画曲线")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("是否使用 IngredientManager 的默认设置")]
    [SerializeField] private bool useDefaultSettings = true;

    [Header("工具交互设置")]
    [Tooltip("工具Layer名称")]
    [SerializeField] private string toolLayerName = "tool";

    [Header("Topping管理")]
    [Tooltip("Topping数据列表（用于首次放大时加载）")]
    [SerializeField] private List<ToppingData> toppingDataList = new List<ToppingData>();

    [Header("容器设置")]
    [Tooltip("关联的容器对象")]
    [SerializeField] private GameObject bowl;

    // 初始状态
    private Vector3 initialPosition;
    private float initialScaleValue;
    private bool hasTriggered = false;

    // bowl 初始状态
    private Vector3 bowlInitialPosition;
    private float bowlInitialScaleValue;
    private bool isAnimating = false;
    private bool isExpanded = false; // 是否处于放大状态

    // 工具对象引用（用于检查拖动状态）
    private StickController toolStickController;
    
    // 当前运行的动画协程
    private Coroutine currentAnimationCoroutine;
    
    // 上一帧的拖动状态
    private bool wasDragging = false;
    
    // 范围限制相关
    private Collider2D ingredientCollider;
    private Bounds ingredientBounds;
    private bool isRestrictingCursor = false;
    
    // 进度完成回调
    private System.Action onProgressComplete;
    
    // Topping管理
    private List<Topping> toppings = new List<Topping>();
    private bool toppingsLoaded = false;

    private void Start()
    {
        // 记录初始状态
        initialPosition = transform.position;
        Vector3 initialScale = transform.localScale;
        initialScaleValue = (initialScale.x + initialScale.y + initialScale.z) / 3f;

        // 记录 bowl 初始状态
        if (bowl != null)
        {
            bowlInitialPosition = bowl.transform.position;
            Vector3 bowlInitialScale = bowl.transform.localScale;
            bowlInitialScaleValue = (bowlInitialScale.x + bowlInitialScale.y + bowlInitialScale.z) / 3f;
        }

        // 获取Collider2D组件
        ingredientCollider = GetComponent<Collider2D>();
        if (ingredientCollider == null)
        {
            // Debug.LogWarning("IngredientController: 未找到 Collider2D 组件，无法计算范围。");
        }

        // 如果启用使用默认设置，从 IngredientManager 获取默认值
        if (useDefaultSettings && IngredientManager.Instance != null)
        {
            ApplyDefaultSettings();
        }

        // 查找工具对象（StickController）
        toolStickController = FindObjectOfType<StickController>();
        if (toolStickController == null)
        {
            // Debug.LogWarning("IngredientController: 未找到 StickController，工具交互功能可能无法正常工作。");
        }
    }

    private void Update()
    {
        // 实时检测拖动状态
        if (toolStickController == null)
        {
            return;
        }

        bool isDragging = toolStickController.IsDragging();
        
        // 当ingredient处于放大状态时，松开鼠标，触发反向动画
        // 注意：如果进度正在进行中，不应该触发反向动画（等待进度完成）
        if (isExpanded && !isDragging && wasDragging)
        {
            TriggerReverseEffect();
        }
        
        // 更新上一帧的拖动状态
        wasDragging = isDragging;
    }

    /// <summary>
    /// 应用 IngredientManager 的默认设置
    /// </summary>
    private void ApplyDefaultSettings()
    {
        // 通过反射或公共方法获取默认值
        // 由于 IngredientManager 的默认值是私有的，我们需要添加公共方法
        if (IngredientManager.Instance != null)
        {
            var defaultSettings = IngredientManager.Instance.GetDefaultSettings();
            if (defaultSettings != null)
            {
                targetPosition = defaultSettings.Value.targetPosition;
                targetScale = defaultSettings.Value.targetScale;
                animationDuration = defaultSettings.Value.animationDuration;
                positionCurve = defaultSettings.Value.positionCurve;
                scaleCurve = defaultSettings.Value.scaleCurve;
                SetBowl(defaultSettings.Value.bowl);
            }
        }
    }

    /// <summary>
    /// 处理工具碰撞进入事件（由 IngredientCollisionHandler 调用）
    /// </summary>
    public void OnToolEnter(Collider2D toolCollider)
    {
        if (toolCollider == null)
        {
            return;
        }

        // 检查碰撞对象的Layer是否为tool
        if (toolCollider.gameObject.layer != LayerMask.NameToLayer(toolLayerName))
        {
            return;
        }

        // 检查StickController是否处于拖动状态
        if (toolStickController == null || !toolStickController.IsDragging())
        {
            return;
        }

        // 触发效果
        TriggerEffect();
    }

    /// <summary>
    /// 触发效果：改变prefab的scale与position
    /// </summary>
    private void TriggerEffect()
    {
        if (hasTriggered && isExpanded)
        {
            return; // 避免重复触发
        }

        // 如果正在反向动画，先停止它
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        hasTriggered = true;
        isAnimating = true;
        isExpanded = true;

        // 启动动画协程
        currentAnimationCoroutine = StartCoroutine(AnimateIngredient(true));
    }

    /// <summary>
    /// 触发反向效果：返回初始状态
    /// </summary>
    private void TriggerReverseEffect()
    {
        if (!isExpanded)
        {
            return; // 如果不在放大状态，不需要反向
        }

        // 如果正在动画，先停止它
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        isAnimating = true;
        isExpanded = false;

        // 启动反向动画协程
        currentAnimationCoroutine = StartCoroutine(AnimateIngredient(false));
    }

    /// <summary>
    /// 动画协程：改变位置和缩放
    /// </summary>
    /// <param name="forward">true 为放大动画，false 为缩小动画</param>
    private IEnumerator AnimateIngredient(bool forward)
    {
        float elapsedTime = 0f;

        // 确定起始和结束状态
        // 对于反向动画，从当前位置开始（可能动画被中断）
        Vector3 startPosition = forward ? initialPosition : transform.position;
        Vector3 endPosition = forward ? targetPosition : initialPosition;
        float startScale = forward ? initialScaleValue : transform.localScale.x; // 等比例缩放，取x即可
        float endScale = forward ? targetScale : initialScaleValue;

        // bowl 的动画状态（如果存在）
        Vector3 bowlStartPosition = forward ? bowlInitialPosition : (bowl != null ? bowl.transform.position : Vector3.zero);
        Vector3 bowlEndPosition = forward ? targetPosition : bowlInitialPosition; // bowl 使用相同的目标位置
        float bowlStartScale = forward ? bowlInitialScaleValue : (bowl != null ? bowl.transform.localScale.x : 1f);
        float bowlEndScale = forward ? targetScale : bowlInitialScaleValue; // bowl 使用相同的目标缩放

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / animationDuration);

            // 使用曲线计算插值（复用相同的动画曲线）
            float positionT = positionCurve.Evaluate(normalizedTime);
            float scaleT = scaleCurve.Evaluate(normalizedTime);

            // 插值位置和缩放 - 本体
            transform.position = Vector3.Lerp(startPosition, endPosition, positionT);
            float currentScale = Mathf.Lerp(startScale, endScale, scaleT);
            transform.localScale = Vector3.one * currentScale;

            // 插值位置和缩放 - bowl（如果存在）
            if (bowl != null)
            {
                bowl.transform.position = Vector3.Lerp(bowlStartPosition, bowlEndPosition, positionT);
                float bowlCurrentScale = Mathf.Lerp(bowlStartScale, bowlEndScale, scaleT);
                bowl.transform.localScale = Vector3.one * bowlCurrentScale;
            }

            yield return null;
        }

        // 确保最终状态精确 - 本体
        transform.position = endPosition;
        transform.localScale = Vector3.one * endScale;

        // 确保最终状态精确 - bowl（如果存在）
        if (bowl != null)
        {
            bowl.transform.position = bowlEndPosition;
            bowl.transform.localScale = Vector3.one * bowlEndScale;
        }

        isAnimating = false;
        currentAnimationCoroutine = null;

        // 如果是放大动画完成，调用 ProgressController 的开始进程方法
        if (forward)
        {
            OnAnimationComplete();
        }
        else
        {
            // 反向动画完成，重置触发状态（允许再次触发）
            hasTriggered = false;
            // 停止光标限制
            StopCursorRestriction();
            // 隐藏Topping
            HideToppings();
        }
    }

    /// <summary>
    /// 动画完成回调
    /// </summary>
    private void OnAnimationComplete()
    {
        // 显示Topping（首次放大时加载）
        ShowToppings();
        
        // 开始光标限制
        StartCursorRestriction();
        
        // 注册进度完成回调并请求进度开始（通过事件解耦）
        RegisterProgressCompleteCallback();
        ProgressController.RequestStartProgress?.Invoke();
    }
    #region topping
    /// <summary>
    /// 显示Topping（由动画完成时调用）
    /// </summary>
    private void ShowToppings()
    {
        // 首次放大时加载Topping
        if (!toppingsLoaded)
        {
            LoadToppings();
            toppingsLoaded = true;
        }
        
        // 创建列表副本，避免在遍历时修改集合导致的异常
        List<Topping> toppingsCopy = new List<Topping>(toppings);

        // 显示所有Topping（传入当前 IngredientController 实例，供 Topping 初始化时使用）
        foreach (var topping in toppingsCopy)
        {
            if (topping != null)
            {
                topping.Show(this);
            }
        }
    }
    
    /// <summary>
    /// 隐藏Topping（由反向动画完成时调用）
    /// </summary>
    private void HideToppings()
    {
        // 创建列表副本，避免在遍历时修改集合导致的异常
        List<Topping> toppingsCopy = new List<Topping>(toppings);
        
        // 隐藏所有Topping
        foreach (var topping in toppingsCopy)
        {
            if (topping != null)
            {
                topping.Hide();
            }
        }
    }
    
    /// <summary>
    /// 添加Topping（由Topping调用）
    /// </summary>
    public void AddTopping(Topping topping)
    {
        if (topping == null || toppings.Contains(topping))
        {
            return;
        }
        
        toppings.Add(topping);
        
        // 将Topping设置为Ingredient的子对象
        topping.transform.SetParent(transform);
        
        // 如果当前是放大状态，显示Topping（传入当前 IngredientController 实例）；否则隐藏
        if (isExpanded)
        {
            topping.Show(this);
        }
        else
        {
            topping.Hide();
        }
        
    }
    
    /// <summary>
    /// 移除Topping
    /// </summary>
    public void RemoveTopping(Topping topping)
    {
        if (topping != null && toppings.Contains(topping))
        {
            toppings.Remove(topping);
        }
    }
    
    /// <summary>
    /// 获取所有Topping
    /// </summary>
    public List<Topping> GetToppings()
    {
        return new List<Topping>(toppings);
    }
    
    /// <summary>
    /// 加载Topping（首次放大时调用）
    /// </summary>
    private void LoadToppings()
    {
        if (toppingDataList == null || toppingDataList.Count == 0)
        {
            return;
        }
        
        foreach (var data in toppingDataList)
        {
            if (data.toppingPrefab == null)
            {
                continue;
            }
            
            // 实例化Topping
            GameObject toppingObj = Instantiate(data.toppingPrefab, transform);
            toppingObj.transform.localPosition = data.localPosition;
            toppingObj.transform.localRotation = data.localRotation;
            toppingObj.transform.localScale = data.localScale;
            
            // 获取Topping组件
            Topping topping = toppingObj.GetComponent<Topping>();
            if (topping != null)
            {
                toppings.Add(topping);
                
                // 调用Topping的加载方法
                topping.LoadTopping();
                
                // 初始状态隐藏（等待放大时显示）
                topping.Hide();
            }
        }
    }
    
    /// <summary>
    /// 设置Topping数据列表（外部调用）
    /// </summary>
    public void SetToppingDataList(List<ToppingData> dataList)
    {
        toppingDataList = dataList != null ? new List<ToppingData>(dataList) : new List<ToppingData>();
    }
    
    /// <summary>
    /// 获取Topping数据列表（外部调用）
    /// </summary>
    public List<ToppingData> GetToppingDataList()
    {
        return new List<ToppingData>(toppingDataList);
    }
    
    /// <summary>
    /// Topping数据类
    /// </summary>
    [System.Serializable]
    public class ToppingData
    {
        [Tooltip("Topping Prefab")]
        public GameObject toppingPrefab;
        
        [Tooltip("Topping类型（用于识别）")]
        public string toppingType;
        
        [Tooltip("初始位置（相对于Ingredient）")]
        public Vector3 localPosition;
        
        [Tooltip("初始旋转")]
        public Quaternion localRotation = Quaternion.identity;
        
        [Tooltip("初始缩放")]
        public Vector3 localScale = Vector3.one;
    }
    #endregion
    /// <summary>
    /// 开始光标限制
    /// </summary>
    private void StartCursorRestriction()
    {
        if (ingredientCollider == null)
        {
            return;
        }

        // 计算放大后的范围
        ingredientBounds = ingredientCollider.bounds;
        isRestrictingCursor = true;

        // 通知 StickController 开始限制光标
        if (toolStickController != null)
        {
            toolStickController.SetCursorRestriction(this, ingredientBounds);
        }
    }

    /// <summary>
    /// 停止光标限制
    /// </summary>
    private void StopCursorRestriction()
    {
        isRestrictingCursor = false;

        // 通知 StickController 停止限制光标
        if (toolStickController != null)
        {
            toolStickController.ClearCursorRestriction(this);
        }
    }

    /// <summary>
    /// 检查鼠标位置是否在范围内
    /// </summary>
    public bool IsMouseInBounds(Vector3 mouseWorldPosition)
    {
        if (!isRestrictingCursor || ingredientCollider == null)
        {
            return false;
        }

        // 根据 Collider 类型进行不同的检查
        if (ingredientCollider is CircleCollider2D circleCollider)
        {
            // 圆形范围检查
            Vector2 center = (Vector2)circleCollider.bounds.center;
            // 使用 lossyScale 的最大值来缩放半径（等比例缩放）
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            float radius = circleCollider.radius * scale;
            Vector2 mousePos2D = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
            float distance = Vector2.Distance(mousePos2D, center);
            return distance <= radius;
        }
        else if (ingredientCollider is BoxCollider2D)
        {
            // 矩形范围检查
            ingredientBounds = ingredientCollider.bounds;
            return ingredientBounds.Contains(mouseWorldPosition);
        }
        else
        {
            // 默认使用 bounds 检查
            ingredientBounds = ingredientCollider.bounds;
            return ingredientBounds.Contains(mouseWorldPosition);
        }
    }

    /// <summary>
    /// 注册进度完成回调
    /// </summary>
    private void RegisterProgressCompleteCallback()
    {
        // 通过协程监听进度完成
        StartCoroutine(WaitForProgressComplete());
    }

    /// <summary>
    /// 等待进度完成
    /// </summary>
    private IEnumerator WaitForProgressComplete()
    {
        // 等待一小段时间，确保进度真的开始了
        yield return new WaitForSeconds(0.2f);
        
        // 检查进度是否真的开始了
        if (ProgressController.Instance == null || !ProgressController.Instance.IsProgressRunning)
        {
            // 如果进度没有开始，停止光标限制，允许用户松开鼠标时触发反向动画
            StopCursorRestriction();
            yield break;
        }
        
        // 等待进度完成
        while (ProgressController.Instance != null && ProgressController.Instance.IsProgressRunning)
        {
            yield return null;
        }

        // 再次等待一小段时间，确保进度真的完成了
        yield return new WaitForSeconds(0.1f);

        // 进度完成，执行清理
        OnProgressComplete();
    }

    /// <summary>
    /// 进度完成回调
    /// </summary>
    private void OnProgressComplete()
    {
        // 停止光标限制
        StopCursorRestriction();

        // 让 StickController 回到原始位置
        if (toolStickController != null)
        {
            toolStickController.ResetToInitial();
        }

        // 如果有 bowl，让 bowl 执行缩小动画回到初始状态
        if (bowl != null)
        {
            // 将 bowl 从当前父对象分离，使其独立存在
            bowl.transform.SetParent(null);

            // 为 bowl 添加自动缩小和销毁组件
            BowlShrinkController shrinkController = bowl.AddComponent<BowlShrinkController>();
            shrinkController.Initialize(bowlInitialPosition, bowlInitialScaleValue, animationDuration, positionCurve, scaleCurve);
        }

        // 销毁 ingredient prefab（但 bowl 已经分离，不会受到影响）
        Destroy(gameObject);
    }

    /// <summary>
    /// 设置目标位置（外部调用）
    /// </summary>
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
    }

    /// <summary>
    /// 设置目标缩放（外部调用）
    /// </summary>
    public void SetTargetScale(float scale)
    {
        targetScale = scale;
    }

    /// <summary>
    /// 获取是否已触发
    /// </summary>
    public bool HasTriggered()
    {
        return hasTriggered;
    }

    /// <summary>
    /// 获取是否正在动画中
    /// </summary>
    public bool IsAnimating()
    {
        return isAnimating;
    }

    /// <summary>
    /// 设置动画曲线和时间（外部调用）
    /// </summary>
    public void SetAnimationSettings(float duration, AnimationCurve posCurve, AnimationCurve sclCurve)
    {
        animationDuration = duration;
        positionCurve = posCurve;
        scaleCurve = sclCurve;
    }

    /// <summary>
    /// 设置容器对象（外部调用）
    /// </summary>
    public void SetBowl(GameObject bowlObject)
    {
        bowl = bowlObject;
    }
}

