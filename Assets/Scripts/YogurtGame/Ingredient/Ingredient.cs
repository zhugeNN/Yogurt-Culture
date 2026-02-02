using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class Ingredient : MonoBehaviour
{
    [Header("Flavor (口味)")]
    [Tooltip("初始口味值，可由 Inspector 设置或在运行时通过 AdjustFlavor 修改")]
    [SerializeField] private float flavor = 0f;
    /// <summary>
    /// 当口味值发生变化时触发，参数为新的口味值
    /// </summary>
    public event Action<float> OnFlavorChanged;

    /// <summary>
    /// 当前口味值（只读）
    /// </summary>
    public float Flavor => flavor;

    [SerializeField] protected float progressPercent;
    [SerializeField] protected float progressPerUnit = 0.1f;
    [SerializeField] public GameObject prefab;
    protected ProgressController owner;
    protected Slider slider;

    protected bool IsInitialized => owner != null && slider != null;

    public virtual void Initialize(ProgressController ownerController, Slider attachedSlider)
    {
        owner = ownerController;
        slider = attachedSlider != null ? attachedSlider : GetComponent<Slider>();
        
        ResetOperation();

        // 初始化时可以触发一次口味变更事件，供 UI 绑定或逻辑读取初始值
        OnFlavorChanged?.Invoke(flavor);

        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>();
        }
    }
    
    /// <summary>
    /// 调整口味值（可以为正或负），并触发 OnFlavorChanged 回调
    /// </summary>
    /// <param name="delta">要增加的口味量（可为负）</param>
    public void AdjustFlavor(float delta)
    {
        float newVal = flavor + delta;
        flavor = newVal;
        OnFlavorChanged?.Invoke(flavor);
    }

    /// <summary>
    /// 直接设置口味值（触发事件）
    /// </summary>
    public void SetFlavor(float value)
    {
        flavor = value;
        OnFlavorChanged?.Invoke(flavor);
    }
    public void InnerExecute()
    {
        if (!IsInitialized)
        {
            return;
        }

        progressPercent += ExecuteOperation() * progressPerUnit;
        float normalizedProgress = Mathf.Clamp01(progressPercent / 100f);
        SetProgress01(normalizedProgress);

        if (progressPercent >= 100f)
        {
            CompleteOperation();
        }
    }
    public virtual float ExecuteOperation()
    {
        return 0;
    }

    public virtual void ResetOperation()
    {
        if (slider != null)
        {
            slider.value = 0f;
        }
        progressPercent = 0f;
    }

    protected void CompleteOperation()
    {
        owner?.OnYogurtOperationCompleted(this);
    }

    protected void SetProgress01(float normalized)
    {
        if (slider != null)
        {
            slider.value = Mathf.Clamp01(normalized);
        }
    }

    protected float GetProgress01()
    {
        return slider != null ? slider.value : 0f;
    }
}

