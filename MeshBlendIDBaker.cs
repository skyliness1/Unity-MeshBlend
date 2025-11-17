using System.Collections;
using System.Collections.Generic;
using ScreenSpaceModelBlend.Scripts.Editor;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace ScreenSpaceModelBlend.Scripts
{
    public class MeshBlendIDBaker
    {
        private class BlendIDBucket
        {
            private List<Bounds> m_boundsList = new List<Bounds>();
            public void Add(Bounds bounds) => m_boundsList.Add(bounds);

            public bool Intersects(Bounds newBounds)
            {
                foreach (var b in m_boundsList)
                {
                    if (b.Intersects(newBounds))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        
        private static Dictionary<byte, BlendIDBucket> m_allBuckets; //{ID:包围盒}池
        private static byte m_currentIdCounter;

        public static void Bake(string outputPath)
        {
            m_allBuckets = new Dictionary<byte, BlendIDBucket>();
            m_currentIdCounter = 1;

            MeshBlendTarget[] targetsToProcess = Object.FindObjectsOfType<MeshBlendTarget>();
            
            if (targetsToProcess.Length == 0)
            {
                EditorUtility.DisplayDialog("警告", "场景中没有找到任何已标记物体，烘焙已取消", "好的");
                return;
            }
            
            Debug.Log($"找到 {targetsToProcess.Length} 个需要烘焙的物体。");
            
            //{GUID:BlendID}字典
            // 使用元组存储ID和名字
            Dictionary<string, (byte, string)> bakedIDMap = new Dictionary<string, (byte, string)>(); 

            foreach (var target in targetsToProcess)
            {
                var go = target.gameObject;
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;

                string guid = target.GUID;
                string objectName = target.gameObject.name;
                
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"物体 '{target.gameObject.name}' 的MeshBlendTarget组件缺少GUID，已跳过。请尝试移除并重新添加该组件。");
                    continue;
                }
                
                byte assignID = AssignBlendID(worldBounds);

                bakedIDMap[guid] = (assignID, objectName);
            }
            SaveBakedData(bakedIDMap, outputPath);
        }

        private static byte AssignBlendID(Bounds bounds)
        {
            const int maxAttempts = 255;
            for (int i = 0; i < maxAttempts; i++)
            {
                byte candidateID = GetNextID();

                if (!m_allBuckets.ContainsKey(candidateID))
                {
                    m_allBuckets[candidateID] = new BlendIDBucket();
                }

                if (m_allBuckets[candidateID].Intersects(bounds))
                {
                    continue;
                }
                
                m_allBuckets[candidateID].Add(bounds);
                return candidateID;
            }
            
            // 如果所有尝试都失败了，返回一个默认ID并给出警告
            Debug.LogWarning($"无法为位于 {bounds.center} 的包围盒找到一个不冲突的ID。返回默认ID 1。");
            return 1;
        }

        private static byte GetNextID()
        {
            byte id = m_currentIdCounter;
            m_currentIdCounter++;
            if (m_currentIdCounter == 0)
            {
                m_currentIdCounter = 1;
            }

            return id;
        }

        private static void SaveBakedData(Dictionary<string, (byte, string)> map, string absolutePath)
        {
            // 1. 写入.bytes文件 
            string directory = Path.GetDirectoryName(absolutePath);
            Directory.CreateDirectory(directory);
            using (BinaryWriter writer = new BinaryWriter(File.Open(absolutePath, FileMode.Create)))
            {
                writer.Write(map.Count);
                foreach (var kvp in map)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value.Item1);
                    writer.Write(kvp.Value.Item2);
                }
            }
            Debug.Log($"成功烘焙 {map.Count} 个ID到: {absolutePath}");
            AssetDatabase.Refresh();

            // 2. 将绝对路径转换为Unity可以使用的相对路径
            string dataAssetRelativePath = null;
            if (absolutePath.StartsWith(Application.dataPath))
            {
                dataAssetRelativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            else
            {
                Debug.LogError("错误：烘焙数据必须保存在项目的Assets文件夹内！资产绑定已跳过。");
                return;
            }

            TextAsset dataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(dataAssetRelativePath);
            if (dataAsset == null)
            {
                Debug.LogError($"无法加载刚刚创建的数据资产于 '{dataAssetRelativePath}'。请检查路径或权限。");
                return;
            }

            // 3. 自动寻找或创建SO并绑定
            string[] guids = AssetDatabase.FindAssets("t:MeshBlendSettings");
            MeshBlendSettings settings;
            if (guids.Length > 0)
            {
                string settingsPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                settings = AssetDatabase.LoadAssetAtPath<MeshBlendSettings>(settingsPath);
            }
            else
            {
                // 从数据文件的相对路径推导出SO的相对路径
                string settingsRelativePath = Path.Combine(Path.GetDirectoryName(dataAssetRelativePath), "MeshBlendSettings.asset");
                
                settings = ScriptableObject.CreateInstance<MeshBlendSettings>();
                
                // 使用修正后的相对路径来创建资产
                AssetDatabase.CreateAsset(settings, settingsRelativePath);
                Debug.Log($"未找到MeshBlendSettings，已自动创建于: {settingsRelativePath}");
            }

            // 4. 绑定、保存并添加到预加载列表 (逻辑更新)
            settings.bakedDataFile = dataAsset;
            EditorUtility.SetDirty(settings);
    
            // 获取当前列表。如果没有则创建一个空数组，避免null引用。
            var originalAssets = PlayerSettings.GetPreloadedAssets() ?? new Object[0];

            // 查找当前列表中是否已存在我们的资产
            var existingAsset = originalAssets.FirstOrDefault(asset => asset is MeshBlendSettings);

            if (existingAsset != null && existingAsset == settings)
            {
                // 情况1：列表中已经存在，并且就是我们正在处理的这一个。
                // 什么都不用做，直接保存即可。
                Debug.Log("MeshBlendSettings 已在预加载列表中，无需重复添加。");
            }
            else
            {
                // 情况2：列表中不存在，或者存在一个不同的/旧的实例。
                // 创建一个新列表，只包含非MeshBlendSettings的旧资产。
                var newPreloadedAssets = originalAssets.Where(asset => !(asset is MeshBlendSettings)).ToList();
        
                // 将我们当前的这一个实例添加到新列表中。
                newPreloadedAssets.Add(settings);
        
                // 将这个干净、无重复的新列表设置回去。
                PlayerSettings.SetPreloadedAssets(newPreloadedAssets.ToArray());
                Debug.Log("MeshBlendSettings已成功更新到PlayerSettings的预加载列表！");
            }
            
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(settings);
            Selection.activeObject = settings;
        }
    }
}