using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace NewVision.PRT
{
    /// <summary>
    /// 探针体调试模式枚举
    /// </summary>
    public enum ProbeVolumeDebugMode
    {
        /// <summary>
        /// 不显示调试信息
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 显示探针网格
        /// </summary>
        ProbeGrid = 1,
        
        /// <summary>
        /// 显示探针辐射度
        /// </summary>
        ProbeRadiance = 2
    }

    /// <summary>
    /// 探针体类
    /// 管理一组探针，存储和更新球谐系数体素
    /// 
    /// 工作流程：
    /// 1. 初始化 - 创建探针网格和系数体素缓冲区
    /// 2. 捕获 - 对每个探针捕获GBuffer Cubemap
    /// 3. 存储 - 将Surfel数据保存到ProbeVolumeData
    /// 4. 运行时 - 每帧更新SH系数，支持多次反弹
    /// </summary>
    [ExecuteAlways]
    public class ProbeVolume : MonoBehaviour
    {
        /// <summary>
        /// 探针预制体
        /// </summary>
        public GameObject probePrefab;

        /// <summary>
        /// 世界位置Cubemap渲染纹理（已废弃，由各Probe管理）
        /// </summary>
        RenderTexture RT_WorldPos;
        
        /// <summary>
        /// 法线Cubemap渲染纹理（已废弃，由各Probe管理）
        /// </summary>
        RenderTexture RT_Normal;
        
        /// <summary>
        /// 反照率Cubemap渲染纹理（已废弃，由各Probe管理）
        /// </summary>
        RenderTexture RT_Albedo;

        /// <summary>
        /// 探针网格X方向数量
        /// </summary>
        public int probeSizeX = 8;
        
        /// <summary>
        /// 探针网格Y方向数量
        /// </summary>
        public int probeSizeY = 4;
        
        /// <summary>
        /// 探针网格Z方向数量
        /// </summary>
        public int probeSizeZ = 8;
        
        /// <summary>
        /// 探针网格间距
        /// </summary>
        public float probeGridSize = 2.0f;

        /// <summary>
        /// 探针体数据资产
        /// </summary>
        public ProbeVolumeData data;

        /// <summary>
        /// 当前帧的SH系数体素缓冲区
        /// 每个探针存储27个int（9个SH系数 x 3通道）
        /// </summary>
        public ComputeBuffer coefficientVoxel;
        
        /// <summary>
        /// 上一帧的SH系数体素缓冲区
        /// 用于多次反弹计算
        /// </summary>
        public ComputeBuffer lastFrameCoefficientVoxel;
        
        /// <summary>
        /// 系数体素清零值数组
        /// </summary>
        int[] cofficientVoxelClearValue;

        /// <summary>
        /// 天空光强度
        /// </summary>
        [Range(0.0f, 50.0f)]
        public float skyLightIntensity = 1.0f;

        /// <summary>
        /// 全局光照强度
        /// </summary>
        [Range(0.0f, 50.0f)]
        public float GIIntensity = 1.0f;

        /// <summary>
        /// 调试模式
        /// </summary>
        public ProbeVolumeDebugMode debugMode = ProbeVolumeDebugMode.ProbeRadiance;

        /// <summary>
        /// 探针游戏对象数组
        /// </summary>
        public GameObject[] probes;

        /// <summary>
        /// Unity Start回调
        /// 步骤1: 生成探针网格
        /// 步骤2: 加载Surfel数据
        /// 步骤3: 设置调试模式
        /// </summary>
        void Start()
        {
            GenerateProbes();
            data.TryLoadSurfelData(this);
            debugMode = ProbeVolumeDebugMode.ProbeGrid;
        }

        /// <summary>
        /// Unity Update回调
        /// </summary>
        void Update()
        {
        }

        /// <summary>
        /// Unity OnDestroy回调
        /// 步骤1: 释放系数体素缓冲区
        /// </summary>
        void OnDestroy()
        {
            if (coefficientVoxel != null) coefficientVoxel.Release();
            if (lastFrameCoefficientVoxel != null) lastFrameCoefficientVoxel.Release();
        }

        /// <summary>
        /// Unity OnDrawGizmos回调
        /// 步骤1: 绘制体素角点
        /// 步骤2: 根据调试模式绘制探针网格
        /// </summary>
        void OnDrawGizmos()
        {
            Gizmos.DrawCube(GetVoxelMinCorner(), new Vector3(1, 1, 1));

            if (probes != null)
            {
                foreach (var go in probes)
                {
                    Probe probe = go.GetComponent<Probe>();
                    if (debugMode == ProbeVolumeDebugMode.ProbeGrid)
                    {
                        Vector3 cubeSize = new Vector3(probeGridSize / 2, probeGridSize / 2, probeGridSize / 2);
                        Gizmos.DrawWireCube(probe.transform.position + cubeSize, cubeSize * 2.0f);
                    }

                    MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                    if (Application.isPlaying) meshRenderer.enabled = false;
                    if (debugMode == ProbeVolumeDebugMode.None) meshRenderer.enabled = false;
                }
            }
        }

        /// <summary>
        /// 生成探针网格
        /// 步骤1: 销毁旧的探针
        /// 步骤2: 释放旧的系数体素缓冲区
        /// 步骤3: 计算探针数量
        /// 步骤4: 遍历创建探针：
        ///        - 计算探针位置
        ///        - 实例化探针预制体
        ///        - 设置探针索引
        ///        - 初始化探针
        /// 步骤5: 创建系数体素缓冲区
        /// 步骤6: 初始化清零值数组
        /// </summary>
        public void GenerateProbes()
        {
            if (probes != null)
            {
                for (int i = 0; i < probes.Length; i++)
                {
                    DestroyImmediate(probes[i]);
                }
            }
            if (coefficientVoxel != null) coefficientVoxel.Release();
            if (lastFrameCoefficientVoxel != null) lastFrameCoefficientVoxel.Release();

            int probeNum = probeSizeX * probeSizeY * probeSizeZ;

            probes = new GameObject[probeNum];
            for (int x = 0; x < probeSizeX; x++)
            {
                for (int y = 0; y < probeSizeY; y++)
                {
                    for (int z = 0; z < probeSizeZ; z++)
                    {
                        Vector3 relativePos = new Vector3(x, y, z) * probeGridSize;
                        Vector3 parentPos = gameObject.transform.position;

                        int index = x * probeSizeY * probeSizeZ + y * probeSizeZ + z;
                        probes[index] = Instantiate(probePrefab, gameObject.transform) as GameObject;
                        probes[index].transform.position = relativePos + parentPos;
                        probes[index].GetComponent<Probe>().indexInProbeVolume = index;
                        probes[index].GetComponent<Probe>().TryInit();
                    }
                }
            }

            coefficientVoxel = new ComputeBuffer(probeNum * 27, sizeof(int));
            lastFrameCoefficientVoxel = new ComputeBuffer(probeNum * 27, sizeof(int));
            cofficientVoxelClearValue = new int[probeNum * 27];
            for (int i = 0; i < cofficientVoxelClearValue.Length; i++)
            {
                cofficientVoxelClearValue[i] = 0;
            }
        }

        /// <summary>
        /// 捕获探针数据
        /// 步骤1: 隐藏所有探针的渲染器
        /// 步骤2: 遍历每个探针捕获GBuffer Cubemap
        /// 步骤3: 存储Surfel数据到ProbeVolumeData
        /// </summary>
        public void ProbeCapture()
        {
            foreach (var go in probes)
            {
                go.GetComponent<MeshRenderer>().enabled = false;
            }

            foreach (var go in probes)
            {
                Probe probe = go.GetComponent<Probe>();
                probe.CaptureGbufferCubemaps();
            }

            data.StorageSurfelData(this);
        }

        /// <summary>
        /// 清空系数体素
        /// 步骤1: 检查缓冲区是否存在
        /// 步骤2: 使用清零值数组填充缓冲区
        /// </summary>
        public void ClearCoefficientVoxel(CommandBuffer cmd)
        {
            if (coefficientVoxel == null || cofficientVoxelClearValue == null) return;
            cmd.SetBufferData(coefficientVoxel, cofficientVoxelClearValue);
        }

        /// <summary>
        /// 交换当前帧和上一帧的系数体素
        /// 用于多次反弹计算
        /// 步骤1: 检查缓冲区是否存在
        /// 步骤2: 交换两个缓冲区引用
        /// </summary>
        public void SwapLastFrameCoefficientVoxel()
        {
            if (coefficientVoxel == null || lastFrameCoefficientVoxel == null) return;
            (coefficientVoxel, lastFrameCoefficientVoxel) = (lastFrameCoefficientVoxel, coefficientVoxel);
        }

        /// <summary>
        /// 获取体素最小角点位置
        /// 即探针体的起始位置
        /// </summary>
        public Vector3 GetVoxelMinCorner()
        {
            return gameObject.transform.position;
        }
    }
}
