using UnityEngine;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光查找表管理类
    /// 负责加载和生成LTC相关的查找表
    /// </summary>
    public static class LTCAreaLightLUT
    {
        /// <summary>
        /// 查找表类型
        /// </summary>
        public enum LUTType
        {
            /// <summary>
            /// 漫反射变换逆矩阵查找表
            /// </summary>
            TransformInvDisneyDiffuse,
            
            /// <summary>
            /// 高光变换逆矩阵查找表
            /// </summary>
            TransformInvDisneyGGX,
            
            /// <summary>
            /// 菲涅尔查找表
            /// </summary>
            AmpDiffAmpSpecFresnel
        }
        
        /// <summary>
        /// 漫反射变换逆矩阵纹理
        /// </summary>
        private static Texture2D s_TransformInvDiffuse;
        
        /// <summary>
        /// 高光变换逆矩阵纹理
        /// </summary>
        private static Texture2D s_TransformInvSpecular;
        
        /// <summary>
        /// 菲涅尔纹理
        /// </summary>
        private static Texture2D s_FresnelTexture;
        
        /// <summary>
        /// 加载查找表
        /// </summary>
        /// <param name="type">查找表类型</param>
        /// <returns>查找表纹理</returns>
        public static Texture2D LoadLUT(LUTType type)
        {
            switch (type)
            {
                case LUTType.TransformInvDisneyDiffuse:
                    if (s_TransformInvDiffuse == null)
                    {
                        s_TransformInvDiffuse = CreateTransformInvDiffuseLUT();
                    }
                    return s_TransformInvDiffuse;
                case LUTType.TransformInvDisneyGGX:
                    if (s_TransformInvSpecular == null)
                    {
                        s_TransformInvSpecular = CreateTransformInvSpecularLUT();
                    }
                    return s_TransformInvSpecular;
                case LUTType.AmpDiffAmpSpecFresnel:
                    if (s_FresnelTexture == null)
                    {
                        s_FresnelTexture = CreateFresnelLUT();
                    }
                    return s_FresnelTexture;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// 创建漫反射变换逆矩阵查找表
        /// </summary>
        /// <returns>漫反射变换逆矩阵纹理</returns>
        private static Texture2D CreateTransformInvDiffuseLUT()
        {
            Texture2D lut = new Texture2D(64, 64, TextureFormat.RGBAFloat, false);
            lut.name = "TransformInvDiffuseLUT";
            lut.filterMode = FilterMode.Bilinear;
            lut.wrapMode = TextureWrapMode.Clamp;
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float theta = (float)y / 63.0f * Mathf.PI * 0.5f;
                    float phi = (float)x / 63.0f * Mathf.PI * 2.0f;
                    Vector3 v = new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)
                    );
                    Vector4 value = CalculateTransformInvDiffuse(v);
                    lut.SetPixel(x, y, new Color(value.x, value.y, value.z, value.w));
                }
            }
            
            lut.Apply();
            return lut;
        }
        
        /// <summary>
        /// 创建高光变换逆矩阵查找表
        /// </summary>
        /// <returns>高光变换逆矩阵纹理</returns>
        private static Texture2D CreateTransformInvSpecularLUT()
        {
            Texture2D lut = new Texture2D(64, 64, TextureFormat.RGBAFloat, false);
            lut.name = "TransformInvSpecularLUT";
            lut.filterMode = FilterMode.Bilinear;
            lut.wrapMode = TextureWrapMode.Clamp;
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float theta = (float)y / 63.0f * Mathf.PI * 0.5f;
                    float phi = (float)x / 63.0f * Mathf.PI * 2.0f;
                    Vector3 v = new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)
                    );
                    Vector4 value = CalculateTransformInvSpecular(v);
                    lut.SetPixel(x, y, new Color(value.x, value.y, value.z, value.w));
                }
            }
            
            lut.Apply();
            return lut;
        }
        
        /// <summary>
        /// 创建菲涅尔查找表
        /// </summary>
        /// <returns>菲涅尔纹理</returns>
        private static Texture2D CreateFresnelLUT()
        {
            Texture2D lut = new Texture2D(64, 64, TextureFormat.RGBAFloat, false);
            lut.name = "FresnelLUT";
            lut.filterMode = FilterMode.Bilinear;
            lut.wrapMode = TextureWrapMode.Clamp;
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float cosTheta = (float)y / 63.0f;
                    float roughness = (float)x / 63.0f;
                    Vector4 value = CalculateFresnel(cosTheta, roughness);
                    lut.SetPixel(x, y, new Color(value.x, value.y, value.z, value.w));
                }
            }
            
            lut.Apply();
            return lut;
        }
        
        /// <summary>
        /// 计算漫反射变换逆矩阵
        /// Disney漫反射使用Lambertian，变换矩阵为单位矩阵
        /// </summary>
        /// <param name="v">方向向量</param>
        /// <returns>变换逆矩阵</returns>
        private static Vector4 CalculateTransformInvDiffuse(Vector3 v)
        {
            float m11 = 1.0f;
            float m12 = 0.0f;
            float m22 = 1.0f;
            return new Vector4(m11, m12, m22, 1.0f / (m11 * m22 - m12 * m12));
        }
        
        /// <summary>
        /// 计算高光变换逆矩阵
        /// Disney GGX的LTC矩阵
        /// </summary>
        /// <param name="v">方向向量</param>
        /// <returns>变换逆矩阵</returns>
        private static Vector4 CalculateTransformInvSpecular(Vector3 v)
        {
            float NdotV = v.y;
            float roughness = 0.5f;
            
            float alpha = roughness * roughness;
            float alpha2 = alpha * alpha;
            
            float a = 1.0f / (1.0f + NdotV);
            float b = alpha2 * a;
            
            float m11 = 1.0f + b * NdotV;
            float m12 = -b * v.x * v.z;
            float m22 = 1.0f + b * (1.0f - v.y * v.y);
            
            float det = m11 * m22 - m12 * m12;
            return new Vector4(m11, m12, m22, 1.0f / det);
        }
        
        /// <summary>
        /// 计算菲涅尔值
        /// Schlick近似法计算菲涅尔
        /// </summary>
        /// <param name="cosTheta">余弦角度</param>
        /// <param name="roughness">粗糙度</param>
        /// <returns>菲涅尔值</returns>
        private static Vector4 CalculateFresnel(float cosTheta, float roughness)
        {
            float F0 = 0.04f;
            float oneMinusCosTheta = 1.0f - cosTheta;
            float oneMinusCosTheta5 = oneMinusCosTheta * oneMinusCosTheta;
            oneMinusCosTheta5 = oneMinusCosTheta5 * oneMinusCosTheta5 * oneMinusCosTheta;
            
            float F = F0 + (1.0f - F0) * oneMinusCosTheta5;
            
            float roughness2 = roughness * roughness;
            float roughness4 = roughness2 * roughness2;
            
            float specularScale = 1.0f / Mathf.Max(roughness4, 0.0001f);
            specularScale = Mathf.Min(specularScale, 1000.0f);
            
            return new Vector4(F, 1.0f, specularScale, roughness);
        }
    }
}