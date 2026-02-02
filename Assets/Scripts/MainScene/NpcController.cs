using System.Collections;
using UnityEngine;

public class NpcController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 2f; // 默认移动速度
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    
    private Animator animator;
    private bool isWalking = true; // 当前是否在行走状态
    private Coroutine stateCheckCoroutine; // 状态判定协程
    private float currentSpeed;
    private Vector3 currentDirection = Vector3.right;
    public SpriteRenderer sprite;
    
    // Animator参数名称
    private const string ANIM_WALK = "IsWalking"; // 根据你的Animator参数名称调整
    
    [Header("购买控制")]
    [SerializeField] private GameObject yogurtShop;
    [SerializeField] private Transform queueParent;
    [SerializeField] private Transform windowTransform;
    [SerializeField] private float queueStepOffset = -0.5f;
    [Header("口味期望范围")]
    [Tooltip("NPC 对口味的最小期望值（包含）")]
    [SerializeField] private int flavorMin = 0;
    [Tooltip("NPC 对口味的最大期望值（包含）")]
    [SerializeField] private int flavorMax = 2;

    private bool isQueued;
    private bool isMovingToQueue;
    private bool isInQueueMode;
    private Vector3 queueTargetPosition;
    private float preQueueSpeed;
    private Vector3 preQueueDirection;
    private bool preQueueWasWalking;
    private bool queueArrivalShouldAddOrder;

    // 折返点设置
    private float turnaroundPointX = float.MaxValue; // 默认值表示没有折返点

    private void Awake()
    {
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        currentSpeed = moveSpeed;
        currentDirection = moveDirection == Vector3.zero ? Vector3.right : moveDirection.normalized;
    }
    
    private void OnEnable()
    {
        InitializeController();
    }
    
    private void OnDisable()
    {
        if (stateCheckCoroutine != null)
        {
            StopCoroutine(stateCheckCoroutine);
            stateCheckCoroutine = null;
        }
    }
    
    private void Update()
    {
        if (isMovingToQueue)
        {
            MoveTowardsQueuePoint();
            return;
        }

        // 在walk状态时沿设定方向移动
        if (isWalking)
        {
            transform.Translate(currentDirection * currentSpeed * Time.deltaTime);

            // 检查是否到达折返点
            CheckTurnaroundPoint();
        }
    }
    
    public void ConfigureMovement(float speed, Vector3 direction)
    {
        currentSpeed = moveSpeed > 0f ? moveSpeed : speed;
        currentDirection = direction == Vector3.zero ? Vector3.right : direction.normalized;
    }
    
    private void InitializeController()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                // Debug.LogError("NpcController: 未找到Animator组件！");
                enabled = false;
                return;
            }
        }
        
        if (!isInQueueMode)
        {
            SetWalkState(true);
            StartStateCheck();
        }
    }
    #region 行走控制
    /// <summary>
    /// 设置walk状态
    /// </summary>
    private void SetWalkState(bool walking)
    {
        isWalking = walking;
        if (animator != null)
        {
            animator.SetBool(ANIM_WALK, walking);
        }
    }
    
    /// <summary>
    /// 启动状态判定协程
    /// </summary>
    private void StartStateCheck()
    {
        if (isInQueueMode)
        {
            return;
        }

        if (stateCheckCoroutine != null)
        {
            StopCoroutine(stateCheckCoroutine);
        }
        
        stateCheckCoroutine = StartCoroutine(isWalking ? WalkStateCheck() : IdleStateCheck());
    }
    
    /// <summary>
    /// Walk状态判定协程
    /// </summary>
    private IEnumerator WalkStateCheck()
    {
        // 等待3秒
        yield return new WaitForSeconds(3f);
        
        // 之后每1秒判定一次
        while (isWalking)
        {
            yield return new WaitForSeconds(1f);
            
            // 10%概率进入idle
            if (Random.Range(0f, 1f) < 0.1f)
            {
                SetWalkState(false);
                StartStateCheck(); // 重新启动状态判定
                yield break;
            }
        }
    }
    
    /// <summary>
    /// Idle状态判定协程
    /// </summary>
    private IEnumerator IdleStateCheck()
    {
        // 等待2秒
        yield return new WaitForSeconds(2f);
        
        // 之后每1秒判定一次
        while (!isWalking)
        {
            yield return new WaitForSeconds(1f);
            
            // 50%概率进入walk
            if (Random.Range(0f, 1f) < 0.5f)
            {
                SetWalkState(true);
                StartStateCheck(); // 重新启动状态判定
                yield break;
            }
        }
    }
    #endregion
    public void ConfigureQueueTargets(GameObject shop, Transform queue, Transform window, float stepOffset)
    {
        yogurtShop = shop;
        queueParent = queue;
        windowTransform = window;
        queueStepOffset = stepOffset;
    }

    /// <summary>
    /// NPC 的口味期望最小值（供 OrderManager 使用）
    /// </summary>
    public int FlavorMin => Mathf.Min(flavorMin, flavorMax);

    /// <summary>
    /// NPC 的口味期望最大值（供 OrderManager 使用）
    /// </summary>
    public int FlavorMax => Mathf.Max(flavorMin, flavorMax);

    #region 购买控制

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Debug.Log("enter");
        if (yogurtShop != null && other.gameObject == yogurtShop)
        {
            TryEnterQueue();
        }
    }

    private void TryEnterQueue()
    {
        if (isQueued || queueParent == null)
        {
            return;
        }

        // 先通过 NpcManager 申请队列名额（含全局概率控制）
        if (!NpcManager.Instance.EnterQueue(gameObject))
        {
            return;
        }

        preQueueSpeed = currentSpeed;
        preQueueDirection = currentDirection;
        preQueueWasWalking = isWalking;

        StopStateTracking();
        isQueued = true;
        isInQueueMode = true;
        transform.SetParent(queueParent, true);
        queueArrivalShouldAddOrder = true;

        int queueCount = queueParent.childCount;
        float targetX = windowTransform != null ? windowTransform.position.x + queueStepOffset * queueCount : transform.position.x;
        float targetY = windowTransform != null ? windowTransform.position.y : transform.position.y;
        queueTargetPosition = new Vector3(targetX, targetY, transform.position.z);

        currentDirection = (queueTargetPosition - transform.position).sqrMagnitude > 0.001f
            ? (queueTargetPosition - transform.position).normalized
            : Vector3.right;

        SetWalkState(true);
        isMovingToQueue = true;
    }

    private void MoveTowardsQueuePoint()
    {
        if (!isMovingToQueue)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, queueTargetPosition, currentSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, queueTargetPosition) <= 0.01f)
        {
            transform.position = queueTargetPosition;
            isMovingToQueue = false;
            SetWalkState(false);
            if (sprite != null)
            {
                sprite.flipX = true;
            }
            if (queueArrivalShouldAddOrder)
            {
                queueArrivalShouldAddOrder = false;
                OrderManager.Instance.AddOrder(this);
            }
        }
    }

    private void StopStateTracking()
    {
        if (stateCheckCoroutine != null)
        {
            StopCoroutine(stateCheckCoroutine);
            stateCheckCoroutine = null;
        }
    }

    public void LeaveQueueAndResume()
    {
        isQueued = false;
        isInQueueMode = false;
        isMovingToQueue = false;

        currentSpeed = preQueueSpeed > 0f ? preQueueSpeed : moveSpeed;
        currentDirection = preQueueDirection == Vector3.zero ? moveDirection.normalized : preQueueDirection;

        if (sprite != null)
        {
            sprite.flipX = currentDirection.x < 0f;
        }

        if (preQueueWasWalking)
        {
            SetWalkState(true);
            StartStateCheck();
        }
        else
        {
            SetWalkState(false);
        }
    }

    /// <summary>
    /// 设置折返点X坐标
    /// </summary>
    /// <param name="turnaroundX">折返点X坐标</param>
    public void SetTurnaroundPoint(float turnaroundX)
    {
        turnaroundPointX = turnaroundX;
    }

    /// <summary>
    /// 检查是否到达折返点，如果到达则反向移动
    /// </summary>
    private void CheckTurnaroundPoint()
    {
        // 只在向右移动且有折返点设置时检查
        if (currentDirection.x > 0 && turnaroundPointX < float.MaxValue)
        {
            if (transform.position.x >= turnaroundPointX)
            {
                // 到达折返点，反向移动（向左）
                currentDirection = Vector3.left;

                // 翻转精灵
                if (sprite != null)
                {
                    sprite.flipX = true;
                }
            }
        }
    }

    /// <summary>
    /// 离开队列并立即向左移动
    /// </summary>
    public void LeaveQueueAndGoLeft()
    {
        isQueued = false;
        isInQueueMode = false;
        isMovingToQueue = false;

        // 设置向左移动
        currentSpeed = moveSpeed;
        currentDirection = Vector3.left;

        if (sprite != null)
        {
            sprite.flipX = true; // 向左移动时翻转精灵
        }

        SetWalkState(true);
        StartStateCheck();
    }

    public void ShiftInQueue(float distance, float speed)
    {
        if (!isQueued)
        {
            return;
        }

        isInQueueMode = true;
        isMovingToQueue = true;
        queueArrivalShouldAddOrder = false;
        
        queueTargetPosition = new Vector3(
            transform.position.x + distance,
            transform.position.y,
            transform.position.z);

        currentDirection = (queueTargetPosition - transform.position).sqrMagnitude > 0.001f
            ? (queueTargetPosition - transform.position).normalized
            : Vector3.right;

        currentSpeed = Mathf.Max(speed, 0f);
        SetWalkState(true);
    }
    #endregion
}
