using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace NewVision.PRT
{
    /// <summary>
    /// 探针体数据类
    /// 用于持久化存储探针的Surfel数据
    /// 
    /// 工作流程：
    /// 1. 存储 - 将ProbeVolume中的Surfel数据序列化保存
    /// 2. 加载 - 在运行时加载Surfel数据到探针
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "ProbeVolumeData", menuName = "NewVision/PRT/ProbeVolumeData")]
    public class ProbeVolumeData : ScriptableObject
    {
        /// <summary>
        /// 探针体的世界位置
        /// 用于检测位置变化
        /// </summary>
        [SerializeField]
        public Vector3 volumePosition;

        /// <summary>
        /// Surfel存储缓冲区
        /// 每个Surfel存储10个float：
        /// - position.x, position.y, position.z (3)
        /// - normal.x, normal.y, normal.z (3)
        /// - albedo.x, albedo.y, albedo.z (3)
        /// - skyMask (1)
        /// </summary>
        [SerializeField]
        public float[] surfelStorageBuffer;

        /// <summary>
        /// 存储Surfel数据
        /// 步骤1: 计算所需缓冲区大小
        /// 步骤2: 调整数组大小
        /// 步骤3: 遍历所有探针，序列化Surfel数据
        /// 步骤4: 记录探针体位置
        /// 步骤5: 标记资产为脏并保存
        /// </summary>
        public void StorageSurfelData(ProbeVolume volume)
        {
            int probeNum = volume.probeSizeX * volume.probeSizeY * volume.probeSizeZ;
            int surfelPerProbe = 512;
            int floatPerSurfel = 10;
            Array.Resize<float>(ref surfelStorageBuffer, probeNum * surfelPerProbe * floatPerSurfel);
            int j = 0;
            for (int i = 0; i < volume.probes.Length; i++)
            {
                Probe probe = volume.probes[i].GetComponent<Probe>();
                foreach (var surfel in probe.readBackBuffer)
                {
                    surfelStorageBuffer[j++] = surfel.position.x;
                    surfelStorageBuffer[j++] = surfel.position.y;
                    surfelStorageBuffer[j++] = surfel.position.z;
                    surfelStorageBuffer[j++] = surfel.normal.x;
                    surfelStorageBuffer[j++] = surfel.normal.y;
                    surfelStorageBuffer[j++] = surfel.normal.z;
                    surfelStorageBuffer[j++] = surfel.albedo.x;
                    surfelStorageBuffer[j++] = surfel.albedo.y;
                    surfelStorageBuffer[j++] = surfel.albedo.z;
                    surfelStorageBuffer[j++] = surfel.skyMask;
                }
            }

            volumePosition = volume.gameObject.transform.position;

            EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 尝试加载Surfel数据
        /// 步骤1: 检查数据大小是否匹配
        /// 步骤2: 检查位置是否变化
        /// 步骤3: 如果数据有效，反序列化Surfel数据到各探针
        /// 步骤4: 将数据上传到GPU
        /// </summary>
        public void TryLoadSurfelData(ProbeVolume volume)
        {
            int probeNum = volume.probeSizeX * volume.probeSizeY * volume.probeSizeZ;
            int surfelPerProbe = 512;
            int floatPerSurfel = 10;
            bool dataDirty = surfelStorageBuffer.Length != probeNum * surfelPerProbe * floatPerSurfel;
            bool posDirty = volume.gameObject.transform.position != volumePosition;
            if (posDirty || dataDirty)
            {
                Debug.LogWarning("volume data is old! please re capture!");
                Debug.LogWarning("探针组数据需要重新捕获");
                return;
            }

            int j = 0;
            foreach (var go in volume.probes)
            {
                Probe probe = go.GetComponent<Probe>();
                for (int i = 0; i < probe.readBackBuffer.Length; i++)
                {
                    probe.readBackBuffer[i].position.x = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].position.y = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].position.z = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].normal.x = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].normal.y = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].normal.z = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].albedo.x = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].albedo.y = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].albedo.z = surfelStorageBuffer[j++];
                    probe.readBackBuffer[i].skyMask = surfelStorageBuffer[j++];
                }
                probe.surfels.SetData(probe.readBackBuffer);
            }
        }
    }
}
