using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OrderManager : Singleton<OrderManager>
{
    public class Order
    {
        public string ID;
        public GameObject OrderEntity; // Prefab reference
        public GameObject SpawnedInstance; // 实例化后的实体
        public List<Type> IngredientTypes = new();
        public int Price = 10;
        public int FlavorExpec;
    }
    private Queue<Order> activeOrders = new();
    private Order currentOrder;
    [SerializeField]
    private List<GameObject> orderPrefabs = new();
    [SerializeField]
    private Transform OrderPos;
    [SerializeField]
    private Transform OrderRoot;
    protected override void Awake()
    {
        base.Awake();
        currentOrder = null;
    }
    public Order AddOrder(NpcController sourceNpc = null)
    {
        if (orderPrefabs == null || orderPrefabs.Count == 0)
        {
            // Debug.LogWarning("OrderManager: 没有可用的订单 prefab。");
            return null;
        }

        Order newOrder = new Order();
        int randomIndex = UnityEngine.Random.Range(0, orderPrefabs.Count);
        newOrder.OrderEntity = orderPrefabs[randomIndex];
        newOrder.ID = newOrder.OrderEntity.name;

        newOrder.IngredientTypes = OrderDemands();
        // 为订单分配随机 FlavorExpec（若提供 NPC 则使用 NPC 中的上下限）
        if (sourceNpc != null)
        {
            AssignRandomFlavorExpectation(newOrder, sourceNpc.FlavorMin, sourceNpc.FlavorMax);
        }
        else
        {
            AssignRandomFlavorExpectation(newOrder);
        }

        activeOrders.Enqueue(newOrder);
        if (currentOrder == null)
        {
            LoadNextOrder();
        }
        return newOrder;
    }
    
    /// <summary>
    /// 为指定订单分配一个随机的 FlavorExpec 整数值。
    /// 默认范围为 [0,2]（包含边界）。
    /// </summary>
    /// <param name="order">目标订单</param>
    /// <param name="min">最小值（包含），默认 0</param>
    /// <param name="max">最大值（包含），默认 2</param>
    private void AssignRandomFlavorExpectation(Order order, int min = 0, int max = 2)
    {
        if (order == null) return;
        // Random.Range for ints is min (inclusive) to max (exclusive), so add +1 to include max
        order.FlavorExpec = UnityEngine.Random.Range(min, max + 1);
    }
    private List<Type> OrderDemands()
    {
        return new List<Type> { typeof(NormalYogurt) };
    }
    public void LoadNextOrder()
    { 
        if(activeOrders.Count > 0)
        {
            currentOrder = activeOrders.Dequeue();
            if (currentOrder.OrderEntity != null)
            {
                GameObject entity = Instantiate(currentOrder.OrderEntity, OrderRoot);
                entity.transform.position = OrderPos.position;
                currentOrder.SpawnedInstance = entity;
            }
        }
        else
        {
            currentOrder = null;
        }
    }
    public void HandleOrderSubmit(YogurtProduct submitOrder)
    {
        if (submitOrder == null)
        {
            return;
        }
        // Debug: 打印提交产品的 flavor
        Debug.Log($"[OrderManager] Submitted product flavor: {submitOrder.GetFlavor()}");

        if (MatchOrder(submitOrder))
        {
            SubmitSuccess();
            Destroy(submitOrder.gameObject);
            LoadNextOrder();
        }
        else
        {
            SubmitFail();
        }
    }
    public bool MatchOrder(YogurtProduct submitOrder)
    {
        if (currentOrder == null || submitOrder == null)
        {
            return false;
        }

        List<Type> targetTypes = currentOrder.IngredientTypes ?? new List<Type>();
        List<Type> submittedIngredients = submitOrder.GetIngredientTypes() ?? new List<Type>();

        if (targetTypes.Count != submittedIngredients.Count)
        {
            return false;
        }

        for (int i = 0; i < targetTypes.Count; i++)
        {
            // Ingredient actual = submittedIngredients[i];

            if (targetTypes[i] != submittedIngredients[i])
            {
                return false;
            }
        }

        return true;
    }
    private void SubmitFail()
    {

    }
    private void SubmitSuccess()
    {
        if (currentOrder != null)
        {
            EconomyManager.Instance.AddMoney(currentOrder.Price);
            if (currentOrder.SpawnedInstance != null)
            {
                Destroy(currentOrder.SpawnedInstance);
                currentOrder.SpawnedInstance = null;
            }
            currentOrder = null;
        }

        NpcManager.Instance?.LeaveQueue();
    }
}