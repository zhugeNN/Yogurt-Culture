using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Unity;

public class NormalYogurt : Ingredient
{
    [Header("旋转物理参数")]
    [Tooltip("搅拌力系数（搅拌速度产生的力）")]
    [SerializeField] private float baseStirForceCoefficient;
    private float stirForceCoefficient;
    
    [Tooltip("阻力系数（F = dragCoefficient * angularVelocity）")]
    [SerializeField] private float dragCoefficient = 8f;
    
    [Tooltip("进度增长系数（根据旋转速度计算进度）")]
    [SerializeField] private float progressCoefficient = 0.5f;
    
    [Tooltip("最大旋转速度（限制旋转速度上限）")]
    [SerializeField] private float maxAngularVelocity = 200f;
    
    [Tooltip("最小检测距离（像素）")]
    [SerializeField] private float minCheckDistance = 15f;

    [Header("UI显示组件")]
    [Tooltip("显示角速度的TextMeshPro组件（可选，如果为空则自动查找）")]
    [SerializeField] private TextMeshProUGUI angularVelocityText;
    
    [Tooltip("显示进度速度的TextMeshPro组件（可选，如果为空则自动查找）")]
    [SerializeField] private TextMeshProUGUI progressSpeedText;

    // 旋转速度（正数=顺时针，负数=逆时针）
    private float angularVelocity = 0f;
    private float extraStirForce = 0f;
    
    // 鼠标位置跟踪
    private Vector3 lastMousePosition;
    private bool hasLastMousePosition;
    private Vector3 lastMouseDir;
    private bool hasLastMouseDir;

    // Canvas组件引用
    private Canvas canvas;
    
    // 当前进度增长速度（用于UI显示）
    private float currentProgressSpeed = 0f;

    private void Awake()
    {
        // 查找Canvas组件并设置EventCamera
        canvas = GetComponentInChildren<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }
            else
            {
                // 如果MainCamera标签不存在，尝试查找场景中的Camera
                Camera foundCamera = FindObjectOfType<Camera>();
                if (foundCamera != null)
                {
                    canvas.worldCamera = foundCamera;
                }
            }
        }

        // 如果未在Inspector中指定，则自动查找TextMeshProUGUI组件
        if (angularVelocityText == null || progressSpeedText == null)
        {
            TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>();
            if (textComponents.Length >= 2)
            {
                // 如果未指定，则使用找到的前两个组件
                if (angularVelocityText == null)
                {
                    angularVelocityText = textComponents[0];
                }
                if (progressSpeedText == null)
                {
                    progressSpeedText = textComponents[1];
                }
            }
        }
    }

    public override void Initialize(ProgressController ownerController, Slider attachedSlider)
    {
        base.Initialize(ownerController, attachedSlider);
        hasLastMousePosition = false;
        hasLastMouseDir = false;
        angularVelocity = 0f;
        stirForceCoefficient = baseStirForceCoefficient;
    }

    public override void ResetOperation()
    {
        base.ResetOperation();
        angularVelocity = 0f;
        hasLastMousePosition = false;
        hasLastMouseDir = false;
    }

    public override float ExecuteOperation()
    {
        // 检查鼠标是否在限制范围内（通过 StickController）
        StickController stickController = FindObjectOfType<StickController>();
        bool isMouseInBounds = stickController != null && stickController.IsMouseInRestrictionBounds();
        
        // 计算阻力（始终存在）
        float dragForce = dragCoefficient * angularVelocity;
        
        // 如果鼠标在范围内且按下，计算搅拌力
        float stirForce = 0f;
        float stirDirection = 0f; // 1=顺时针，-1=逆时针，0=无搅拌
        
        if (isMouseInBounds && Input.GetMouseButton(0))
        {
            Vector3 currentMouse = Input.mousePosition;
            
            if (!hasLastMousePosition)
            {
                lastMousePosition = currentMouse;
                hasLastMousePosition = true;
            }
            else
            {
                float dist = Vector3.Distance(currentMouse, lastMousePosition);
                if (dist >= minCheckDistance)
                {
                    Vector3 currentDir = currentMouse - lastMousePosition;
                    
                    if (!hasLastMouseDir)
                    {
                        lastMouseDir = currentDir;
                        hasLastMouseDir = true;
                        lastMousePosition = currentMouse;
                    }
                    else
                    {
                        // 计算角度变化（正数=顺时针，负数=逆时针）
                        float angleDelta = Vector2.SignedAngle(lastMouseDir, currentDir);
                        
                        // 计算搅拌速度（角度变化率）
                        float stirSpeed = Mathf.Abs(angleDelta);
                        
                        // 如果角度变化在合理范围内（避免误判）
                        if (stirSpeed > 0f && stirSpeed < 180f)
                        {
                            // 搅拌方向：正数=顺时针，负数=逆时针
                            stirDirection = Mathf.Sign(angleDelta);
                            
                            // 搅拌力 = 搅拌速度 * 系数
                            stirForce = stirSpeed * stirForceCoefficient * stirDirection;
                        }
                        
                        // 更新鼠标位置记录
                        lastMousePosition = currentMouse;
                        lastMouseDir = currentDir;
                    }
                }
            }
        }
        else
        {
            // 鼠标不在范围内或未按下，重置鼠标位置记录
            hasLastMousePosition = false;
            hasLastMouseDir = false;
        }
        
        // 更新旋转速度：angularVelocity += (stirForce - dragForce) * deltaTime
        float netForce = stirForce - dragForce;
        angularVelocity += netForce * Time.deltaTime;
        
        // 限制旋转速度在最大范围内
        angularVelocity = Mathf.Clamp(angularVelocity, -maxAngularVelocity, maxAngularVelocity);
        
        // 如果旋转速度很小，逐渐归零（避免无限小的旋转）
        if (Mathf.Abs(angularVelocity) < 0.1f)
        {
            angularVelocity = 0f;
        }
        
        // 计算进度增长
        float progressIncrease = 0f;
        
        // 只有当搅拌方向和旋转方向相同时，才增长进度
        if (Mathf.Abs(angularVelocity) > 0.1f && stirDirection != 0f)
        {
            // 检查搅拌方向和旋转方向是否相同
            bool sameDirection = (stirDirection > 0f && angularVelocity > 0f) || 
                                (stirDirection < 0f && angularVelocity < 0f);
            
            if (sameDirection)
            {
                // 进度增长 = 旋转速度的绝对值 * 进度系数
                progressIncrease = Mathf.Abs(angularVelocity) * progressCoefficient * Time.deltaTime;
            }
        }
        else if (Mathf.Abs(angularVelocity) > 0.1f && stirDirection == 0f)
        {
            // 没有搅拌但仍在旋转时，根据旋转速度增长进度（惯性）
            progressIncrease = Mathf.Abs(angularVelocity) * progressCoefficient * Time.deltaTime * 0.3f; // 惯性时的效率较低
        }
        
        // 存储当前进度增长速度（用于UI显示）
        currentProgressSpeed = progressIncrease * progressPerUnit;
        
        return progressIncrease;
    }
    #region 升级方法
    //搅拌棒使用
    public void ChangeExtraForce(float percent){
        extraStirForce += percent;
        stirForceCoefficient = baseStirForceCoefficient * (1 + extraStirForce/100f);
        // Debug.Log("now force = " + stirForceCoefficient);
    }
    //Viber使用
    public void SetDrag(float drag = 8f){
        dragCoefficient = drag;
    }
    #endregion
    private void Update()
    {
        // 更新angularVelocity显示
        if (angularVelocityText != null)
        {
            angularVelocityText.text = $"Angular Velocity: {angularVelocity:F2}";
        }

        // 更新progressSpeed显示
        if (progressSpeedText != null)
        {
            progressSpeedText.text = $"Progress Speed: {currentProgressSpeed:F4}";
        }
    }
}