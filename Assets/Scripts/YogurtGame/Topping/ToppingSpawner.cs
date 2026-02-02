using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Topping生成器：从操作台鼠标按下后生成Topping实体并跟随鼠标
/// 将此脚本挂载到操作台的GameObject上
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ToppingSpawner : MonoBehaviour, IPointerDownHandler
{
    [Header("Topping设置")]
    [Tooltip("要生成的Topping Prefab")]
    [SerializeField] private GameObject toppingPrefab;
    
    [Tooltip("生成的Topping的父节点（留空则挂载到场景根节点）")]
    [SerializeField] private Transform parentTransform;
    
    private Camera mainCamera;
    private Topping currentTopping;
    private bool isDraggingTopping = false;
    
    private void Awake()
    {
        mainCamera = Camera.main;
        
        // 确保 EventSystem 存在
        EnsureEventSystem();
        
        // 确保 Camera 有 Physics2DRaycaster
        EnsurePhysics2DRaycaster();
        
        // 确保 Collider2D 不是 Trigger（用于点击检测）
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;
        }
    }
    
    private void Update()
    {
        // 如果正在拖拽Topping，更新其位置跟随鼠标
        if (isDraggingTopping && currentTopping != null && mainCamera != null)
        {
            Vector3 mouseScreen = Input.mousePosition;
            mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - currentTopping.transform.position.z);
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);
            
            // 中心点跟随鼠标位置
            currentTopping.transform.position = mouseWorld;
        }
        
        // 检测鼠标松开
        if (isDraggingTopping && !Input.GetMouseButton(0))
        {
            // 鼠标松开，触发Topping的检测逻辑
            if (currentTopping != null)
            {
                // 调用Topping的检测方法
                currentTopping.CheckAndAddToIngredient();
                
                // 停止拖拽管理
                isDraggingTopping = false;
                currentTopping = null;
            }
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
    
    /// <summary>
    /// 鼠标按下事件处理
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (toppingPrefab == null)
        {
            Debug.LogWarning("ToppingSpawner: Topping Prefab 未设置！");
            return;
        }
        
        // 如果已经有Topping在拖拽，不生成新的
        if (isDraggingTopping && currentTopping != null)
        {
            return;
        }
        
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        if (mainCamera == null)
        {
            Debug.LogWarning("ToppingSpawner: 未找到主摄像机！");
            return;
        }
        
        // 在鼠标位置生成
        Vector3 mouseScreen = eventData.position;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 spawnPosition = mainCamera.ScreenToWorldPoint(mouseScreen);
        
        // 确定父节点
        Transform parent = parentTransform != null ? parentTransform : transform.parent;
        if (parent == null)
        {
            parent = null; // 挂载到场景根节点
        }
        
        // 实例化Topping
        GameObject toppingInstance = Instantiate(toppingPrefab, spawnPosition, Quaternion.identity, parent);
        
        // 确保Topping有Topping组件
        Topping topping = toppingInstance.GetComponent<Topping>();
        if (topping == null)
        {
            Debug.LogWarning($"ToppingSpawner: Prefab {toppingPrefab.name} 没有 Topping 组件！");
            Destroy(toppingInstance);
            return;
        }
        
        // 禁用Topping的拖拽功能（由Spawner统一管理）
        // 注意：这里我们需要临时禁用Topping的拖拽，或者让Topping知道它正在被Spawner管理
        // 为了简单，我们直接管理位置，但Topping的OnEndDrag仍然会处理添加到Ingredient的逻辑
        
        // 设置当前Topping并开始拖拽
        currentTopping = topping;
        isDraggingTopping = true;
        
        // 确保Topping的中心点跟随鼠标（设置keepOffsetFromMouse为false的效果）
        // 由于我们直接控制位置，不需要设置offset
    }
    
    /// <summary>
    /// 设置Topping Prefab（外部调用）
    /// </summary>
    public void SetToppingPrefab(GameObject prefab)
    {
        toppingPrefab = prefab;
    }
    
    /// <summary>
    /// 获取Topping Prefab（外部调用）
    /// </summary>
    public GameObject GetToppingPrefab()
    {
        return toppingPrefab;
    }
}

