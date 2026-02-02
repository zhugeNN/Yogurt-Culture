using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 全局经济系统管理器（单例）
/// - 管理金钱和声望值
/// - 提供外部调用的修改方法
/// - 在每次值发生变化时触发回调事件
/// </summary>
public class EconomyManager : Singleton<EconomyManager>
{
    [Header("起始数值")]
    [Tooltip("初始金钱")]
    [SerializeField] private float startingMoney = 100f;

    [Tooltip("初始声望")]
    [SerializeField] private float startingReputation = 0f;

    [Header("UI绑定")]
    [Tooltip("显示金钱值的TextMeshPro组件")]
    [SerializeField] private TextMeshProUGUI moneyText;

    // 当前状态
    private float money = 0;
    private float reputation;

    // 变更回调：传入新的值
    public event Action<float> OnMoneyChanged;
    public event Action<float> OnReputationChanged;

    protected override void Awake()
    {
        base.Awake();
        reputation = startingReputation;
        OnMoneyChanged += UpdateMoneyText;
        SetMoney(startingMoney);
    }

    /// <summary>
    /// 读当前金钱
    /// </summary>
    public float Money => money;

    /// <summary>
    /// 读当前声望
    /// </summary>
    public float Reputation => reputation;

    /// <summary>
    /// 直接设置金钱（会触发 OnMoneyChanged，当且仅当值发生变化）
    /// </summary>
    public void SetMoney(float newMoney)
    {
        if (Mathf.Approximately(newMoney, money)) return;
        money = newMoney;
        OnMoneyChanged?.Invoke(money);
    }

    /// <summary>
    /// 增加金钱（可为负）
    /// </summary>
    public void AddMoney(float delta)
    {
        SetMoney(money + delta);
    }

    /// <summary>
    /// 尝试消费，成功返回 true 并扣款，否则返回 false（不改变金钱）
    /// </summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (money >= cost)
        {
            SetMoney(money - cost);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 直接设置声望（会触发 OnReputationChanged，当且仅当值发生变化）
    /// </summary>
    public void SetReputation(float newRep)
    {
        if (Mathf.Approximately(newRep, reputation)) return;
        reputation = newRep;
        OnReputationChanged?.Invoke(reputation);
    }

    /// <summary>
    /// 增加声望（可为负）
    /// </summary>
    public void AddReputation(float delta)
    {
        SetReputation(reputation + delta);
    }

    /// <summary>
    /// 重置为起始值
    /// </summary>
    public void ResetEconomy()
    {
        SetMoney(startingMoney);
        SetReputation(startingReputation);
    }

    /// <summary>
    /// 更新金钱显示文本
    /// </summary>
    private void UpdateMoneyText(float newMoney)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{newMoney:F0}"; // 格式化为整数显示
        }
    }
}


