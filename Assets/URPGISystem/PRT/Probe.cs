using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NewVision.PRT
{
    /// <summary>
    /// Surfel（表面元素）结构体
    /// 存储探针采样点的几何和材质信息
    /// </summary>
    public struct Surfel
    {
        /// <summary>
        /// Surfel的世界坐标位置
        /// </summary>
        public Vector3 position;
        
        /// <summary>
        /// Surfel的法线方向
        /// </summary>
        public Vector3 normal;
        
        /// <summary>
        /// Surfel的反照率（Albedo）颜色
        /// </summary>
        public Vector3 albedo;
        
        /// <summary>
        /// 天空遮罩（1.0表示天空，0.0表示几何体）
        /// </summary>
        public float skyMask;
    }

    /// <summary>
    /// 探针调试模式枚举
    /// </summary>
    public enum ProbeDebugMode
    {
        /// <summary>
        /// 不显示调试信息
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 显示球面采样分布
        /// </summary>
        SphereDistribution = 1,
        
        /// <summary>
        /// 显示采样方向
        /// </summary>
        SampleDirection = 2,
        
        /// <summary>
        /// 显示Surfel位置和法线
        /// </summary>
        Surfel = 3,
        
        /// <summary>
        /// 显示Surfel辐射度
        /// </summary>
        SurfelRadiance = 4
    }

    /// <summary>
    /// 探针类
    /// 负责采样场景几何信息并计算球谐系数
    /// 
    /// 工作流程：
    /// 1. 初始化 - 创建ComputeBuffer和RenderTexture
    /// 2. 捕获GBuffer Cubemap - 从探针位置渲染场景的GBuffer
    /// 3. 采样Surfel - 从Cubemap中采样生成Surfel数据
    /// 4. 重光照 - 计算每个Surfel的光照并更新球谐系数
    /// </summary>
    [ExecuteAlways]
    public class Probe : MonoBehaviour
    {
        /// <summary>
        /// Compute Shader线程数（X方向）
        /// </summary>
        const int tX = 32;
        
        /// <summary>
        /// Compute Shader线程数（Y方向）
        /// </summary>
        const int tY = 16;
        
        /// <summary>
        /// 每个探针的射线数量（512条）
        /// </summary>
        const int rayNum = tX * tY;
        
        /// <summary>
        /// Surfel结构体的字节大小
        /// position(12) + normal(12) + albedo(12) + skyMask(4) = 40字节
        /// </summary>
        const int surfelByteSize = 3 * 12 + 4;

        /// <summary>
        /// 材质属性块（用于调试渲染）
        /// </summary>
        MaterialPropertyBlock matPropBlock;
        
        /// <summary>
        /// CPU端Surfel数组，用于调试和数据回读
        /// </summary>
        public Surfel[] readBackBuffer;
        
        /// <summary>
        /// GPU端Surfel缓冲区
        /// </summary>
        public ComputeBuffer surfels;

        /// <summary>
        /// Surfel辐射度调试缓冲区（CPU端）
        /// </summary>
        Vector3[] radianceDebugBuffer;
        
        /// <summary>
        /// Surfel辐射度缓冲区（GPU端）
        /// </summary>
        public ComputeBuffer surfelRadiance;

        /// <summary>
        /// SH9系数的字节大小（9个系数 x 3通道 x 4字节）
        /// </summary>
        const int coefficientSH9ByteSize = 9 * 3 * 4;
        
        /// <summary>
        /// SH9系数清零值数组
        /// </summary>
        int[] coefficientClearValue;
        
        /// <summary>
        /// GPU端SH9系数缓冲区（27个int，对应9个RGB系数）
        /// </summary>
        public ComputeBuffer coefficientSH9;

        /// <summary>
        /// 世界位置Cubemap渲染纹理
        /// </summary>
        public RenderTexture RT_WorldPos;
        
        /// <summary>
        /// 法线Cubemap渲染纹理
        /// </summary>
        public RenderTexture RT_Normal;
        
        /// <summary>
        /// 反照率Cubemap渲染纹理
        /// </summary>
        public RenderTexture RT_Albedo;

        /// <summary>
        /// Surfel采样Compute Shader
        /// </summary>
        public ComputeShader surfelSampleCS;
        
        /// <summary>
        /// Surfel重光照Compute Shader
        /// </summary>
        public ComputeShader surfelReLightCS;

        /// <summary>
        /// 在ProbeVolume中的索引（由父对象设置）
        /// </summary>
        [HideInInspector]
        public int indexInProbeVolume = -1;
        
        /// <summary>
        /// 临时缓冲区（用于独立探针）
        /// </summary>
        ComputeBuffer tempBuffer;

        /// <summary>
        /// 调试模式
        /// </summary>
        public ProbeDebugMode debugMode;

        /// <summary>
        /// Unity Start回调
        /// 步骤1: 尝试初始化探针
        /// </summary>
        void Start()
        {
            TryInit();
        }

        /// <summary>
        /// 尝试初始化探针
        /// 步骤1: 创建Surfel ComputeBuffer
        /// 步骤2: 创建SH9系数ComputeBuffer
        /// 步骤3: 创建CPU端回读缓冲区
        /// 步骤4: 创建辐射度缓冲区
        /// 步骤5: 创建材质属性块
        /// 步骤6: 创建临时缓冲区
        /// </summary>
        public void TryInit()
        {
            if (surfels == null)
                surfels = new ComputeBuffer(rayNum, surfelByteSize);

            if (coefficientSH9 == null)
            {
                coefficientSH9 = new ComputeBuffer(27, sizeof(int));
                coefficientClearValue = new int[27];
                for (int i = 0; i < 27; i++) coefficientClearValue[i] = 0;
            }

            if (readBackBuffer == null)
                readBackBuffer = new Surfel[rayNum];

            if (surfelRadiance == null)
                surfelRadiance = new ComputeBuffer(rayNum, sizeof(float) * 3);

            if (radianceDebugBuffer == null)
                radianceDebugBuffer = new Vector3[rayNum];

            if (matPropBlock == null)
                matPropBlock = new MaterialPropertyBlock();

            if (tempBuffer == null)
                tempBuffer = new ComputeBuffer(1, 4);
        }

        /// <summary>
        /// Unity OnDestroy回调
        /// 步骤1: 释放所有ComputeBuffer
        /// </summary>
        void OnDestroy()
        {
            if (surfels != null) surfels.Release();
            if (coefficientSH9 != null) coefficientSH9.Release();
            if (surfelRadiance != null) surfelRadiance.Release();
            if (tempBuffer != null) tempBuffer.Release();
        }

        /// <summary>
        /// Unity OnDrawGizmos回调
        /// 步骤1: 设置探针材质使用SH调试Shader
        /// 步骤2: 根据调试模式绘制Gizmos
        /// </summary>
        void OnDrawGizmos()
        {
            Vector3 probePos = gameObject.transform.position;

            gameObject.GetComponent<MeshRenderer>().enabled = !Application.isPlaying;
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial.shader = Shader.Find("NewVision/PRT/SHDebug");
            matPropBlock.SetBuffer("_coefficientSH9", coefficientSH9);
            meshRenderer.SetPropertyBlock(matPropBlock);


            if (debugMode == ProbeDebugMode.None)
                return;

            surfels.GetData(readBackBuffer);
            surfelRadiance.GetData(radianceDebugBuffer);


            for (int i = 0; i < rayNum; i++)
            {
                Surfel surfel = readBackBuffer[i];
                Vector3 radiance = radianceDebugBuffer[i];

                Vector3 pos = surfel.position;
                Vector3 normal = surfel.normal;
                Vector3 color = surfel.albedo;

                Vector3 dir = pos - probePos;
                dir = Vector3.Normalize(dir);

                bool isSky = surfel.skyMask >= 0.995;

                Gizmos.color = Color.yellow;
                if (debugMode == ProbeDebugMode.SphereDistribution)
                {
                    if (isSky) Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(dir + probePos, 0.025f);
                }

                if (debugMode == ProbeDebugMode.SampleDirection)
                {
                    if (isSky)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(probePos, probePos + dir * 25.0f);
                    }
                    else
                    {
                        Gizmos.DrawLine(probePos, pos);
                        Gizmos.DrawSphere(pos, 0.05f);
                    }
                }

                if (debugMode == ProbeDebugMode.Surfel)
                {
                    if (isSky) continue;
                    Gizmos.DrawSphere(pos, 0.05f);
                    Gizmos.DrawLine(pos, pos + normal * 0.25f);
                }

                if (debugMode == ProbeDebugMode.SurfelRadiance)
                {
                    if (isSky) continue;
                    Gizmos.color = new Color(radiance.x, radiance.y, radiance.z);
                    Gizmos.DrawSphere(pos, 0.05f);
                }
            }
        }

        /// <summary>
        /// 批量设置GameObject的Shader
        /// </summary>
        void BatchSetShader(GameObject[] gameObjects, Shader shader)
        {
            foreach (var go in gameObjects)
            {
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial.shader = shader;
                }
            }
        }

        /// <summary>
        /// 捕获GBuffer Cubemap
        /// 步骤1: 创建临时相机
        /// 步骤2: 使用WorldPos Shader渲染世界位置Cubemap
        /// 步骤3: 使用Normal Shader渲染法线Cubemap
        /// 步骤4: 使用Unlit Shader渲染反照率Cubemap
        /// 步骤5: 执行Surfel采样
        /// 步骤6: 销毁临时相机
        /// </summary>
        public void CaptureGbufferCubemaps()
        {
            TryInit();

            GameObject go = new GameObject("CubemapCamera");
            go.transform.position = transform.position;
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<Camera>();
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

            GameObject[] gameObjects = FindObjectsOfType(typeof(GameObject)) as GameObject[];

            BatchSetShader(gameObjects, Shader.Find("NewVision/PRT/GbufferWorldPos"));
            camera.RenderToCubemap(RT_WorldPos);

            BatchSetShader(gameObjects, Shader.Find("NewVision/PRT/GbufferNormal"));
            camera.RenderToCubemap(RT_Normal);

            BatchSetShader(gameObjects, Shader.Find("Universal Render Pipeline/Unlit"));
            camera.RenderToCubemap(RT_Albedo);

            BatchSetShader(gameObjects, Shader.Find("Universal Render Pipeline/Lit"));

            SampleSurfels(RT_WorldPos, RT_Normal, RT_Albedo);

            DestroyImmediate(go);
        }

        /// <summary>
        /// 采样Surfel
        /// 步骤1: 获取Compute Shader内核
        /// 步骤2: 设置探针位置和随机种子
        /// 步骤3: 设置Cubemap纹理
        /// 步骤4: 设置Surfel输出缓冲区
        /// 步骤5: 执行Compute Shader
        /// 步骤6: 回读Surfel数据到CPU
        /// </summary>
        void SampleSurfels(RenderTexture worldPosCubemap, RenderTexture normalCubemap, RenderTexture albedoCubemap)
        {
            var kid = surfelSampleCS.FindKernel("CSMain");

            Vector3 p = gameObject.transform.position;
            surfelSampleCS.SetVector("_probePos", new Vector4(p.x, p.y, p.z, 1.0f));
            surfelSampleCS.SetFloat("_randSeed", UnityEngine.Random.Range(0.0f, 1.0f));
            surfelSampleCS.SetTexture(kid, "_worldPosCubemap", worldPosCubemap);
            surfelSampleCS.SetTexture(kid, "_normalCubemap", normalCubemap);
            surfelSampleCS.SetTexture(kid, "_albedoCubemap", albedoCubemap);
            surfelSampleCS.SetBuffer(kid, "_surfels", surfels);

            surfelSampleCS.Dispatch(kid, 1, 1, 1);

            surfels.GetData(readBackBuffer);
        }

        /// <summary>
        /// 重光照
        /// 步骤1: 获取Compute Shader内核
        /// 步骤2: 设置探针位置
        /// 步骤3: 设置Surfel缓冲区
        /// 步骤4: 设置SH9系数缓冲区
        /// 步骤5: 设置辐射度输出缓冲区
        /// 步骤6: 获取父ProbeVolume的系数体素缓冲区
        /// 步骤7: 清零SH9系数
        /// 步骤8: 执行Compute Shader
        /// </summary>
        public void ReLight(CommandBuffer cmd)
        {
            var kid = surfelReLightCS.FindKernel("CSMain");

            Vector3 p = gameObject.transform.position;
            cmd.SetComputeVectorParam(surfelReLightCS, "_probePos", new Vector4(p.x, p.y, p.z, 1.0f));
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_surfels", surfels);
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_coefficientSH9", coefficientSH9);
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_surfelRadiance", surfelRadiance);

            var parent = transform.parent;
            ProbeVolume probeVolume = parent == null ? null : parent.gameObject.GetComponent<ProbeVolume>();
            ComputeBuffer coefficientVoxel = probeVolume == null ? tempBuffer : probeVolume.coefficientVoxel;
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_coefficientVoxel", coefficientVoxel);
            cmd.SetComputeIntParam(surfelReLightCS, "_indexInProbeVolume", indexInProbeVolume);

            cmd.SetBufferData(coefficientSH9, coefficientClearValue);
            cmd.DispatchCompute(surfelReLightCS, kid, 1, 1, 1);
        }
    }
}
