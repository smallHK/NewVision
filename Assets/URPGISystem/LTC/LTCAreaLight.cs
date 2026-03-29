using UnityEngine;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光组件
    /// 表示场景中的一个区域光源
    /// </summary>
    [ExecuteInEditMode]
    public class LTCAreaLight : MonoBehaviour
    {
        [Header("Light Properties")]
        /// <summary>
        /// 光源颜色
        /// </summary>
        public Color m_LightColor = Color.white;
        
        /// <summary>
        /// 光源强度
        /// </summary>
        public float m_Intensity = 1.0f;
        
        /// <summary>
        /// 是否渲染阴影
        /// </summary>
        public bool m_RenderShadow = false;
        
        [Header("Shadow Properties")]
        /// <summary>
        /// 阴影近裁剪面
        /// </summary>
        public float m_ShadowNearClip = 0.1f;
        
        /// <summary>
        /// 阴影远裁剪面
        /// </summary>
        public float m_ShadowFarClip = 100.0f;
        
        /// <summary>
        /// 阴影偏移
        /// </summary>
        public float m_ShadowBias = 0.001f;
        
        /// <summary>
        /// 阴影贴图分辨率
        /// </summary>
        public int m_ShadowMapResolution = 1024;
        
        [Header("Texture Properties")]
        /// <summary>
        /// 纹理索引
        /// </summary>
        public int TextureIndex = 0;
        
        /// <summary>
        /// 网格过滤器
        /// </summary>
        private MeshFilter m_MeshFilter;
        
        /// <summary>
        /// 光源顶点
        /// </summary>
        private Matrix4x4 m_LightVertices;
        
        /// <summary>
        /// 阴影贴图
        /// </summary>
        private RenderTexture _shadowMap;
        
        /// <summary>
        /// 阴影贴图占位符
        /// </summary>
        private Texture2D _shadowMapDummy;
        
        /// <summary>
        /// 阴影贴图属性
        /// </summary>
        public RenderTexture m_ShadowMap
        {
            get { return _shadowMap; }
        }
        
        /// <summary>
        /// 阴影贴图占位符属性
        /// </summary>
        public Texture2D mShadowMapDummy
        {
            get { return _shadowMapDummy; }
        }
        
        /// <summary>
        /// 当组件启用时调用
        /// </summary>
        private void OnEnable()
        {
            LTCAreaLightManager.Register(this);
            InitializeShadowMap();
        }
        
        /// <summary>
        /// 当组件禁用时调用
        /// </summary>
        private void OnDisable()
        {
            LTCAreaLightManager.Unregister(this);
            ReleaseShadowMap();
        }
        
        /// <summary>
        /// 每帧更新时调用
        /// </summary>
        private void Update()
        {
            UpdateLightVertices();
        }
        
        /// <summary>
        /// 初始化阴影贴图
        /// </summary>
        private void InitializeShadowMap()
        {
            if (m_RenderShadow)
            {
                _shadowMap = new RenderTexture(m_ShadowMapResolution, m_ShadowMapResolution, 16, RenderTextureFormat.Depth);
                _shadowMap.name = "AreaLightShadowMap";
                _shadowMap.filterMode = FilterMode.Bilinear;
                _shadowMap.wrapMode = TextureWrapMode.Clamp;
                _shadowMapDummy = new Texture2D(1, 1, TextureFormat.R8, false);
                _shadowMapDummy.SetPixel(0, 0, Color.white);
                _shadowMapDummy.Apply();
            }
        }
        
        /// <summary>
        /// 释放阴影贴图
        /// </summary>
        private void ReleaseShadowMap()
        {
            if (_shadowMap != null)
            {
                _shadowMap.Release();
                _shadowMap = null;
            }
            if (_shadowMapDummy != null)
            {
                DestroyImmediate(_shadowMapDummy);
                _shadowMapDummy = null;
            }
        }
        
        /// <summary>
        /// 更新光源顶点
        /// </summary>
        private void UpdateLightVertices()
        {
            if (m_MeshFilter == null)
            {
                m_MeshFilter = GetComponent<MeshFilter>();
            }
            
            if (m_MeshFilter != null && m_MeshFilter.sharedMesh != null)
            {
                Vector3[] vertices = m_MeshFilter.sharedMesh.vertices;
                Matrix4x4 localToWorld = transform.localToWorldMatrix;
                
                for (int i = 0; i < 4 && i < vertices.Length; i++)
                {
                    Vector3 worldPos = localToWorld.MultiplyPoint(vertices[i]);
                    m_LightVertices.SetColumn(i, new Vector4(worldPos.x, worldPos.y, worldPos.z, 1.0f));
                }
            }
        }
        
        /// <summary>
        /// 获取光源颜色
        /// </summary>
        /// <returns>包含颜色和强度的向量</returns>
        public Vector4 GetLightColor()
        {
            return new Vector4(m_LightColor.r, m_LightColor.g, m_LightColor.b, m_Intensity);
        }
        
        /// <summary>
        /// 获取光源顶点
        /// </summary>
        /// <returns>光源顶点矩阵</returns>
        public Matrix4x4 GetLightVertices()
        {
            return m_LightVertices;
        }
        
        /// <summary>
        /// 获取阴影参数
        /// </summary>
        /// <returns>阴影参数向量</returns>
        public Vector4 GetShadowParams()
        {
            return new Vector4(m_ShadowBias, m_ShadowMapResolution, 1.0f / m_ShadowMapResolution, 0.0f);
        }
        
        /// <summary>
        /// 获取阴影近裁剪面
        /// </summary>
        /// <returns>阴影近裁剪面距离</returns>
        public float GetShadowNearClip()
        {
            return m_ShadowNearClip;
        }
        
        /// <summary>
        /// 获取阴影远裁剪面
        /// </summary>
        /// <returns>阴影远裁剪面距离</returns>
        public float GetShadowFarClip()
        {
            return m_ShadowFarClip;
        }
        
        /// <summary>
        /// 获取投影矩阵
        /// </summary>
        /// <returns>投影矩阵</returns>
        public Matrix4x4 GetProjMatrix()
        {
            return Matrix4x4.identity;
        }
    }
}