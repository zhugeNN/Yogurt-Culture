using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Linq;
namespace Excel2Unity
{
    public class ExcelSOUpdater
    {
        public static void UpdateSO(ExcelXmlConfig xmlConfig, List<Dictionary<string, object>> tableData)
        {
#if UNITY_EDITOR

            if (xmlConfig == null || tableData == null) return;
            if (string.IsNullOrEmpty(xmlConfig.SOPath)) return;
            Type soType = Excel2UnityUtils.TryGetType(xmlConfig.SOClassName);
            if (soType == null) return;
            foreach (var data in tableData)
            {
                ScriptableObject so = null;

                var AssetPath = data["AssetPath"];
                string AssetName = Path.GetFileNameWithoutExtension((string)AssetPath) ?? null;
                if (!string.IsNullOrEmpty(AssetName))
                {
                    Excel2UnityUtils.Log("尝试更新SO文件:" + AssetName);
                    string soRelePath = Path.Combine("Assets/Resources", ExcelConfig.SOLoadBasePath)
                    + xmlConfig.SOPath + AssetName + ".asset";
                    string soAbPath = Path.Combine(ExcelConfig.AssetsPath, "Resources", ExcelConfig.SOLoadBasePath)
                   + xmlConfig.SOPath + AssetName + ".asset";
                    if (!File.Exists(soAbPath))
                    {
                        // 新建 SO 资源
                        so = ScriptableObject.CreateInstance(soType);
                        UnityEditor.AssetDatabase.CreateAsset(so, soRelePath);
                    }
                    else
                        so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(soRelePath);
                    if (!so) continue;
                    foreach (var pair in data)
                    {
                        FieldInfo fieldInfo = soType.GetField(pair.Key);
                        if (fieldInfo != null)
                        {
                            // 更新属性值
                            //UnityEngine.Debug.Log($"更新属性名{pair.Key}，\n属性值{pair.Value}");
                            fieldInfo.SetValue(so, pair.Value);
                        }
                    }
                    Excel2UnityUtils.Log("更新SO文件完成:" + AssetName);

                    EditorUtility.SetDirty(so);
                }
            }
            // 保存 SO 资源
            AssetDatabase.SaveAssets();
#endif
        }

        public static void UpdateSOAsync(ExcelXmlConfig xmlConfig, List<Dictionary<string, object>> tableData)
        {
#if UNITY_EDITOR

            if (xmlConfig == null || tableData == null) return;
            if (string.IsNullOrEmpty(xmlConfig.SOPath)) return;
            Type soType = Excel2UnityUtils.TryGetType(xmlConfig.SOClassName);
            if (soType == null) return;

            // 收集所有应该在的SO文件名
            HashSet<string> expectedSONames = new HashSet<string>();
            foreach (var data in tableData)
            {
                var AssetPath = data["AssetPath"];
                string AssetName = Path.GetFileNameWithoutExtension((string)AssetPath) ?? null;
                if (!string.IsNullOrEmpty(AssetName))
                {
                    expectedSONames.Add(AssetName);
                }
            }

            // 清理不在数据表中的SO文件
            CleanOrphanedSOFiles(xmlConfig, expectedSONames);


            foreach (var data in tableData)
            {
                var AssetPath = data["AssetPath"];
                string AssetName = Path.GetFileNameWithoutExtension((string)AssetPath) ?? null;
                if (!string.IsNullOrEmpty(AssetName))
                {
                    string soRelePath = Path.Combine("Assets/Resources", ExcelConfig.SOLoadBasePath)
                    + xmlConfig.SOPath + AssetName + ".asset";
                    string soAbPath = Path.Combine(ExcelConfig.AssetsPath, "Resources", ExcelConfig.SOLoadBasePath)
                   + xmlConfig.SOPath + AssetName + ".asset";
                    Debug.Log(soAbPath);
                    bool isExist = File.Exists(soAbPath);
                    Action<ScriptableObject> loadOverAction = (so) =>
                    {
                        if (!so) return;

                        // 自动设置Sprite属性
                        FieldInfo spriteField = soType.GetField("Sprite");
                        if (spriteField != null && spriteField.FieldType == typeof(Sprite))
                        {
                            // 去掉SO文件名的前缀来获取Sprite文件名
                            string spriteFileName = AssetName;
                            if (spriteFileName.StartsWith("P_"))
                            {
                                spriteFileName = spriteFileName.Substring(2);
                                string spritePath = $"Assets/Art/Sprites/PixelAsset/Prop/{spriteFileName}.png";
                                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                                if (sprite != null)
                                {
                                    spriteField.SetValue(so, sprite);
                                }
                                else
                                {
                                    Excel2UnityUtils.LogWarning($"找不到Sprite资源: {spritePath}");
                                }
                            }
                            else if (spriteFileName.StartsWith("T_"))
                            {
                                spriteFileName = spriteFileName.Substring(2);
                                string spritePath = $"Assets/Art/Sprites/PixelAsset/Tower/{spriteFileName}.png";
                                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                                if (sprite != null)
                                {
                                    spriteField.SetValue(so, sprite);
                                }
                                else
                                {
                                    Excel2UnityUtils.LogWarning($"找不到Sprite资源: {spritePath}");
                                }
                            }
                            else
                                Excel2UnityUtils.LogWarning($"未定义的SO文件名: {AssetName}");


                        }

                        // 自动设置Prefab属性
                        if (AssetName.StartsWith("P_"))
                        {
                            FieldInfo prefabField = soType.GetField("GenePrefab");
                            string prefabPath = $"Assets/Resources/Prefabs/Gene/{AssetName}.prefab";
                            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab != null)
                            {
                                prefabField.SetValue(so, prefab);
                            }
                            else
                            {
                                Excel2UnityUtils.LogWarning($"找不到Prefab资源: {prefabPath}");
                            }
                        }
                        else if (AssetName.StartsWith("T_"))
                        {
                            FieldInfo prefabField = soType.GetField("TowerPrefab");
                            string prefabPath = $"Assets/Resources/Prefabs/Towers/{AssetName}.prefab";
                            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab != null)
                            {
                                prefabField.SetValue(so, prefab);
                            }
                            else
                            {
                                Excel2UnityUtils.LogWarning($"找不到Prefab资源: {prefabPath}");
                            }
                        }

                        foreach (var pair in data)
                        {
                            FieldInfo fieldInfo = soType.GetField(pair.Key);
                            if (fieldInfo != null)
                            {
                                //UnityEngine.Debug.Log($"更新属性名{pair.Key}，\n属性值{pair.Value}");
                                fieldInfo.SetValue(so, pair.Value);

                            }
                        }
                        //Excel2UnityUtils.Log("更新SO文件完成:" + AssetName);
                        EditorUtility.SetDirty(so);
                    };
                    try
                    {
                        if (isExist)
                            LoadScriptableObjectAsync(soRelePath, loadOverAction);
                        else
                            CreateScriptableObjectAsync(soRelePath, soType, loadOverAction);
                    }
                    catch (Exception e)
                    {
                        var actionStr = isExist ? "加载" : "创建";
                        Excel2UnityUtils.LogWarning($"{actionStr}SO文件失败:" + e.Message);
                    }
                }
            }
            // 这里实际应该等待SO更新回调，再保存
            // 似乎SO更改不需要手动保存，所以即使注释掉该行上面的修改也能应用
            EditorApplication.delayCall += () => AssetDatabase.SaveAssets();
            //AssetDatabase.SaveAssets();
#endif
        }


        // 清理不在数据表中的SO文件
        private static void CleanOrphanedSOFiles(ExcelXmlConfig xmlConfig, HashSet<string> expectedSONames)
        {
#if UNITY_EDITOR
            try
            {
                // 构建SO目录的相对路径（Unity格式）
                string soRelativeDirectory = Path.Combine("Assets", "Resources", ExcelConfig.SOLoadBasePath, xmlConfig.SOPath);
                soRelativeDirectory = soRelativeDirectory.Replace('\\', '/');

                // 构建SO目录的完整物理路径
                string soFullDirectory = Path.Combine(ExcelConfig.AssetsPath, "Resources", ExcelConfig.SOLoadBasePath, xmlConfig.SOPath);
                soFullDirectory = soFullDirectory.Replace('\\', '/');

                if (!Directory.Exists(soFullDirectory))
                    return;

                // 获取目录下所有的.asset文件
                string[] allSOFiles = Directory.GetFiles(soFullDirectory, "*.asset", SearchOption.AllDirectories);

                foreach (string soFilePath in allSOFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(soFilePath);

                    // 如果文件名不在预期的SO名称集合中，则删除
                    if (!expectedSONames.Contains(fileName))
                    {
                        // 转换为Unity相对路径
                        string relativePath = soFilePath.Replace('\\', '/');
                        if (relativePath.StartsWith(ExcelConfig.AssetsPath.Replace('\\', '/')))
                        {
                            relativePath = relativePath.Substring(ExcelConfig.AssetsPath.Length);
                            if (relativePath.StartsWith("/"))
                            {
                                relativePath = relativePath.Substring(1);
                            }

                            // 确保是有效的Unity资源路径
                            if (relativePath.StartsWith("Resources/") || relativePath.StartsWith("Assets/"))
                            {
                                // 如果还没有Assets/前缀，加上它
                                if (!relativePath.StartsWith("Assets/"))
                                {
                                    relativePath = "Assets/" + relativePath;
                                }

                                Excel2UnityUtils.Log($"准备删除: {relativePath}");
                                AssetDatabase.DeleteAsset(relativePath);
                                Excel2UnityUtils.Log($"删除孤立的SO文件: {relativePath}");
                            }
                            else
                            {
                                Excel2UnityUtils.LogWarning($"无效的Unity资源路径: {relativePath}");
                            }
                        }
                        else
                        {
                            Excel2UnityUtils.LogWarning($"文件不在Assets目录内: {soFilePath}");
                        }
                    }
                }

                // 删除空目录
                DeleteEmptyDirectories(soRelativeDirectory);

                // 刷新AssetDatabase以确保删除操作生效
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Excel2UnityUtils.LogWarning($"清理SO文件时出错: {e.Message}");
            }
#endif
        }

        // 递归删除空目录
        private static void DeleteEmptyDirectories(string relativeDirectoryPath)
        {
#if UNITY_EDITOR
            try
            {
                // 转换为完整路径来检查目录内容
                string fullDirectoryPath = Path.Combine(ExcelConfig.AssetsPath, relativeDirectoryPath.Replace("Assets/", ""));
                fullDirectoryPath = fullDirectoryPath.Replace('\\', '/');

                if (!Directory.Exists(fullDirectoryPath))
                    return;

                // 先递归处理子目录
                string[] subDirectories = Directory.GetDirectories(fullDirectoryPath);
                foreach (string subDir in subDirectories)
                {
                    string subDirName = Path.GetFileName(subDir);
                    string subRelativePath = Path.Combine(relativeDirectoryPath, subDirName).Replace('\\', '/');
                    DeleteEmptyDirectories(subRelativePath);
                }

                // 检查当前目录是否为空
                string[] files = Directory.GetFiles(fullDirectoryPath);
                string[] directories = Directory.GetDirectories(fullDirectoryPath);

                // 如果目录为空或者只包含.meta文件
                bool hasNonMetaFiles = files.Any(f => !f.EndsWith(".meta"));
                bool hasSubDirectories = directories.Length > 0;

                if (!hasNonMetaFiles && !hasSubDirectories)
                {
                    // 目录为空，可以删除
                    AssetDatabase.DeleteAsset(relativeDirectoryPath);
                    Excel2UnityUtils.Log($"删除空目录: {relativeDirectoryPath}");
                }
            }
            catch (Exception e)
            {
                Excel2UnityUtils.LogWarning($"删除空目录时出错: {e.Message}");
            }
#endif
        }
        private static void CreateScriptableObjectAsync(string soRelePath, Type soType, Action<ScriptableObject> onLoaded)
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
           {
               var so = ScriptableObject.CreateInstance(soType);
               AssetDatabase.CreateAsset(so, soRelePath);
               onLoaded?.Invoke(so);
           };
#endif
        }

        private static void LoadScriptableObjectAsync(string path, Action<ScriptableObject> onLoaded)
        {
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                onLoaded?.Invoke(so);
            };
#endif
        }
    }
}