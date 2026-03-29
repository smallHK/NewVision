using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NewVision.PRT
{
    public struct Surfel
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 albedo;
        public float skyMask;
    }

    public enum ProbeDebugMode
    {
        None = 0,
        SphereDistribution = 1,
        SampleDirection = 2,
        Surfel = 3,
        SurfelRadiance = 4
    }

    [ExecuteAlways]
    public class Probe : MonoBehaviour
    {
        const int tX = 32;
        const int tY = 16;
        const int rayNum = tX * tY;         // 512 per probe
        const int surfelByteSize = 3 * 12 + 4;  // sizeof(Surfel)

        MaterialPropertyBlock matPropBlock;
        
        public Surfel[] readBackBuffer; // CPU side surfel array, for debug
        public ComputeBuffer surfels;   // GPU side surfel array

        Vector3[] radianceDebugBuffer;
        public ComputeBuffer surfelRadiance;

        const int coefficientSH9ByteSize = 9 * 3 * 4;
        int[] coefficientClearValue;
        public ComputeBuffer coefficientSH9; // GPU side SH9 coefficient, size: 9x3=27

        public RenderTexture RT_WorldPos;
        public RenderTexture RT_Normal;
        public RenderTexture RT_Albedo;

        public ComputeShader surfelSampleCS;
        public ComputeShader surfelReLightCS;

        [HideInInspector]
        public int indexInProbeVolume = -1; // set by parent
        ComputeBuffer tempBuffer;

        public ProbeDebugMode debugMode;

        void Start()
        {
            TryInit();
        }

        public void TryInit()
        {
            if(surfels==null) 
                surfels = new ComputeBuffer(rayNum, surfelByteSize);

            if(coefficientSH9==null) 
            {
                coefficientSH9 = new ComputeBuffer(27, sizeof(int));
                coefficientClearValue = new int[27];
                for(int i=0; i<27; i++) coefficientClearValue[i] = 0;
            }

            if(readBackBuffer==null) 
                readBackBuffer = new Surfel[rayNum];

            if(surfelRadiance==null) 
                surfelRadiance = new ComputeBuffer(rayNum, sizeof(float) * 3);
                
            if(radianceDebugBuffer==null) 
                radianceDebugBuffer = new Vector3[rayNum];
            
            if(matPropBlock==null)
                matPropBlock = new MaterialPropertyBlock();
            
            if(tempBuffer==null)
                tempBuffer = new ComputeBuffer(1, 4);
        }

        void OnDestroy()
        {
            if(surfels!=null) surfels.Release();
            if(coefficientSH9!=null) coefficientSH9.Release();
            if(surfelRadiance!=null) surfelRadiance.Release();
            if(tempBuffer!=null) tempBuffer.Release();
        }

        void OnDrawGizmos()
        {
            Vector3 probePos = gameObject.transform.position;
            
            gameObject.GetComponent<MeshRenderer>().enabled = !Application.isPlaying;
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial.shader = Shader.Find("NewVision/PRT/SHDebug");
            matPropBlock.SetBuffer("_coefficientSH9", coefficientSH9);
            meshRenderer.SetPropertyBlock(matPropBlock);
            

            if(debugMode == ProbeDebugMode.None)
                return;
            
            surfels.GetData(readBackBuffer);
            surfelRadiance.GetData(radianceDebugBuffer);
            

            for (int i=0; i<rayNum; i++)
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
                if(debugMode == ProbeDebugMode.SphereDistribution)
                {
                    if(isSky) Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(dir + probePos, 0.025f);
                }

                if(debugMode == ProbeDebugMode.SampleDirection)
                {
                    if(isSky)
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

                if(debugMode == ProbeDebugMode.Surfel)
                {
                    if(isSky) continue;
                    Gizmos.DrawSphere(pos, 0.05f);
                    Gizmos.DrawLine(pos, pos + normal * 0.25f);
                }

                if(debugMode == ProbeDebugMode.SurfelRadiance)
                {
                    if(isSky) continue;
                    Gizmos.color = new Color(radiance.x, radiance.y, radiance.z);
                    Gizmos.DrawSphere(pos, 0.05f);
                }
            }
        }

        void BatchSetShader(GameObject[] gameObjects, Shader shader)
        {
            foreach(var go in gameObjects)
            {
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                if(meshRenderer!=null)
                {
                    meshRenderer.sharedMaterial.shader = shader;
                }
            }
        }

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

        public void ReLight(CommandBuffer cmd)
        {
            var kid = surfelReLightCS.FindKernel("CSMain");

            Vector3 p = gameObject.transform.position;
            cmd.SetComputeVectorParam(surfelReLightCS, "_probePos", new Vector4(p.x, p.y, p.z, 1.0f));
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_surfels", surfels);
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_coefficientSH9", coefficientSH9);
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_surfelRadiance", surfelRadiance);

            var parent = transform.parent;
            ProbeVolume probeVolume = parent==null ? null : parent.gameObject.GetComponent<ProbeVolume>();
            ComputeBuffer coefficientVoxel = probeVolume==null ? tempBuffer : probeVolume.coefficientVoxel;
            cmd.SetComputeBufferParam(surfelReLightCS, kid, "_coefficientVoxel", coefficientVoxel);
            cmd.SetComputeIntParam(surfelReLightCS, "_indexInProbeVolume", indexInProbeVolume);

            cmd.SetBufferData(coefficientSH9, coefficientClearValue);
            cmd.DispatchCompute(surfelReLightCS, kid, 1, 1, 1);
        }
    }
}