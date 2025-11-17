using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScreenSpaceModelBlend.Scripts.Editor;
using UnityEditor;
using UnityEngine;

namespace ScreenSpaceModelBlend.Scripts
{
    public class MeshBlendInitializer
    {
        private static Dictionary<string, (byte id, string name)> m_bakedData;
        private static MaterialPropertyBlock m_propertyBlock;
        private static int m_blendIDShaderPropertyID;
        private static bool m_isInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
            if (m_isInitialized) return;

            m_propertyBlock = new MaterialPropertyBlock();
            m_blendIDShaderPropertyID = Shader.PropertyToID("_BlendID");
            
            LoadBakedData();
            //ApplyBlendIDsToAllSceneObjects();
            ApplyBlendIDsToEachSceneObjects(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            
            m_isInitialized = true;
            Debug.Log("MeshBlend: 自动初始化完成。");
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (m_bakedData == null || m_bakedData.Count == 0)
            {
                LoadBakedData();
            }
            
            //ApplyBlendIDsToAllSceneObjects();
            ApplyBlendIDsToEachSceneObjects(scene);
        }

        private static void LoadBakedData()
        {
            m_bakedData = new Dictionary<string, (byte id, string name)>();

            // 1. 从预加载资产中直接查找我们的Settings对象
            var allPreloadedAssets = PlayerSettings.GetPreloadedAssets();
            MeshBlendSettings settings = allPreloadedAssets.FirstOrDefault(asset => asset is MeshBlendSettings) as MeshBlendSettings;

            if (settings == null || settings.bakedDataFile == null)
            {
#if UNITY_EDITOR
                Debug.LogError("MeshBlend运行时错误: 未在PlayerSettings的预加载列表中找到MeshBlendSettings，或其数据文件未配置。请尝试重新烘焙一次。");
#else
        Debug.LogError("MeshBlend运行时错误: 关键配置缺失。");
#endif
                return;
            }

            // 2. 从TextAsset中获取二进制数据
            byte[] assetBytes = settings.bakedDataFile.bytes;

            // 3. 使用MemoryStream从内存中读取，而不是从文件路径
            using (MemoryStream stream = new MemoryStream(assetBytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string guid = reader.ReadString();
                    byte blendId = reader.ReadByte();
                    string objectName = reader.ReadString();
                    m_bakedData[guid] = (blendId, objectName);
                }
            }
        }

        //全量更新式分配
        private static void ApplyBlendIDsToAllSceneObjects()
        {
            if (m_bakedData == null || m_bakedData.Count == 0)
            {
                return;
            }

            var sceneTargets = Object.FindObjectsOfType<MeshBlendTarget>();

            foreach (var target in sceneTargets)
            {
                if (m_bakedData.TryGetValue(target.GUID, out var data))
                {
                    var renderer = target.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(m_propertyBlock);
                        m_propertyBlock.SetFloat(m_blendIDShaderPropertyID, data.id);
                        renderer.SetPropertyBlock(m_propertyBlock);
                    }
                }
            }
            Debug.Log($"MeshBlend: 已为场景 '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' 中的 {sceneTargets.Length} 个已标记物体应用了混合ID。");
        }
        
        //增量更新式分配
        private static void ApplyBlendIDsToEachSceneObjects(UnityEngine.SceneManagement.Scene scene)
        {
            if (m_bakedData == null || m_bakedData.Count == 0)
            {
                return;
            }

            var rootObjects = scene.GetRootGameObjects();
            List<MeshBlendTarget> targetsInScene = new List<MeshBlendTarget>();

            foreach (var go in rootObjects)
            {
                targetsInScene.AddRange(go.GetComponentsInChildren<MeshBlendTarget>(true));
            }

            if (targetsInScene.Count == 0)
            {
                return;
            }

            foreach (var target in targetsInScene)
            {
                if (m_bakedData.TryGetValue(target.GUID, out var data))
                {
                    var renderer = target.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(m_propertyBlock);
                        m_propertyBlock.SetFloat(m_blendIDShaderPropertyID, data.id);
                        renderer.SetPropertyBlock(m_propertyBlock);
                    }
                }
            }
            Debug.Log($"MeshBlend: 已为新加载的场景 '{scene.name}' 中的 {targetsInScene.Count} 个物体应用了混合ID。");
        }
    }
}



