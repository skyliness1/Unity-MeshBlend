using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ScreenSpaceModelBlend.Scripts.Editor
{
    /// <summary>
    /// 一个标记组件，用于标识需要参与MeshBlend烘焙的物体。
    /// </summary>
    
    [DisallowMultipleComponent]
    [AddComponentMenu("Mesh Blend/Mesh Blend Target")]
    public class MeshBlendTarget : MonoBehaviour
    {
        [SerializeField, HideInInspector] 
        private string m_guid;

        public string GUID => m_guid;
        
        /// <summary>
        /// 组件的GUID，用于精确识别每个混合ID所对应的场景物体
        /// </summary>
        private void Reset()
        {
            if (string.IsNullOrEmpty(m_guid))
            {
                m_guid = Guid.NewGuid().ToString();
            }
        }
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(m_guid))
            {
                m_guid = Guid.NewGuid().ToString();
            }
        }
    }
}
