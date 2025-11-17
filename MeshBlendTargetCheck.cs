using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScreenSpaceModelBlend.Scripts.Editor
{
    /// <summary>
    /// 用于检查MeshBlendTarget标识添加的合规性
    /// </summary>
    
    [CustomEditor(typeof(MeshBlendTarget))]
    public class MeshBlendTargetCheck : UnityEditor.Editor
    {
        private void OnEnable()
        {
            MeshBlendTarget blendTarget = (MeshBlendTarget)target;
            GameObject thisObject = blendTarget.gameObject;

            var renderer = thisObject.GetComponent<MeshRenderer>();
            
            if (renderer == null)
            {
                ValidateAndDestory(blendTarget, "接缝识别标记只能被添加到Mesh物体之上，请检查");
                return;
            }

            LODGroup lodGroup = thisObject.GetComponentInParent<LODGroup>();

            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();

                if (lods.Length > 0)
                {
                    Renderer[] lod0Renderers = lods[0].renderers;

                    if (!lod0Renderers.Contains(renderer))
                    {
                        ValidateAndDestory(blendTarget, "接缝识别标记只能被添加到LOD0的Mesh之上，请检查");
                        return;
                    }
                }
            }
        }
        
        private void ValidateAndDestory(MeshBlendTarget blendTarget, string message)
        {
            EditorUtility.DisplayDialog(
                "无法添加标记",
                message,
                "好的");

            EditorApplication.delayCall += () =>
            {
                if (blendTarget != null)
                {
                    DestroyImmediate(blendTarget, true);
                }

            };

        }
    }
}
