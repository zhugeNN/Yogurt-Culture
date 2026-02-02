using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class StickController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("物理设置")]
    [Tooltip("是否启用重力（拖动时）")]
    [SerializeField] private bool useGravity = true;
    
    [Header("恢复设置")]
    [Tooltip("恢复动画曲线（X轴：时间0-1，Y轴：插值0-1）")]
    [SerializeField] private AnimationCurve restoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Tooltip("恢复时间（秒）")]
    [SerializeField] private float restoreDuration = 0.5f;

    private Rigidbody2D rb;
    private Collider2D col;
    private HingeJoint2D joint2D;
    private Camera mainCamera;
    
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isDragging;
    private Coroutine restoreCoroutine;
    
    // 铰链 anchor 的初始位置（本地坐标）
    private Vector2 initialAnchor;
    
    // 上一帧的鼠标世界位置（用于计算移动向量）
    private Vector3 lastMouseWorldPosition;
    private bool hasLastMousePosition;
    
    // 光标限制相关
    private IngredientController activeIngredientController;
    private Bounds cursorRestrictionBounds;
    private bool isCursorRestricted = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        col = GetComponent<Collider2D>();
        joint2D = GetComponent<HingeJoint2D>();
        if (joint2D == null)
        {
            // Debug.LogWarning("StickController: 未找到 HingeJoint2D 组件，请添加该组件。");
        }
        
        mainCamera = Camera.main;
        
        // 确保 EventSystem 存在
        EnsureEventSystem();
        
        // 确保 Camera 有 Physics2DRaycaster
        EnsurePhysics2DRaycaster();
        
        // 记录初始状态
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        // 记录初始 anchor 位置
        if (joint2D != null)
        {
            initialAnchor = joint2D.anchor;
        }
        
        // 初始状态：不受重力，不受物理影响
        rb.gravityScale = 0f;
        rb.isKinematic = true;
        
        // 初始状态：禁用铰链
        if (joint2D != null)
        {
            joint2D.enabled = false;
        }
    }
    
    /// <summary>
    /// 确保场景中有 EventSystem
    /// </summary>
    private void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
    }
    
    /// <summary>
    /// 确保 Camera 有 Physics2DRaycaster（用于2D拖拽检测）
    /// </summary>
    private void EnsurePhysics2DRaycaster()
    {
        if (mainCamera != null && mainCamera.GetComponent<Physics2DRaycaster>() == null)
        {
            mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
        }
    }

    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (restoreCoroutine != null)
        {
            return; // 恢复过程中不允许拖动
        }

        if (joint2D == null)
        {
            // Debug.LogWarning("StickController: HingeJoint2D 组件未找到，无法拖动。");
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Debug.LogWarning("StickController: 未找到主摄像机。");
                return;
            }
        }

        isDragging = true;
        
        // 计算鼠标世界坐标（使用 EventSystem 提供的位置）
        Vector3 mouseWorldPoint = GetWorldPosition(eventData.position);
        
        // 记录初始鼠标位置（用于计算移动向量）
        lastMouseWorldPosition = mouseWorldPoint;
        hasLastMousePosition = true;
        
        // 计算当前 anchor 的世界位置
        Vector3 currentAnchorWorld = transform.TransformPoint(joint2D.anchor);
        
        // 计算从 anchor 到鼠标位置的向量
        Vector3 anchorToMouse = mouseWorldPoint - currentAnchorWorld;
        
        // 将物体 position 增加这个向量，保证起始时 anchor 与鼠标重合
        transform.position += anchorToMouse;
        
        // 切换到物理模式
        rb.isKinematic = false;
        rb.gravityScale = useGravity ? 1f : 0f;
        rb.freezeRotation = false; // 允许旋转
        rb.angularDrag = 10;
        
        // 启用铰链
        joint2D.enabled = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || restoreCoroutine != null)
        {
            return;
        }

        if (joint2D == null || mainCamera == null)
        {
            return;
        }

        // 更新鼠标世界坐标（使用 EventSystem 提供的位置）
        Vector3 mouseWorldPoint = GetWorldPosition(eventData.position);
        
        // 如果启用了光标限制，限制鼠标位置在范围内
        if (isCursorRestricted)
        {
            mouseWorldPoint = ClampMousePositionToBounds(mouseWorldPoint);
        }
        
        // 计算鼠标移动向量
        if (hasLastMousePosition)
        {
            Vector3 mouseDelta = mouseWorldPoint - lastMouseWorldPosition;
            
            // 显式地将鼠标移动向量加到物体 position 上
            transform.position += mouseDelta;
        }
        
        // 更新上一帧的鼠标位置
        lastMouseWorldPosition = mouseWorldPoint;
        hasLastMousePosition = true;
        
        // 将鼠标位置转换为物体本地坐标，更新铰链 anchor
        // 这样铰链的固定点（世界位置）会跟随鼠标移动
        // anchor 的本地坐标会变化，但这是为了让固定点跟随鼠标
        Vector2 localAnchor = transform.InverseTransformPoint(mouseWorldPoint);
        joint2D.anchor = localAnchor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        hasLastMousePosition = false; // 重置鼠标位置记录
        StartRestore();
    }
    
    /// <summary>
    /// 将屏幕坐标转换为世界坐标
    /// </summary>
    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Vector3 mouseScreen = screenPosition;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(mouseScreen);
    }

    private void StartRestore()
    {
        // 如果已有恢复协程在运行，先停止它
        if (restoreCoroutine != null)
        {
            StopCoroutine(restoreCoroutine);
        }
        
        // 禁用铰链
        if (joint2D != null)
        {
            joint2D.enabled = false;
        }
        
        // 停止物理模拟
        rb.isKinematic = true;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        // 恢复初始 anchor
        if (joint2D != null)
        {
            joint2D.anchor = initialAnchor;
        }
        
        // 启动恢复协程
        restoreCoroutine = StartCoroutine(RestoreCoroutine());
    }

    private IEnumerator RestoreCoroutine()
    {
        Vector3 restoreStartPosition = transform.position;
        Quaternion restoreStartRotation = transform.rotation;
        float elapsedTime = 0f;
        
        while (elapsedTime < restoreDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / restoreDuration);
            
            // 使用曲线计算插值
            float curveValue = restoreCurve.Evaluate(normalizedTime);
            
            // 插值位置和旋转
            transform.position = Vector3.Lerp(restoreStartPosition, initialPosition, curveValue);
            transform.rotation = Quaternion.Lerp(restoreStartRotation, initialRotation, curveValue);
            
            yield return null;
        }
        
        // 确保最终状态精确
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        restoreCoroutine = null;
    }

    /// <summary>
    /// 重置到初始状态（外部调用）
    /// </summary>
    public void ResetToInitial()
    {
        if (isDragging)
        {
            isDragging = false;
        }
        
        StartRestore();
    }

    /// <summary>
    /// 设置初始位置和旋转（外部调用）
    /// </summary>
    public void SetInitialState(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation;
    }

    /// <summary>
    /// 检查是否处于拖动状态（外部调用）
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }

    /// <summary>
    /// 设置光标限制（由 IngredientController 调用）
    /// </summary>
    public void SetCursorRestriction(IngredientController controller, Bounds bounds)
    {
        activeIngredientController = controller;
        cursorRestrictionBounds = bounds;
        isCursorRestricted = true;
    }

    /// <summary>
    /// 清除光标限制（由 IngredientController 调用）
    /// </summary>
    public void ClearCursorRestriction(IngredientController controller)
    {
        if (activeIngredientController == controller)
        {
            activeIngredientController = null;
            isCursorRestricted = false;
        }
    }

    /// <summary>
    /// 将鼠标位置限制在bounds内
    /// </summary>
    private Vector3 ClampMousePositionToBounds(Vector3 mouseWorldPoint)
    {
        if (!isCursorRestricted || activeIngredientController == null)
        {
            return mouseWorldPoint;
        }

        Collider2D col = activeIngredientController.GetComponent<Collider2D>();
        if (col == null)
        {
            return mouseWorldPoint;
        }

        // 根据 Collider 类型进行不同的限制
        if (col is CircleCollider2D circleCollider)
        {
            // 圆形范围限制
            Vector2 center = (Vector2)circleCollider.bounds.center;
            // 使用 lossyScale 的最大值来缩放半径（等比例缩放）
            float scale = Mathf.Max(Mathf.Abs(activeIngredientController.transform.lossyScale.x), Mathf.Abs(activeIngredientController.transform.lossyScale.y));
            float radius = circleCollider.radius * scale;
            
            Vector2 mousePos2D = new Vector2(mouseWorldPoint.x, mouseWorldPoint.y);
            Vector2 direction = mousePos2D - center;
            float distance = direction.magnitude;
            
            if (distance > radius)
            {
                // 将鼠标位置限制在圆形边界上
                if (distance > 0.001f) // 避免除零
                {
                    direction = direction.normalized * radius;
                }
                else
                {
                    direction = Vector2.up * radius; // 如果距离为0，使用向上方向
                }
                return new Vector3(center.x + direction.x, center.y + direction.y, mouseWorldPoint.z);
            }
            
            return mouseWorldPoint;
        }
        else if (col is BoxCollider2D)
        {
            // 矩形范围限制
            cursorRestrictionBounds = col.bounds;
            Vector3 clampedPosition = new Vector3(
                Mathf.Clamp(mouseWorldPoint.x, cursorRestrictionBounds.min.x, cursorRestrictionBounds.max.x),
                Mathf.Clamp(mouseWorldPoint.y, cursorRestrictionBounds.min.y, cursorRestrictionBounds.max.y),
                mouseWorldPoint.z
            );
            return clampedPosition;
        }
        else
        {
            // 默认使用矩形限制
            cursorRestrictionBounds = col.bounds;
            Vector3 clampedPosition = new Vector3(
                Mathf.Clamp(mouseWorldPoint.x, cursorRestrictionBounds.min.x, cursorRestrictionBounds.max.x),
                Mathf.Clamp(mouseWorldPoint.y, cursorRestrictionBounds.min.y, cursorRestrictionBounds.max.y),
                mouseWorldPoint.z
            );
            return clampedPosition;
        }
    }

    /// <summary>
    /// 检查当前鼠标位置是否在限制范围内（供 Ingredient 使用）
    /// </summary>
    public bool IsMouseInRestrictionBounds()
    {
        if (!isCursorRestricted || activeIngredientController == null)
        {
            return false;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }
        }

        // 使用 Input.mousePosition（此方法由外部调用，不通过 EventSystem）
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPoint = mainCamera.ScreenToWorldPoint(mouseScreen);

        return activeIngredientController.IsMouseInBounds(mouseWorldPoint);
    }
}

