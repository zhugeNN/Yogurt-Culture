using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载在 UI 根节点上，用于控制整个 UI 的显示与隐藏。
/// 使用 CanvasGroup 控制可见性和交互性，避免在隐藏时把根对象设为 inactive（这样脚本仍可被其它脚本调用）。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PhoneController : MonoBehaviour {
    [SerializeField]private CanvasGroup _canvasGroup;
    [Header("App主界面")]
    [Tooltip("当前App")]
    [SerializeField] private List<GameObject> AppList = new();
    [Tooltip("界面面板")]
    [SerializeField] private GameObject AppPanel;

    /// <summary>
    /// 显示整个 UI，允许交互
    /// </summary>
    public void ShowUI() {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (_canvasGroup == null) return;
        gameObject.SetActive(true); // 确保根对象为激活状态
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        RepositionApps();
    }

    /// <summary>
    /// 隐藏整个 UI，禁止交互（不将根对象设为 inactive，以便脚本仍可被调用）
    /// </summary>
    public void HideUI() {
        if (_canvasGroup == null) return;
        gameObject.SetActive(false);
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    public void AddApp(GameObject newApp){
        if(newApp != null)
            AppList.Add(newApp);
    }

    /// <summary>
    /// 将 AppList 中的所有预制体设置为 AppPanel 的子对象，并按四列一行的网格排列。
    /// 每个元素之间的水平和垂直间距为 50（单位：RectTransform 单位）。
    /// </summary>
    public void RepositionApps() {
        if (AppPanel == null || AppList == null || AppList.Count == 0) return;

        const int columns = 4;
        const float offset = 50f;

        // 预处理：清理 AppPanel 下的所有子对象，避免重复实例化
        for (int c = AppPanel.transform.childCount - 1; c >= 0; c--) {
            var child = AppPanel.transform.GetChild(c).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                DestroyImmediate(child);
            } else {
                Destroy(child);
            }
#else
            Destroy(child);
#endif
        }

        // 先实例化所有预制体到 AppPanel 下，保存实例用于排列
        var instances = new List<GameObject>(AppList.Count);
        for (int i = 0; i < AppList.Count; i++) {
            var prefab = AppList[i];
            if (prefab == null) continue;

            // 实例化 prefab 到 AppPanel 下（保持本地变换）
            GameObject instance = Instantiate(prefab, AppPanel.transform, false);
            instance.transform.localScale = Vector3.one;
            instances.Add(instance);
        }

        // 按网格排列实例
        for (int i = 0; i < instances.Count; i++) {
            var app = instances[i];
            if (app == null) continue;

            var rect = app.GetComponent<RectTransform>();
            if (rect == null) continue;

            int col = i % columns;
            int row = i / columns;

            // 使用顶部左对齐的锚点与 pivot 来计算 anchoredPosition
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            float x = col * offset;
            float y = -row * offset;

            rect.anchoredPosition = new Vector2(x+12.5f, y-13f);
        }
    }

    /// <summary>
    /// 如果当前 Canvas（此对象或父对象上的 Canvas）处于激活状态，则切换目标 GameObject 的激活状态。
    /// 若找不到 Canvas 或 target 为 null，则不做任何操作。
    /// </summary>
    public void ToggleCanvas() {
        if(_canvasGroup.gameObject.activeInHierarchy){
            HideUI();
        }
        else
            ShowUI();
    }
}


