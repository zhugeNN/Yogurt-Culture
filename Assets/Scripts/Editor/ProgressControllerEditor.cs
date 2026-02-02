using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

[CustomEditor(typeof(ProgressController))]
public class ProgressControllerEditor : Editor
{
    private SerializedProperty sliderPrefabsProperty;
    private SerializedProperty loadedSlidersProperty;
    private SerializedProperty sliderContainerProperty;
    private SerializedProperty spawnPointProperty;

    private void OnEnable()
    {
        sliderPrefabsProperty = serializedObject.FindProperty("sliderPrefabs");
        loadedSlidersProperty = serializedObject.FindProperty("loadedSliders");
        sliderContainerProperty = serializedObject.FindProperty("sliderContainer");
        spawnPointProperty = serializedObject.FindProperty("spawnPoint");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ProgressController controller = (ProgressController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Slider Prefab列表", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("添加Prefab后会自动生成对应的Enum类型", MessageType.Info);

        // 确保属性字段可以正常展开和添加元素
        if (sliderPrefabsProperty != null)
        {
            EditorGUILayout.PropertyField(sliderPrefabsProperty, new GUIContent("Slider Prefabs"), true);
        }

        // 检查prefab列表变化并生成enum
        if (GUILayout.Button("生成/更新 Enum类型", GUILayout.Height(30)))
        {
            GenerateEnumFromPrefabs(controller);
        }

        EditorGUILayout.Space();
        // 容器与生成点设置
        EditorGUILayout.LabelField("容器与生成点", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sliderContainerProperty, new GUIContent("Slider Container"));
        if (spawnPointProperty != null)
        {
            EditorGUILayout.PropertyField(spawnPointProperty, new GUIContent("Spawn Point"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("已加载的Slider列表", EditorStyles.boldLabel);
        if (loadedSlidersProperty != null)
        {
            EditorGUILayout.PropertyField(loadedSlidersProperty, new GUIContent("Loaded Sliders"), true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新所有Slider", GUILayout.Height(25)))
        {
            controller.RefreshSliders();
            EditorUtility.SetDirty(controller);
        }
        if (GUILayout.Button("清空所有Slider", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要清空所有Slider吗？", "确定", "取消"))
            {
                controller.ClearAllSliders();
                EditorUtility.SetDirty(controller);
            }
        }
        EditorGUILayout.EndHorizontal();

        // 快速添加Slider按钮
        if (controller.GetPrefabList() != null && controller.GetPrefabList().Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("快速添加Slider", EditorStyles.boldLabel);
            var prefabs = controller.GetPrefabList();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] == null) continue;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i}: {prefabs[i].name}", GUILayout.Width(150));
                if (GUILayout.Button("添加", GUILayout.Width(60)))
                {
                    controller.AddSliderByIndex(i);
                    EditorUtility.SetDirty(controller);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void GenerateEnumFromPrefabs(ProgressController controller)
    {
        if (controller.GetPrefabList() == null || controller.GetPrefabList().Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先添加Slider Prefab", "确定");
            return;
        }

        var validPrefabs = controller.GetPrefabList().Where(p => p != null).ToList();
        if (validPrefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "Prefab列表中所有项都为空，请添加有效的Prefab", "确定");
            return;
        }

        string enumPath = "Assets/Scripts/YogurtGame/ProgressControllerOperationType.cs";
        string enumContent = "// 此文件由ProgressControllerEditor自动生成，请勿手动修改\n";
        enumContent += "// 如需修改，请在ProgressController的Inspector中更新Prefab列表后重新生成\n\n";
        enumContent += "public enum ProgressControllerOperationType\n";
        enumContent += "{\n";

        for (int i = 0; i < validPrefabs.Count; i++)
        {
            string prefabName = validPrefabs[i].name;
            // 清理名称，使其符合C#命名规范
            string enumName = CleanNameForEnum(prefabName);
            
            enumContent += $"    {enumName}";
            if (i < validPrefabs.Count - 1)
                enumContent += ",";
            enumContent += "\n";
        }

        enumContent += "}\n";

        // 确保目录存在
        string directory = Path.GetDirectoryName(enumPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(enumPath, enumContent);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("成功", $"Enum已生成到: {enumPath}\n共生成 {validPrefabs.Count} 个枚举值", "确定");
    }

    private string CleanNameForEnum(string name)
    {
        // 移除(Clone)后缀
        name = name.Replace("(Clone)", "").Trim();
        
        // 移除空格和特殊字符，转换为PascalCase
        string[] parts = name.Split(new char[] { ' ', '_', '-', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
        string result = "";
        foreach (string part in parts)
        {
            if (part.Length > 0)
            {
                result += char.ToUpper(part[0]) + (part.Length > 1 ? part.Substring(1) : "");
            }
        }
        
        // 确保以字母开头
        if (result.Length == 0 || !char.IsLetter(result[0]))
        {
            result = "Slider" + result;
        }
        
        return result;
    }
}

