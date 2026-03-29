using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace NewVision.LTC
{
    public class LTCTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool runTestsOnStart = false;
        public bool enableDebugLogging = true;
        
        [Header("Test Objects")]
        public GameObject testPlane;
        public GameObject testCube;
        public LTCAreaLight testAreaLight;
        
        [Header("Test Parameters")]
        public int maxAreaLights = 5;
        public float testDuration = 10.0f;
        
        private float testStartTime;
        private bool isTesting = false;
        private List<LTCAreaLight> testLights = new List<LTCAreaLight>();
        
        private void Start()
        {
            if (runTestsOnStart)
            {
                StartTests();
            }
        }
        
        private void Update()
        {
            if (isTesting && Time.time - testStartTime > testDuration)
            {
                StopTests();
            }
        }
        
        /// <summary>
        /// 开始执行所有测试
        /// 按照顺序执行基础功能测试、阴影测试和多光源测试
        /// </summary>
        public void StartTests()
        {
            if (enableDebugLogging)
            {
                Debug.Log("=== Starting LTC Area Light Tests ===");
            }
            
            testStartTime = Time.time;
            isTesting = true;
            
            // Test 1: Basic functionality test - 测试区域光的基本功能
            TestBasicFunctionality();
            
            // Test 2: Shadow test - 测试区域光的阴影功能
            TestShadows();
            
            // Test 3: Multiple lights test - 测试多个区域光同时存在的效果
            TestMultipleLights();
            
            if (enableDebugLogging)
            {
                Debug.Log("=== Tests started. Duration: " + testDuration + " seconds ===");
            }
        }
        
        public void StopTests()
        {
            isTesting = false;
            
            // Clean up test lights
            foreach (var light in testLights)
            {
                if (light != null)
                {
                    Destroy(light.gameObject);
                }
            }
            testLights.Clear();
            
            if (enableDebugLogging)
            {
                Debug.Log("=== LTC Area Light Tests Completed ===");
            }
        }
        
        /// <summary>
        /// 测试区域光的基本功能
        /// 创建测试场景，包括区域光、平面和立方体
        /// 验证区域光组件能够正确添加和配置
        /// </summary>
        private void TestBasicFunctionality()
        {
            if (enableDebugLogging)
            {
                Debug.Log("Test 1: Basic Functionality Test");
            }
            
            // 创建默认区域光（如果未指定）
            if (testAreaLight == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning("No test area light assigned. Creating a default one.");
                }
                
                GameObject lightObj = new GameObject("TestAreaLight");
                lightObj.transform.position = new Vector3(0, 2, 0);
                lightObj.transform.rotation = Quaternion.Euler(-90, 0, 0);
                
                // 添加平面网格作为区域光的形状
                MeshFilter meshFilter = lightObj.AddComponent<MeshFilter>();
                meshFilter.mesh = CreatePlaneMesh();
                
                MeshRenderer meshRenderer = lightObj.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                
                // 添加LTCAreaLight组件并设置基本参数
                testAreaLight = lightObj.AddComponent<LTCAreaLight>();
                testAreaLight.m_LightColor = Color.white;
                testAreaLight.m_Intensity = 2.0f;
            }
            
            // 创建测试平面（如果未指定）
            if (testPlane == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning("No test plane assigned. Creating a default one.");
                }
                
                testPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                testPlane.name = "TestPlane";
                testPlane.transform.position = new Vector3(0, 0, 0);
                testPlane.transform.localScale = new Vector3(5, 5, 5);
            }
            
            // 创建测试立方体（如果未指定）
            if (testCube == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning("No test cube assigned. Creating a default one.");
                }
                
                testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                testCube.name = "TestCube";
                testCube.transform.position = new Vector3(0, 0.5f, 0);
            }
            
            if (enableDebugLogging)
            {
                Debug.Log("Basic functionality test setup completed.");
            }
        }
        
        /// <summary>
        /// 测试区域光的阴影功能
        /// 启用区域光的阴影，并设置阴影参数
        /// 验证区域光是否能正确生成阴影
        /// </summary>
        private void TestShadows()
        {
            if (enableDebugLogging)
            {
                Debug.Log("Test 2: Shadow Test");
            }
            
            if (testAreaLight != null)
            {
                // 启用阴影功能
                testAreaLight.m_RenderShadow = true;
                // 设置阴影贴图分辨率
                testAreaLight.m_ShadowMapResolution = 1024;
                
                if (enableDebugLogging)
                {
                    Debug.Log("Shadow test enabled.");
                }
            }
        }
        
        /// <summary>
        /// 测试多个区域光同时存在的效果
        /// 在场景中创建多个区域光，分布在不同位置
        /// 每个光源使用不同的颜色，验证多光源叠加效果
        /// </summary>
        private void TestMultipleLights()
        {
            if (enableDebugLogging)
            {
                Debug.Log("Test 3: Multiple Lights Test");
            }
            
            // 创建多个区域光，均匀分布在圆周上
            for (int i = 1; i < maxAreaLights; i++)
            {
                float angle = (float)i / maxAreaLights * Mathf.PI * 2.0f;
                float distance = 3.0f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * distance,
                    2.0f,
                    Mathf.Sin(angle) * distance
                );
                
                GameObject lightObj = new GameObject("TestAreaLight_" + i);
                lightObj.transform.position = position;
                lightObj.transform.rotation = Quaternion.Euler(-90, 0, 0);
                
                // 添加平面网格作为区域光的形状
                MeshFilter meshFilter = lightObj.AddComponent<MeshFilter>();
                meshFilter.mesh = CreatePlaneMesh();
                
                MeshRenderer meshRenderer = lightObj.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                
                // 添加LTCAreaLight组件并设置颜色（基于角度）
                LTCAreaLight areaLight = lightObj.AddComponent<LTCAreaLight>();
                areaLight.m_LightColor = new Color(
                    0.5f + Mathf.Sin(angle) * 0.5f,
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f
                );
                areaLight.m_Intensity = 1.0f;
                
                // 将创建的光源添加到测试列表中
                testLights.Add(areaLight);
            }
            
            if (enableDebugLogging)
            {
                Debug.Log("Created " + (maxAreaLights - 1) + " additional area lights for multiple lights test.");
            }
        }
        
        /// <summary>
        /// 创建平面网格，用于区域光的形状
        /// 生成一个简单的2x2平面网格
        /// </summary>
        /// <returns>创建的平面网格</returns>
        private Mesh CreatePlaneMesh()
        {
            Mesh mesh = new Mesh();
            
            // 创建四个顶点，形成一个2x2的平面
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-1, 0, -1);
            vertices[1] = new Vector3(1, 0, -1);
            vertices[2] = new Vector3(1, 0, 1);
            vertices[3] = new Vector3(-1, 0, 1);
            
            // 创建三角形索引
            int[] triangles = new int[6];
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            triangles[3] = 0;
            triangles[4] = 2;
            triangles[5] = 3;
            
            // 创建UV坐标
            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(1, 1);
            uv[3] = new Vector2(0, 1);
            
            // 设置网格数据
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            
            return mesh;
        }
        
        /// <summary>
        /// 记录当前场景中的区域光数量
        /// 用于验证区域光管理器是否正确注册和管理光源
        /// </summary>
        public void LogLightCount()
        {
            int lightCount = LTCAreaLightManager.GetCount();
            if (enableDebugLogging)
            {
                Debug.Log("Current area light count: " + lightCount);
            }
        }
        
        /// <summary>
        /// 切换调试日志的启用状态
        /// 用于控制测试过程中的日志输出
        /// </summary>
        public void ToggleDebugLogging()
        {
            enableDebugLogging = !enableDebugLogging;
            if (enableDebugLogging)
            {
                Debug.Log("Debug logging enabled.");
            }
            else
            {
                Debug.Log("Debug logging disabled.");
            }
        }
    }
}