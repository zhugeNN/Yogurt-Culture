using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NpcManager : Singleton<NpcManager>
{
    [Header("NPC资源")]
    [SerializeField] private List<GameObject> npcPrefabs = new List<GameObject>();
    
    [Header("刷新设定")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(3f, 5f);
    [SerializeField] private Vector2 speedRange = new Vector2(1.4f, 1.8f);
    [SerializeField] private float spawnOffset = 1.5f;
    [SerializeField] private float recyclePadding = 2f;
    
    [Header("层级管理")]
    [SerializeField] private Transform npcRoot;
    [SerializeField] private string npcRootName = "NPC";
    
    [Header("Y轴可视化范围")]
    [SerializeField] private Transform minYMarker;
    [SerializeField] private Transform maxYMarker;
    [SerializeField] private float fallbackMinY = -2f;
    [SerializeField] private float fallbackMaxY = 2f;

    [Header("折返点设置")]
    [Tooltip("NPC从左向右移动时到达此点后会反向行走")]
    [SerializeField] private Transform turnaroundPoint;
    [Header("购买控制")]
    [SerializeField] private GameObject yogurtShop;
    [SerializeField] private Transform queueParent;
    [SerializeField] private Transform windowTransform;
    [SerializeField] private float queueStepOffset = -0.5f;
    [Header("排队概率")]
    [Range(0f, 1f)]
    [SerializeField] private float queueEntryProbability = 0.7f;
    [SerializeField] private Queue<GameObject> queuedNPC = new();
    
    private readonly List<GameObject> activeNpcs = new List<GameObject>();
    private Camera mainCamera;
    private Coroutine spawnRoutine;
    
    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main;
    }
    
    private void Start()
    {
        if (npcPrefabs == null || npcPrefabs.Count == 0)
        {
            // Debug.LogWarning("NpcManager: 未配置NPC预制体。");
            return;
        }
        
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }
    
    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
    
    private void Update()
    {
        RecycleOffScreenNpcs();
    }
    
    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnNpc();
            float wait = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            yield return new WaitForSeconds(wait);
        }
    }
    
    private void SpawnNpc()
    {
        if (npcPrefabs.Count == 0)
        {
            return;
        }
        
        EnsureCamera();
        if (mainCamera == null)
        {
            return;
        }
        
        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];
        if (prefab == null)
        {
            return;
        }
        
        // 所有NPC都从左侧生成
        bool spawnLeft = true;
        float spawnY = GetRandomY();
        Vector3 spawnPos = GetSpawnPosition(spawnLeft);
        spawnPos.y = spawnY;

        GameObject npc = ObjectPool.Instance.GetObject(prefab, spawnPos, Quaternion.identity);
        EnsureNpcRoot();
        if (npcRoot != null)
        {
            npc.transform.SetParent(npcRoot, false);
        }
        if (!activeNpcs.Contains(npc))
        {
            activeNpcs.Add(npc);
        }

        float moveSpeed = Random.Range(speedRange.x, speedRange.y);
        Vector3 direction = Vector3.right; // 从左向右移动

        NpcController controller = npc.GetComponent<NpcController>();
        if (controller != null)
        {
            controller.ConfigureMovement(moveSpeed, direction);
            controller.sprite.flipX = false; // 从左向右移动时不需要翻转
            controller.ConfigureQueueTargets(yogurtShop, queueParent, windowTransform, queueStepOffset);

            // 设置折返点
            if (turnaroundPoint != null)
            {
                controller.SetTurnaroundPoint(turnaroundPoint.position.x);
            }
        }
    }
    /// <summary>
    /// 尝试让指定NPC进入队列（包含统一的概率控制），成功返回 true
    /// </summary>
    public bool EnterQueue(GameObject npc)
    {
        if (npc == null)
        {
            return false;
        }

        // 统一在管理器里控制进入队列的概率
        if (Random.value > queueEntryProbability)
        {
            return false;
        }

        queuedNPC.Enqueue(npc);
        return true;
    }
    public void LeaveQueue()
    {
        if (queuedNPC.Count == 0)
        {
            return;
        }

        EnsureNpcRoot();

        GameObject leavingNpc = queuedNPC.Dequeue();
        if (leavingNpc != null)
        {
            if (npcRoot != null)
            {
                leavingNpc.transform.SetParent(npcRoot, true);
            }
            else
            {
                leavingNpc.transform.SetParent(null, true);
            }

            NpcController controller = leavingNpc.GetComponent<NpcController>();
            if (controller != null)
            {
                // 让NPC立即向左离开
                controller.LeaveQueueAndGoLeft();
            }
        }

        float shiftDistance = -queueStepOffset;
        foreach (GameObject npc in queuedNPC)
        {
            if (npc == null) continue;
            NpcController ctrl = npc.GetComponent<NpcController>();
            if (ctrl != null)
            {
                ctrl.ShiftInQueue(shiftDistance, 0.2f);
            }
        }
    }
    private void RecycleOffScreenNpcs()
    {
        EnsureCamera();
        if (mainCamera == null || activeNpcs.Count == 0)
        {
            return;
        }
        
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float leftLimit = mainCamera.transform.position.x - halfWidth - recyclePadding;
        float rightLimit = mainCamera.transform.position.x + halfWidth + recyclePadding;
        
        for (int i = activeNpcs.Count - 1; i >= 0; i--)
        {
            GameObject npc = activeNpcs[i];
            if (npc == null || !npc.activeSelf)
            {
                activeNpcs.RemoveAt(i);
                continue;
            }
            
            float npcX = npc.transform.position.x;
            if (npcX < leftLimit || npcX > rightLimit)
            {
                ObjectPool.Instance.PushObject(npc);
                activeNpcs.RemoveAt(i);
            }
        }
    }
    
    private void EnsureCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Debug.LogWarning("NpcManager: 找不到主摄像机。");
            }
        }
    }
    
    private void EnsureNpcRoot()
    {
        if (npcRoot == null)
        {
            GameObject root = GameObject.Find(npcRootName);
            if (root == null)
            {
                root = new GameObject(npcRootName);
            }
            npcRoot = root.transform;
        }
    }
    
    private Vector3 GetSpawnPosition(bool spawnLeft)
    {
        EnsureCamera();
        if (mainCamera == null)
        {
            return Vector3.zero;
        }
        
        if (mainCamera.orthographic)
        {
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            float x = spawnLeft
                ? mainCamera.transform.position.x - halfWidth - spawnOffset
                : mainCamera.transform.position.x + halfWidth + spawnOffset;
            return new Vector3(x, mainCamera.transform.position.y, 0f);
        }
        else
        {
            float z = Mathf.Abs(mainCamera.transform.position.z);
            Vector3 viewportPoint = new Vector3(spawnLeft ? 0f : 1f, 0.5f, z);
            Vector3 world = mainCamera.ViewportToWorldPoint(viewportPoint);
            world.x += spawnLeft ? -spawnOffset : spawnOffset;
            world.z = 0f;
            return world;
        }
    }
    
    private float GetRandomY()
    {
        float minY = minYMarker ? minYMarker.position.y : fallbackMinY;
        float maxY = maxYMarker ? maxYMarker.position.y : fallbackMaxY;
        if (minY > maxY)
        {
            float temp = minY;
            minY = maxY;
            maxY = temp;
        }
        return Random.Range(minY, maxY);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        float minY = minYMarker ? minYMarker.position.y : fallbackMinY;
        float maxY = maxYMarker ? maxYMarker.position.y : fallbackMaxY;
        Vector3 center = transform.position;
        Gizmos.DrawLine(new Vector3(center.x - 10f, minY, 0f), new Vector3(center.x + 10f, minY, 0f));
        Gizmos.DrawLine(new Vector3(center.x - 10f, maxY, 0f), new Vector3(center.x + 10f, maxY, 0f));
    }
}

