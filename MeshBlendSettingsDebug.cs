using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScreenSpaceModelBlend.Scripts.Editor
{
    [CustomEditor(typeof(MeshBlendSettings))]
    public class MeshBlendSettingsDebug : UnityEditor.Editor
    {
        private List<(GameObject go, byte id, string name)> m_inspectedData;
        private Vector2 m_scrollPosition;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MeshBlendSettings settings = (MeshBlendSettings)target;

            if (settings.bakedDataFile == null)
            {
                EditorGUILayout.HelpBox("没有绑定的烘焙数据文件。", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("烘焙数据预览", EditorStyles.boldLabel);
            
            if (GUILayout.Button("加载/刷新预览"))
            {
                LoadAndInspectFile(settings.bakedDataFile);
            }
            
            if (m_inspectedData != null && m_inspectedData.Count > 0)
            {
                EditorGUILayout.LabelField($"已加载 {m_inspectedData.Count} 条记录:");
                m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.Height(300));
                foreach (var item in m_inspectedData)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (item.go != null) { EditorGUILayout.ObjectField(item.go, typeof(GameObject), true); }
                    else { EditorGUILayout.LabelField($"[Missing] {item.name}"); }
                    EditorGUILayout.LabelField("ID: " + item.id, GUILayout.Width(60));
                    EditorGUI.BeginDisabledGroup(item.go == null);
                    if (GUILayout.Button("Ping", GUILayout.Width(50))) { EditorGUIUtility.PingObject(item.go); }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            else if (m_inspectedData != null)
            {
                EditorGUILayout.HelpBox("文件中没有数据，或者在当前场景中找不到任何对应的物体。", MessageType.Info);
            }
        }

        private void LoadAndInspectFile(TextAsset dataFile)
        {
            var bakedData = new Dictionary<string, (byte id, string name)>();
            using (MemoryStream stream = new MemoryStream(dataFile.bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    bakedData[reader.ReadString()] = (reader.ReadByte(), reader.ReadString());
                }
            }
        
            var sceneTargets = FindObjectsOfType<MeshBlendTarget>();
            var guidToObjectMap = sceneTargets.ToDictionary(t => t.GUID, t => t.gameObject);
        
            m_inspectedData = new List<(GameObject go, byte id, string name)>();
            foreach (var kvp in bakedData)
            {
                guidToObjectMap.TryGetValue(kvp.Key, out GameObject foundObject);
                m_inspectedData.Add((foundObject, kvp.Value.id, kvp.Value.name));
            }
        }
    }
}

