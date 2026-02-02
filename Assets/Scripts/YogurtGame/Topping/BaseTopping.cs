using UnityEngine;
using System.Reflection;

/// <summary>
/// 基础Topping实现示例
/// 继承Topping基类，实现LoadTopping虚函数
/// </summary>
public class BaseTopping : Topping
{
    [Header("Topping特定设置")]
    [Tooltip("Topping的显示名称")]
    [SerializeField] private string toppingName = "Base Topping";
    
    [Tooltip("Topping的数值（例如：甜度、数量等）")]
    [SerializeField] private float toppingValue = 1f;
    
    [Header("复制设置")]
    [Tooltip("首次Show时生成的复制数量（原+复制=总数）")]
    [SerializeField] private int cloneCount = 2;

    [SerializeField] private float maxDist = 0.5f;
    
    [Header("物理设置")]
    [Tooltip("径向力系数（力正比于速度差值）")]
    [SerializeField] private float radialForceCoefficient = 1f;
    
    [Tooltip("法向力系数（力正比于旋转速度和距离）")]
    [SerializeField] private float normalForceCoefficient = 1f;
    
    [Header("融化设置")]
    [Tooltip("融化系数：用于将周长与速度映射为自然融化量")]
    [SerializeField] private float meltCoefficient = 0.01f;
    [Tooltip("将融化量映射到缩放变化的因子（currentMelt * meltToScaleFactor = scale减少量）")]
    [SerializeField] private float meltToScaleFactor = 0.01f;
    [Tooltip("当局部缩放任一轴小于此阈值时销毁该 topping")]
    [SerializeField] private float minScaleThreshold = 0.1f;
    [Tooltip("鼠标滑动融化因子（mouseDistance * mouseMeltFactor = mouseMelt）")]
    [SerializeField] private float mouseMeltFactor = 1f;
    [Tooltip("鼠标对融化的最小贡献（当鼠标进入或滑动时的最小融化量）")]
    [SerializeField] private float minMouseMelt = 0.2f;

    // 鼠标位置记录（用于后续计算滑动导致的融化量）
    private Vector3 lastMouseWorldPosition = Vector3.zero;
    private Vector3 currentMouseWorldPosition = Vector3.zero;
    
    // 是否是复制体（复制体不会触发第一次调用的效果）
    private bool isClone = false;
    
    // 物理相关
    private Rigidbody2D rb;
    // private IngredientController ingredientController;
    private IngredientController ingredient;
    private Transform ingredientTransform;
    private Vector3 center;
    private bool physicsEnabled = false;
    
    // 用于获取angularVelocity的反射字段和目标对象
    private FieldInfo angularVelocityField;
    private Ingredient currentYogurtProgress; // 保存当前Yogurt Progress引用
    // 缓存自身的 Collider2D 以供快速检测
    private Collider2D cachedCollider;
    
    protected override void Awake()
    {
        base.Awake();
        
        // 获取Rigidbody2D组件
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        // 缓存自身 Collider2D，避免每次调用 GetComponent
        cachedCollider = GetComponent<Collider2D>();
        
        // 初始状态禁用物理
        rb.isKinematic = true;
    }
    
    /// <summary>
    /// 第一次调用Show时的效果回调函数（重写以实现复制和定位效果）
    /// </summary>
    protected override void OnFirstShow(IngredientController ingredientController)
    {
        // 如果是复制体，不执行首次Show的效果
        if (isClone)
        {
            return;
        }
        // 在首次显示时，使用传入的 IngredientController 设置所属引用（由调用方传入）
        if (ingredientController != null)
        {
            SetIngredient(ingredientController);
        }

        // 调用基类方法，设置scale等基础效果
        base.OnFirstShow(ingredientController);
        
        // 随机调整当前 Topping 的尺寸（70% - 130%）
        RandomScale();
        // 创建复制并重新定位
        CreateClonesAndReposition();
        
        // 启用物理效果
        EnablePhysics();
    }
    
    /// <summary>
    /// 设置Ingredient引用（由IngredientController调用）
    /// </summary>
    public void SetIngredient(IngredientController ingredientComponent)
    {
        ingredient = ingredientComponent;

        // 获取IngredientController和Transform
        if (ingredient == null)
        {
            return;
        }

        ingredientTransform = ingredient.transform;

        // 优先尝试从 ProgressController 获取当前正在进行的 Ingredient（若已初始化）
        if (ProgressController.Instance != null)
        {
            currentYogurtProgress = ProgressController.Instance.GetCurrentYogurtProgress();
        }

        // 如果 ProgressController 尚未设置 currentYogurtProgress，回退为使用传入的 IngredientController 所在的 Ingredient 组件
        if (currentYogurtProgress == null)
        {
            Ingredient fallbackIngredient = ingredientComponent.GetComponent<Ingredient>();
            if (fallbackIngredient != null)
            {
                currentYogurtProgress = fallbackIngredient;
            }
        }

        // 如果找到了具体的 Ingredient 实例，尝试建立反射（仅对 NormalYogurt 有效）
        if (currentYogurtProgress != null && currentYogurtProgress is NormalYogurt)
        {
            angularVelocityField = typeof(NormalYogurt).GetField("angularVelocity",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
    
    /// <summary>
    /// 启用物理效果（公共方法，供复制体调用）
    /// </summary>
    public void EnablePhysics()
    {
        // 确保ingredient已经设置
        if (ingredient == null || ingredientTransform == null)
        {
            return;
        }
        
        center = ingredientTransform.position;
        
        // 启用物理
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.bodyType  = RigidbodyType2D.Dynamic;
            physicsEnabled = true;
        }
    }
    
    private void FixedUpdate()
    {
        if (!physicsEnabled || rb == null || ingredientTransform == null)
        {
            return;
        }
        
        // 每帧计算融化量（自然融化 + 鼠标滑动影响）
        CalcMelt();
        
        // Debug.Log("not return");
        
        // 更新中心位置
        center = ingredientTransform.position;
        
        // 获取当前topping的位置（世界坐标）
        Vector3 toppingPos = transform.position;
        
        // 计算相对于中心的向量和距离
        Vector3 toCenter = center - toppingPos;
        float distance = toCenter.magnitude;
        
        if (distance < 0.001f)
        {
            return; // 距离太近，跳过计算
        }
        
        // 使用已建立的反射字段读取angularVelocity（如果尚未绑定则尝试在此处延迟绑定）
        float angularVelocity = 0f;

        if (angularVelocityField == null)
        {
            SetIngredient(ingredient);
        }

        if (angularVelocityField != null && currentYogurtProgress != null)
        {
            object value = angularVelocityField.GetValue(currentYogurtProgress);
            if (value != null)
            {
                angularVelocity = (float)value;
            }
        }
        
        // 将angularVelocity转换为rad/s（200度/秒 = 1周/秒 = 2π rad/s）
        float angularVelocityRad = angularVelocity/200*360 * Mathf.Deg2Rad;
        
        // 计算topping的径向速度（相对于中心）
        Vector3 radialDirection = new Vector3(-toCenter.y, toCenter.x, 0f).normalized; // 垂直于toCenter的方向（切向）
        Vector2 toppingVelocity = rb.velocity;
        float toppingRadialVelocity = Vector2.Dot(toppingVelocity, new Vector2(radialDirection.x, radialDirection.y));
        
        // 计算ingredient在该点的径向速度
        // v = ω * r（角速度 * 半径）
        float ingredientRadialVelocity = angularVelocityRad * distance;
        
        // 计算“切向”力（原实现称为径向力）：与ingredient旋转方向相同，力正比于速度差值
        float velocityDifference =  - toppingRadialVelocity - ingredientRadialVelocity;
        Vector3 radialForce = radialDirection * velocityDifference * radialForceCoefficient;
        
        // 计算法向力：指向中心，大小正比于旋转速度和距离
        Vector3 normalDirection = toCenter.normalized;
        float normalForceMagnitude = Mathf.Abs(angularVelocityRad) * distance * normalForceCoefficient;
        Vector3 normalForce = normalDirection * normalForceMagnitude;

        // 约束：计算 ingredient 与 topping 的世界半径，若两者中心距离将超过 (ingredientRadius - toppingRadius)
        // 则将 topping 的法向（径向）速度反向，防止离开 ingredient 范围
        float ingredientRadius = 0f;
        CircleCollider2D ingredientCircle = ingredientTransform.GetComponent<CircleCollider2D>();
        if (ingredientCircle != null)
        {
            ingredientRadius = ingredientCircle.radius * ingredientTransform.lossyScale.x;
        }
        else
        {
            // 回退：使用 bounds 的最大半径估计
            Collider2D ic = ingredientTransform.GetComponent<Collider2D>();
            if (ic != null)
            {
                ingredientRadius = Mathf.Max(ic.bounds.extents.x, ic.bounds.extents.y);
            }
        }

        float toppingRadius = 0f;
        CircleCollider2D toppingCircle = GetComponent<CircleCollider2D>();
        if (toppingCircle != null)
        {
            toppingRadius = toppingCircle.radius * transform.lossyScale.x;
        }
        else
        {
            Collider2D tc = GetComponent<Collider2D>();
            if (tc != null)
            {
                toppingRadius = Mathf.Max(tc.bounds.extents.x, tc.bounds.extents.y);
            }
        }

        // 阈值：两者中心距离大于 ingredientRadius - toppingRadius 时即将离开
        float threshold = Mathf.Max(0f, ingredientRadius - toppingRadius);
        if (distance > threshold && rb != null)
        {
            Vector2 vel2 = rb.velocity;
            Vector2 normalDir2 = new Vector2(normalDirection.x, normalDirection.y);
            float normalVel = Vector2.Dot(vel2, normalDir2); // positive -> moving toward center, negative -> moving away
            if (normalVel < 0f)
            {
                // 反转法向速度分量
                vel2 -= 2f * normalVel * normalDir2;
                rb.velocity = vel2;
            }
        }
        
        // 应用力
        Vector3 totalForce = radialForce + normalForce;
        // 输出已去除（调试信息已清理）
        rb.AddForce(totalForce, ForceMode2D.Force);
    }

    /// <summary>
    /// 计算当前帧的融化量（包含自然融化和鼠标滑动影响的占位）
    /// 不返回值，结果会写入 stateData 以供外部查询/调试
    /// </summary>
    private void CalcMelt()
    {
        float currentMelt = 0f;

        // 自然融化量：与周长和速度成正比
        float toppingRadius = 0f;
        CircleCollider2D toppingCircle = GetComponent<CircleCollider2D>();
        if (toppingCircle != null)
        {
            toppingRadius = toppingCircle.radius * transform.lossyScale.x;
        }

        // 使用刚体速度的大小作为速度量级
        float velocityMagnitude = (rb != null) ? rb.velocity.magnitude : 0f;

        float naturalMelt = Mathf.Sqrt(toppingRadius) * (Mathf.Pow(velocityMagnitude+0.7f, 2)*20+1) * meltCoefficient;
        Debug.Log($"{toppingRadius}, {velocityMagnitude}");
        currentMelt += naturalMelt;

        // 鼠标滑动部分：记录每帧鼠标世界位置（占位，暂不计算滑动对融化的贡献）
        lastMouseWorldPosition = currentMouseWorldPosition;
        if (Camera.main != null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            currentMouseWorldPosition = mousePos;
        }
        else
        {
            // Camera.main 为空时，保留为 Vector3.zero 或上次值
        }
        // 鼠标滑动部分：记录每帧鼠标世界位置并计算对融化的贡献
        float mouseMelt;
        bool prevInBounds = IsMouseInBounds(lastMouseWorldPosition);
        bool currInBounds = IsMouseInBounds(currentMouseWorldPosition);
        if (!prevInBounds && !currInBounds && ingredient != null)
        {
            // 回退：如果自身检测都为 false，再尝试使用 ingredient 的检测（兼容旧逻辑）
            prevInBounds = IsMouseInBounds(lastMouseWorldPosition);
            currInBounds = IsMouseInBounds(currentMouseWorldPosition);
        }

        if (!prevInBounds && !currInBounds)
        {
            // 两帧都不在碰撞体范围内，不计算
            mouseMelt = 0f;
        }
        else if (prevInBounds ^ currInBounds)
        {
            // 只有一帧在范围内，贡献为最小值
            mouseMelt = minMouseMelt;
        }
        else
        {
            // 两帧都在范围内，计算滑动距离并乘以因子，最小为 2
            float dragDistance = Vector3.Distance(lastMouseWorldPosition, currentMouseWorldPosition);
            mouseMelt = dragDistance * mouseMeltFactor;
            if (mouseMelt < minMouseMelt)
            {
                mouseMelt = minMouseMelt;
            }
        }

        currentMelt += mouseMelt;

        // 将当前帧的总融化量写入状态数据（仅存储总量）
        stateData["currentMelt"] = currentMelt;

        // 根据 currentMelt 缩小 scale（等比例缩放，假定局部缩放各轴始终相等）
        float shrinkAmount = currentMelt * meltToScaleFactor;
        if (shrinkAmount > 0f)
        {
            float currentUniformScale = transform.localScale.x;
            float newUniformScale = Mathf.Max(0f, currentUniformScale - shrinkAmount);
            transform.localScale = Vector3.one * newUniformScale;

            // 若缩放低于阈值则销毁（在同一处判断，避免多次读取各轴）
            if (newUniformScale < minScaleThreshold)
            {
                OnMeltEnd();
            }
        }
    }
    
    /// <summary>
    /// 创建复制并重新定位所有Topping
    /// </summary>
    private void CreateClonesAndReposition()
    {
        // 获取IngredientController的GameObject（通过parent查找）
        Transform ingredientControllerTransform = transform.parent;
        if (ingredientControllerTransform == null)
        {
            Debug.LogWarning("[BaseTopping] 无法找到IngredientController的GameObject，无法创建复制");
            return;
        }
        
        IngredientController ingredientController = ingredientControllerTransform.GetComponent<IngredientController>();
        if (ingredientController == null)
        {
            Debug.LogWarning("[BaseTopping] Parent不是IngredientController，无法创建复制");
            return;
        }
        
        // 确保复制数量有效
        if (cloneCount < 0)
        {
            cloneCount = 0;
        }
        
        Vector3 center = ingredientControllerTransform.position;
        int totalCount = 1 + cloneCount; // 原 + 复制数量
        
        // 生成复制
        BaseTopping[] allToppings = GenerateClones(ingredientControllerTransform, ingredientController);
        
        // 生成位置（总数 = 原 + 复制）
        Vector3[] positions = GeneratePositionsAroundCenter(center, maxDist, totalCount);
        
        // 设置所有Topping的位置（使用localPosition）
        transform.localPosition = positions[0] - center;
        for (int i = 0; i < allToppings.Length; i++)
        {
            if (allToppings[i] != null)
            {
                allToppings[i].transform.localPosition = positions[i + 1] - center;
            }
        }
    }
    
    /// <summary>
    /// 随机等比例缩放当前 Topping（70% - 130%）
    /// </summary>
    private void RandomScale()
    {
        float currentScale = transform.localScale.x;
        float randomFactor = Random.Range(0.7f, 1.3f);
        float newScale = Mathf.Max(0f, currentScale * randomFactor);
        transform.localScale = Vector3.one * newScale;
        // 标记已设置 scale（如果基类存在该字段则生效）
        hasSetScale = true;
    }
    
    /// <summary>
    /// 生成复制体
    /// </summary>
    /// <param name="parent">父Transform</param>
    /// <param name="ingredientController">IngredientController引用</param>
    /// <returns>生成的复制体数组</returns>
    private BaseTopping[] GenerateClones(Transform parent, IngredientController ingredientController)
    {
        if (cloneCount <= 0)
        {
            return new BaseTopping[0];
        }
        
        BaseTopping[] clones = new BaseTopping[cloneCount];
        
        for (int i = 0; i < cloneCount; i++)
        {
            // 创建复制
            GameObject cloneObj = Instantiate(gameObject, parent);
            BaseTopping clone = cloneObj.GetComponent<BaseTopping>();
            
            if (clone != null)
            {
                // 标记为复制体
                clone.isClone = true;
                clone.hasSetScale = true; // 复制体已经有scale了
                clone.hasPerformedFirstShow = true; // 复制体不触发首次Show效果
                
                // 为复制体设置所属 IngredientController，以便 EnablePhysics 能正常工作
                clone.SetIngredient(ingredientController);

                // 让复制体在首次出现时也执行随机缩放
                clone.RandomScale();

                // 将复制添加到IngredientController（显示/隐藏由 IngredientController 管理）
                ingredientController.AddTopping(clone);
                
                // 复制体也需要启用物理效果
                clone.EnablePhysics();
            }
            
            clones[i] = clone;
        }
        
        return clones;
    }
    
    /// <summary>
    /// 生成指定数量的位置，距离中心不大于maxDistance，且所有位置之和等于center * count
    /// 要求：pos1 + pos2 + ... + posN = center * count（平均值等于center）
    /// </summary>
    /// <param name="center">中心位置</param>
    /// <param name="maxDistance">最大距离</param>
    /// <param name="count">位置数量</param>
    /// <returns>生成的位置数组</returns>
    private Vector3[] GeneratePositionsAroundCenter(Vector3 center, float maxDistance, int count)
    {
        if (count <= 0)
        {
            return new Vector3[0];
        }
        
        Vector3[] positions = new Vector3[count];
        
        // 生成前count-1个随机位置
        Vector3[] offsets = new Vector3[count - 1];
        Vector3 sumOffsets = Vector3.zero;
        
        for (int attempt = 0; attempt < 100; attempt++)
        {
            sumOffsets = Vector3.zero;
            
            // 生成前count-1个偏移
            for (int i = 0; i < count - 1; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0f, maxDistance);
                offsets[i] = new Vector3(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance,
                    0f
                );
                sumOffsets += offsets[i];
            }
            
            // 计算最后一个偏移，使所有位置之和等于center * count
            // pos1 + pos2 + ... + posN = center * count
            // (center + offset1) + ... + (center + offsetN) = center * count
            // count * center + sum(offsets) = center * count
            // sum(offsets) = 0
            // 所以最后一个offset = -sum(前N-1个offsets)
            Vector3 lastOffset = -sumOffsets;
            
            // 检查最后一个位置是否在范围内
            Vector3 lastPos = center + lastOffset;
            float lastDistance = Vector3.Distance(lastPos, center);
            if (lastDistance <= maxDistance)
            {
                // 找到有效的位置组合
                for (int i = 0; i < count - 1; i++)
                {
                    positions[i] = center + offsets[i];
                }
                positions[count - 1] = lastPos;
                return positions;
            }
        }
        
        // 如果100次尝试都失败，使用备选方案：生成位置后调整
        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = (i * angleStep + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, maxDistance * 0.6f); // 使用较小的距离
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance,
                0f
            );
            positions[i] = center + offset;
        }
        
        // 调整使所有位置之和等于center * count（平均值等于center）
        Vector3 currentSum = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            currentSum += positions[i];
        }
        Vector3 targetSum = center * count;
        Vector3 difference = targetSum - currentSum;
        
        // 将差值分配到所有位置
        for (int i = 0; i < count; i++)
        {
            positions[i] += difference / count;
            
            // 确保调整后仍在范围内
            float distance = Vector3.Distance(positions[i], center);
            if (distance > maxDistance)
            {
                Vector3 direction = (positions[i] - center).normalized;
                positions[i] = center + direction * maxDistance;
            }
        }
        
        // 再次验证和调整
        Vector3 finalSum = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            finalSum += positions[i];
        }
        Vector3 finalAdjustment = (targetSum - finalSum) / count;
        for (int i = 0; i < count; i++)
        {
            positions[i] += finalAdjustment;
            
            // 限制在范围内
            float distance = Vector3.Distance(positions[i], center);
            if (distance > maxDistance)
            {
                Vector3 direction = (positions[i] - center).normalized;
                positions[i] = center + direction * maxDistance;
            }
        }
        
        return positions;
    }
    
    /// <summary>
    /// 加载Topping的具体实现
    /// 在Ingredient首次放大时调用
    /// </summary>
    public override void LoadTopping()
    {
        // 在这里实现具体的加载逻辑
        // 例如：设置材质、动画、数值等
        
        // 示例：记录加载时间
        stateData["loadTime"] = Time.time;
        stateData["toppingName"] = toppingName;
        stateData["toppingValue"] = toppingValue;
        
        // 可以在这里添加更多初始化逻辑
        InitializeTopping();
    }
    
    /// <summary>
    /// 初始化Topping（子类可重写）
    /// </summary>
    protected virtual void InitializeTopping()
    {
        // 子类可以在这里实现特定的初始化逻辑
    }
    
    /// <summary>
    /// 重写UpdateState以添加Topping特定的状态
    /// </summary>
    protected override void UpdateState()
    {
        base.UpdateState();
        
        // 添加Topping特定的状态
        stateData["toppingName"] = toppingName;
        stateData["toppingValue"] = toppingValue;
    }

    /// <summary>
    /// 在销毁时从所属 IngredientController 中移除自身，完成清理工作
    /// </summary>
    private void OnMeltEnd()
    {
        // 在融化结束时，将 flavor 加一（如果存在当前的 Ingredient 模型）
        if (currentYogurtProgress != null)
        {
            currentYogurtProgress.AdjustFlavor(1f);
        }

        if (ingredient != null)
        {
            ingredient.RemoveTopping(this);
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// 检查给定世界坐标的鼠标点是否在当前 Topping 的碰撞体范围内（基于本对象的 Collider2D）
    /// </summary>
    /// <param name="mouseWorldPosition">鼠标世界坐标</param>
    /// <returns>在范围内返回 true，否则 false</returns>
    public bool IsMouseInBounds(Vector3 mouseWorldPosition)
    {
        Collider2D col = cachedCollider != null ? cachedCollider : GetComponent<Collider2D>();

        // 对常见 Collider 类型分别处理
        if (col == null)
        {
            return false;
        }
        if (col is CircleCollider2D circle)
        {
            Vector2 center = (Vector2)circle.bounds.center;
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            float radius = circle.radius * scale;
            Vector2 mouse2D = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
            return Vector2.Distance(mouse2D, center) <= radius;
        }
        else if (col is BoxCollider2D)
        {
            return col.bounds.Contains(mouseWorldPosition);
        }
        else
        {
            // 默认使用 bounds 检查
            return col.bounds.Contains(mouseWorldPosition);
        }
    }
}

