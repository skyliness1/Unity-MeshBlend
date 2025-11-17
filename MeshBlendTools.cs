using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Object = UnityEngine.Object;

namespace ScreenSpaceModelBlend.Scripts.Editor
{
    public class MeshBlendTools : EditorWindow
    {
        private Vector2 m_scrollPosition;
        private List<(GameObject go, byte id, string name)> m_inspectedData;
        
        [MenuItem("Tools/ScreenSpaceModelBlend/模型融合工具")]
        public static void ShowWindow()
        {
            GetWindow<MeshBlendTools>("模型融合工具");
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("1. 选中场景中需要加入接缝识别的物体", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("选中一个或多个LOD0的静态网格物体，然后点击下面的按钮来添加或移除接缝识别标记", MessageType.Info);
            
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Green tint

            if (GUILayout.Button("标记选中物体为混合目标"))
            {
                MarkSelectedObjects();
            }

            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Red tint
            if (GUILayout.Button("移除选中物体的标记"))
            {
                UnmarkSelectedObjects();
            }
            
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Red tint
            if (GUILayout.Button("移除所有物体的标记"))
            {
                UnmarkAllObjects();
            }
            
            GUI.backgroundColor = Color.white; // Reset color
        
            EditorGUILayout.Space(20);
            
            EditorGUILayout.LabelField("执行烘焙", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("点击下方按钮开始烘焙。您将被要求选择一个位置来保存烘焙出的数据文件 (.bytes)。", MessageType.Info);

            if (GUILayout.Button("开始烘焙所有已标记的物体"))
            {
                // 直接弹出“另存为”对话框
                string path = EditorUtility.SaveFilePanel(
                    "保存烘焙数据", 
                    Application.dataPath, // 默认从Assets文件夹开始
                    "bakedMeshBlendIds", 
                    "bytes"
                );

                // 如果用户选择了路径（而不是取消）
                if (!string.IsNullOrEmpty(path))
                {
                    // 将路径传递给烘焙逻辑
                    MeshBlendIDBaker.Bake(path);
                }
            }
        }

        private void MarkSelectedObjects()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有选中任何物体", "取消");
                return;
            }

            int markedCount = 0;
            int skippedCount = 0;
            string lastSkippedReason = "";

            foreach (var go in selectedObjects )
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    skippedCount++;
                    lastSkippedReason = "选中的物体不合法";
                    continue;
                }

                LODGroup lodGroup = go.GetComponentInParent<LODGroup>();

                if (lodGroup != null)
                {
                    var lods = lodGroup.GetLODs();
                    if (lods.Length > 0 && !lods[0].renderers.Contains(renderer))
                    {
                        skippedCount++;
                        lastSkippedReason = "物体不是LOD0的Mesh";
                        continue;
                    }
                }
                
                // 物体合格，并且尚未被标记
                if (go.GetComponent<MeshBlendTarget>() == null)
                {
                    Undo.AddComponent<MeshBlendTarget>(go);
                    markedCount++;
                }
            }

            string message = $"操作完成。\n\n成功标记了{markedCount}个新物体";
            
            if (skippedCount > 0)
            {
                message += $"\n跳过了 {skippedCount} 个物体。最后一个被跳过的原因是：{lastSkippedReason}";
            }
            
            Debug.Log(message.Replace("\n\n", " ").Replace("\n", " "));
            EditorUtility.DisplayDialog("标记结果", message, "好的");
        }

        private void UnmarkSelectedObjects()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有选中任何物体", "取消");
                return;
            }

            int count = 0;
            foreach (var go in selectedObjects)
            {
                MeshBlendTarget target = go.GetComponent<MeshBlendTarget>();
                if (target != null)
                {
                    Undo.DestroyObjectImmediate(target);
                    count++;
                }
            }
            EditorUtility.DisplayDialog("成功", $"成功移除了{count}个物体的标记", "取消");
        }

        private void UnmarkAllObjects()
        {
            var sceneTargets = Object.FindObjectsOfType<MeshBlendTarget>();
            if (sceneTargets.Length == 0)
            {
                EditorUtility.DisplayDialog("警告", "场景中没有任何已标记物体", "取消");
                return;
            }

            int count = 0;

            foreach (var target in sceneTargets)
            {
                if (target != null)
                {
                    Undo.DestroyObjectImmediate(target);
                    count++;
                }
            }
            EditorUtility.DisplayDialog("成功", $"成功移除了{count}个物体的标记", "取消");
        } 
    }
}