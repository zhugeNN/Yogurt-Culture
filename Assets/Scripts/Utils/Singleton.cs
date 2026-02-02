using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;
    private static object _lock = new object();
    private static bool isQuitting = false; // *** 修改：用 OnApplicationQuit 来设置 ***

    public static T Instance
    {
        get
        {
            if (isQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                return null;
            }

            if (instance == null)
            {
                lock (_lock)
                {
                    instance = FindObjectOfType<T>() as T;

                    if (instance == null)
                    {
                        GameObject go = new GameObject(typeof(T).Name);
                        instance = go.AddComponent<T>();
                    }
                }
            }
            return instance;
        }
    }

    [SerializeField] protected bool dontDestroyOnLoad = false;

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = (T)this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // *** 这是关键：如果我是重复的，我只销毁自己，不碰任何静态变量 ***
            Destroy(gameObject);
        }
    }

    // *** 新增：使用 OnApplicationQuit 来处理游戏退出 ***
    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // *** 修改：OnDestroy 应该只在“自己是那个单例”时才清理 instance ***
    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        // *** 删掉了 isQuitting = true; ***
    }
}