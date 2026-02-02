using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 将本脚本挂在任意带 Collider2D 的物体上，即可用鼠标拖拽并在场景中放置。
/// 适用于2D场景（正交摄像机）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class YogurtProduct : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("所含配料")]
    [SerializeField] private List<Type> ingredientTypes = new();

    [Header("拖拽设置")]
    [Tooltip("拖拽时物体与鼠标之间是否保持初始偏移（true：更自然；false：中心跟随鼠标）。")]
    [SerializeField] private bool keepOffsetFromMouse = true;

    [Tooltip("是否在拖拽过程中锁定Z轴位置。")]
    [SerializeField] private bool lockZAxis = true;

    [Header("拖动范围限制")]
    [Tooltip("限制拖动范围的 GameObject（留空则自动查找 tag 为 Gameboard 的物体）")]
    [SerializeField] private GameObject dragBoundsCollider;
    private Bounds bounds;
    private bool hasBounds = false;

    [Header("接手检测")]
    [Tooltip("拖拽结束后判定的 Layer 名称（需在 Project Settings > Tags and Layers 中配置）。")]
    [SerializeField] private string orderLayerName = "order";

    private Camera mainCamera;
    private bool isDragging;
    private Vector3 dragOffset;
    private float objectZ;
    private int orderLayerMask;

    private void Awake()
    {
        mainCamera = Camera.main;
        objectZ = transform.position.z;
        orderLayerMask = LayerMask.GetMask(orderLayerName);
        
        // 确保 EventSystem 存在
        EnsureEventSystem();
        
        // 确保 Camera 有 Physics2DRaycaster
        EnsurePhysics2DRaycaster();
        
        // 初始化拖动范围 bounds
        InitializeDragBounds();
        
        if (orderLayerMask == 0)
        {
            // Debug.LogWarning($"ShopItem: Layer '{orderLayerName}' 未找到，将无法触发订单检测。");
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
    /// 初始化拖动范围 bounds
    /// </summary>
    private void InitializeDragBounds()
    {
        GameObject boundsObject = null;
        
        // 优先使用手动设置的 dragBoundsCollider
        if (dragBoundsCollider != null)
        {
            boundsObject = dragBoundsCollider;
        }
        else
        {
            // 自动查找 tag 为 Gameboard 的物体
            GameObject gameboard = GameObject.FindGameObjectWithTag("Gameboard");
            if (gameboard != null)
            {
                boundsObject = gameboard;
            }
        }
        
        // 获取 BoxCollider2D 的 bounds
        if (boundsObject != null)
        {
            BoxCollider2D boxCollider = boundsObject.GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                bounds = boxCollider.bounds;
                hasBounds = true;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            // Debug.LogWarning("DraggableObject: 未找到主摄像机，无法进行拖拽。");
            return;
        }

        isDragging = true;

        Vector3 mouseWorld = GetWorldPosition(eventData.position);
        if (keepOffsetFromMouse)
        {
            dragOffset = transform.position - mouseWorld;
        }
        else
        {
            dragOffset = Vector3.zero;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || mainCamera == null)
        {
            return;
        }

        Vector3 mouseWorld = GetWorldPosition(eventData.position);
        Vector3 targetPos = mouseWorld + dragOffset;

        if (lockZAxis)
        {
            targetPos.z = objectZ;
        }

        // 如果设置了拖动范围限制，将位置限制在范围内
        if (hasBounds)
        {
            targetPos = ClampPositionToBounds(targetPos);
        }

        transform.position = targetPos;
    }

    /// <summary>
    /// 将位置限制在拖动范围内
    /// </summary>
    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        Collider2D selfCollider = GetComponent<Collider2D>();
        
        if (selfCollider != null)
        {
            // 考虑物体自身 Collider 的大小，确保整个物体都在范围内
            Bounds selfBounds = selfCollider.bounds;
            float halfWidth = selfBounds.extents.x;
            float halfHeight = selfBounds.extents.y;
            
            // 限制位置，确保物体的 bounds 完全在拖动范围内
            float clampedX = Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
            float clampedY = Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
            
            return new Vector3(clampedX, clampedY, position.z);
        }
        else
        {
            // 如果没有 Collider，只限制中心点
            float clampedX = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float clampedY = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            
            return new Vector3(clampedX, clampedY, position.z);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        HandOver();
    }
    
    /// <summary>
    /// 将屏幕坐标转换为世界坐标
    /// </summary>
    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Vector3 mouseScreen = screenPosition;
        // 对于2D正交相机，使用物体当前z或相机到物体的z距离
        float z = lockZAxis ? Mathf.Abs(mainCamera.transform.position.z - objectZ) : Mathf.Abs(mainCamera.transform.position.z);
        mouseScreen.z = z;
        return mainCamera.ScreenToWorldPoint(mouseScreen);
    }

    /// <summary>
    /// 拖拽结束后检查是否与目标区域重叠
    /// </summary>
    private void HandOver()
    {
        Collider2D selfCollider = GetComponent<Collider2D>();
        if (selfCollider == null)
        {
            return;
        }

        if (orderLayerMask == 0)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = orderLayerMask,
            useTriggers = true
        };
        Collider2D[] results = new Collider2D[4];
        int hitCount = selfCollider.OverlapCollider(filter, results);
        if (hitCount > 0)
        {
            OrderManager.Instance?.HandleOrderSubmit(this);
        }
    }
    public void SetIngredients(IList<Ingredient> newIngredients)
    {
        ingredientTypes = new List<Type>();
        foreach (Ingredient ingredient in newIngredients)
        {
            if (ingredient != null)
            {
                ingredientTypes.Add(ingredient.GetType());
            }
        }
    }

    public List<Type> GetIngredientTypes()
    {
        return ingredientTypes;
    }
    
    // Flavor (口味) 存储与访问
    private float flavor = 0f;
    public void SetFlavor(float value) { flavor = value; }
    public float GetFlavor() { return flavor; }
}