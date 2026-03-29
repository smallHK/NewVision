using UnityEngine;
using UnityEditor;
using NewVision.PRT;

namespace NewVision.PRT.Test
{
    /// <summary>
    /// PRT模块测试脚本
    /// 用于自动创建测试场景并验证PRT功能
    /// </summary>
    public class PRTTest : MonoBehaviour
    {
        /// <summary>
        /// 测试场景的宽度
        /// </summary>
        public int sceneWidth = 20;
        
        /// <summary>
        /// 测试场景的高度
        /// </summary>
        public int sceneHeight = 10;
        
        /// <summary>
        /// 测试场景的深度
        /// </summary>
        public int sceneDepth = 20;
        
        /// <summary>
        /// 探针网格大小
        /// </summary>
        public Vector3Int probeGridSize = new Vector3Int(5, 3, 5);
        
        /// <summary>
        /// 探针间距
        /// </summary>
        public float probeSpacing = 4.0f;
        
        /// <summary>
        /// 测试物体数量
        /// </summary>
        public int testObjectCount = 10;
        
        /// <summary>
        /// 测试材质
        /// </summary>
        public Material testMaterial;
        
        /// <summary>
        /// 测试场景根物体
        /// </summary>
        private GameObject testSceneRoot;
        
        /// <summary>
        /// 探针体组件
        /// </summary>
        private ProbeVolume probeVolume;
        
        /// <summary>
        /// 测试开始
        /// </summary>
        [ContextMenu("Start PRT Test")]
        public void StartPRTTest()
        {
            // 清理旧的测试场景
            CleanupTestScene();
            
            // 创建测试场景
            CreateTestScene();
            
            // 创建探针体
            CreateProbeVolume();
            
            // 创建测试物体
            CreateTestObjects();
            
            // 配置PRT组件
            ConfigurePRT();
            
            Debug.Log("PRT测试场景创建完成！");
        }
        
        /// <summary>
        /// 清理旧的测试场景
        /// </summary>
        private void CleanupTestScene()
        {
            if (testSceneRoot != null)
            {
                DestroyImmediate(testSceneRoot);
            }
            
            // 清理场景中的其他测试对象
            GameObject[] testObjects = GameObject.FindGameObjectsWithTag("PRTTest");
            foreach (GameObject obj in testObjects)
            {
                DestroyImmediate(obj);
            }
        }
        
        /// <summary>
        /// 创建测试场景
        /// </summary>
        private void CreateTestScene()
        {
            // 创建场景根物体
            testSceneRoot = new GameObject("PRTTestScene");
            testSceneRoot.tag = "PRTTest";
            
            // 创建地面
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.parent = testSceneRoot.transform;
            ground.transform.localScale = new Vector3(sceneWidth / 10f, 1, sceneDepth / 10f);
            ground.transform.position = new Vector3(0, 0, 0);
            ground.tag = "PRTTest";
            
            // 创建墙壁
            // 后墙
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.transform.parent = testSceneRoot.transform;
            backWall.transform.localScale = new Vector3(sceneWidth, sceneHeight, 1);
            backWall.transform.position = new Vector3(0, sceneHeight / 2f, -sceneDepth / 2f);
            backWall.tag = "PRTTest";
            
            // 左墙
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.transform.parent = testSceneRoot.transform;
            leftWall.transform.localScale = new Vector3(1, sceneHeight, sceneDepth);
            leftWall.transform.position = new Vector3(-sceneWidth / 2f, sceneHeight / 2f, 0);
            leftWall.tag = "PRTTest";
            
            // 右墙
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.transform.parent = testSceneRoot.transform;
            rightWall.transform.localScale = new Vector3(1, sceneHeight, sceneDepth);
            rightWall.transform.position = new Vector3(sceneWidth / 2f, sceneHeight / 2f, 0);
            rightWall.tag = "PRTTest";
            
            // 天花板
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.parent = testSceneRoot.transform;
            ceiling.transform.localScale = new Vector3(sceneWidth, 1, sceneDepth);
            ceiling.transform.position = new Vector3(0, sceneHeight, 0);
            ceiling.tag = "PRTTest";
            
            // 添加方向光
            GameObject directionalLight = new GameObject("DirectionalLight");
            directionalLight.transform.parent = testSceneRoot.transform;
            directionalLight.transform.rotation = Quaternion.Euler(45, 45, 0);
            Light light = directionalLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = Color.white;
            directionalLight.tag = "PRTTest";
        }
        
        /// <summary>
        /// 创建探针体
        /// </summary>
        private void CreateProbeVolume()
        {
            // 创建探针体对象
            GameObject probeVolumeObj = new GameObject("PRTProbeVolume");
            probeVolumeObj.transform.parent = testSceneRoot.transform;
            probeVolumeObj.transform.position = new Vector3(0, sceneHeight / 2f, 0);
            probeVolume = probeVolumeObj.AddComponent<ProbeVolume>();
            probeVolume.tag = "PRTTest";
            
            // 配置探针体参数
            probeVolume.probeSizeX = probeGridSize.x;
            probeVolume.probeSizeY = probeGridSize.y;
            probeVolume.probeSizeZ = probeGridSize.z;
            probeVolume.probeGridSize = probeSpacing;
            
            // 创建Probe预制体
            GameObject probePrefab = CreateProbePrefab();
            probeVolume.probePrefab = probePrefab;
            
            // 创建ProbeVolumeData
            ProbeVolumeData data = CreateProbeVolumeData();
            probeVolume.data = data;
            
            // 生成探针
            probeVolume.GenerateProbes();
        }
        
        /// <summary>
        /// 创建Probe预制体
        /// </summary>
        /// <returns>Probe预制体</returns>
        private GameObject CreateProbePrefab()
        {
            // 创建探针对象
            GameObject probeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probeObj.name = "Probe";
            probeObj.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            
            // 添加Probe组件
            Probe probe = probeObj.AddComponent<Probe>();
            
            // 创建Cubemap RenderTexture
            RenderTextureDescriptor rtDesc = new RenderTextureDescriptor(128, 128, RenderTextureFormat.ARGB32, 0);
            rtDesc.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            
            RenderTexture worldPosRT = new RenderTexture(rtDesc);
            worldPosRT.name = "RT_WorldPos";
            probe.RT_WorldPos = worldPosRT;
            
            RenderTexture normalRT = new RenderTexture(rtDesc);
            normalRT.name = "RT_Normal";
            probe.RT_Normal = normalRT;
            
            RenderTexture albedoRT = new RenderTexture(rtDesc);
            albedoRT.name = "RT_Albedo";
            probe.RT_Albedo = albedoRT;
            
            // 加载计算着色器
            probe.surfelSampleCS = Resources.Load<ComputeShader>("SurfelSampleCS");
            probe.surfelReLightCS = Resources.Load<ComputeShader>("SurfelReLightCS");
            
            // 保存为预制体
            string prefabPath = "Assets/URPGISystem/PRT/Test/Probe.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(probeObj, prefabPath);
            
            // 销毁临时对象
            DestroyImmediate(probeObj);
            
            return prefab;
        }
        
        /// <summary>
        /// 创建ProbeVolumeData
        /// </summary>
        /// <returns>ProbeVolumeData实例</returns>
        private ProbeVolumeData CreateProbeVolumeData()
        {
            ProbeVolumeData data = ScriptableObject.CreateInstance<ProbeVolumeData>();
            string assetPath = "Assets/URPGISystem/PRT/Test/ProbeVolumeData.asset";
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
            return data;
        }
        
        /// <summary>
        /// 创建测试物体
        /// </summary>
        private void CreateTestObjects()
        {
            for (int i = 0; i < testObjectCount; i++)
            {
                // 随机位置
                float x = Random.Range(-sceneWidth / 2f + 1, sceneWidth / 2f - 1);
                float y = Random.Range(1, sceneHeight - 1);
                float z = Random.Range(-sceneDepth / 2f + 1, sceneDepth / 2f - 1);
                
                // 创建物体
                GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                testObj.transform.parent = testSceneRoot.transform;
                testObj.transform.position = new Vector3(x, y, z);
                testObj.transform.localScale = new Vector3(1, 1, 1);
                testObj.tag = "PRTTest";
                
                // 应用材质
                if (testMaterial != null)
                {
                    testObj.GetComponent<MeshRenderer>().sharedMaterial = testMaterial;
                }
            }
        }
        
        /// <summary>
        /// 配置PRT组件
        /// </summary>
        private void ConfigurePRT()
        {
            // 这里可以添加额外的PRT配置代码
            // 例如配置RenderFeature等
        }
        
        /// <summary>
        /// 捕获探针数据
        /// </summary>
        [ContextMenu("Capture Probe Data")]
        public void CaptureProbeData()
        {
            if (probeVolume != null)
            {
                probeVolume.ProbeCapture();
                Debug.Log("探针数据捕获完成！");
            }
            else
            {
                Debug.LogError("探针体不存在，请先创建测试场景！");
            }
        }
        
        /// <summary>
        /// 验证PRT效果
        /// </summary>
        [ContextMenu("Verify PRT Effect")]
        public void VerifyPRTEffect()
        {
            // 检查探针是否生成
            if (probeVolume == null || probeVolume.probes == null || probeVolume.probes.Length == 0)
            {
                Debug.LogError("探针未生成，请先创建测试场景！");
                return;
            }
            
            // 检查探针数据是否捕获
            if (probeVolume.data == null || probeVolume.data.surfelStorageBuffer == null || probeVolume.data.surfelStorageBuffer.Length == 0)
            {
                Debug.LogError("探针数据未捕获，请先执行Capture Probe Data！");
                return;
            }
            
            Debug.Log("PRT效果验证完成，系统工作正常！");
            Debug.Log($"探针数量: {probeVolume.probes.Length}");
            Debug.Log($"探针数据大小: {probeVolume.data.surfelStorageBuffer.Length} 浮点数");
        }
    }
}
