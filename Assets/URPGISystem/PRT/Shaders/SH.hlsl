// ref: https://www.shadertoy.com/view/lsfXWH
// 球谐函数库
// 提供SH9（二阶球谐）相关的计算函数

// 步骤1: 球谐基函数 Y_l_m(s)
// l是阶数，m是范围[-l..l]
// 返回方向s处的SH基函数值
float SH(in int l, in int m, in float3 s) 
{ 
    // SH系数常量
    #define k01 0.2820947918    // sqrt(  1/PI)/2
    #define k02 0.4886025119    // sqrt(  3/PI)/2
    #define k03 1.0925484306    // sqrt( 15/PI)/2
    #define k04 0.3153915652    // sqrt(  5/PI)/4
    #define k05 0.5462742153    // sqrt( 15/PI)/4

    // 步骤1.1: 坐标变换（适应Unity坐标系）
    float x = s.x;
    float y = s.z;
    float z = s.y;
	
    //----------------------------------------------------------
    // 步骤1.2: L0阶（1个基函数）
    if( l==0 )          return  k01;
    //----------------------------------------------------------
    // 步骤1.3: L1阶（3个基函数）
	if( l==1 && m==-1 ) return  k02*y;
    if( l==1 && m== 0 ) return  k02*z;
    if( l==1 && m== 1 ) return  k02*x;
    //----------------------------------------------------------
    // 步骤1.4: L2阶（5个基函数）
	if( l==2 && m==-2 ) return  k03*x*y;
    if( l==2 && m==-1 ) return  k03*y*z;
    if( l==2 && m== 0 ) return  k04*(2.0*z*z-x*x-y*y);
    if( l==2 && m== 1 ) return  k03*x*z;
    if( l==2 && m== 2 ) return  k05*(x*x-y*y);

	return 0.0;
}
 
// 步骤2: 从SH9系数解码辐照度
// c[9]是9个SH系数，dir是采样方向
float3 IrradianceSH9(in float3 c[9], in float3 dir)
{
    // 辐照度积分常数
    #define A0 3.1415
    #define A1 2.0943
    #define A2 0.7853

    // 步骤2.1: 累加各阶SH贡献
    float3 irradiance = float3(0, 0, 0);
    irradiance += SH(0,  0, dir) * c[0] * A0;
    irradiance += SH(1, -1, dir) * c[1] * A1;
    irradiance += SH(1,  0, dir) * c[2] * A1;
    irradiance += SH(1,  1, dir) * c[3] * A1;
    irradiance += SH(2, -2, dir) * c[4] * A2;
    irradiance += SH(2, -1, dir) * c[5] * A2;
    irradiance += SH(2,  0, dir) * c[6] * A2;
    irradiance += SH(2,  1, dir) * c[7] * A2;
    irradiance += SH(2,  2, dir) * c[8] * A2;
    // 步骤2.2: 限制为非负值
    irradiance = max(float3(0, 0, 0), irradiance);

    return irradiance;
}

// 步骤3: 定点数编码/解码
// 使用定点数存储小数，保留小数点后5位
// 因为Compute Shader的InterlockedAdd不支持float
#define FIXED_SCALE 100000.0

// 步骤3.1: 将浮点数编码为整数
int EncodeFloatToInt(float x)
{
    return int(x * FIXED_SCALE);
}

// 步骤3.2: 从整数解码浮点数
float DecodeFloatFromInt(int x)
{
    return float(x) / FIXED_SCALE;
}

// 步骤4: 从世界位置计算探针3D索引
int3 GetProbeIndex3DFromWorldPos(float3 worldPos, float3 coefficientVoxelSize, float coefficientVoxelGridSize, float3 coefficientVoxelCorner)
{
    float3 probeIndexF = floor((worldPos - coefficientVoxelCorner) / coefficientVoxelGridSize);
    int3 probeIndex3 = int3(probeIndexF.x, probeIndexF.y, probeIndexF.z);
    return probeIndex3;
}

// 步骤5: 从3D索引计算1D索引
int GetProbeIndex1DFromIndex3D(int3 probeIndex3, float3 coefficientVoxelSize)
{
    int probeIndex = probeIndex3.x * coefficientVoxelSize.y * coefficientVoxelSize.z
                    + probeIndex3.y * coefficientVoxelSize.z 
                    + probeIndex3.z;
    return probeIndex;
}

// 步骤6: 检查3D索引是否在体素范围内
bool IsIndex3DInsideVoxel(int3 probeIndex3, float3 coefficientVoxelSize)
{
    bool isInsideVoxelX = 0 <= probeIndex3.x && probeIndex3.x < coefficientVoxelSize.x;
    bool isInsideVoxelY = 0 <= probeIndex3.y && probeIndex3.y < coefficientVoxelSize.y;
    bool isInsideVoxelZ = 0 <= probeIndex3.z && probeIndex3.z < coefficientVoxelSize.z;
    bool isInsideVoxel = isInsideVoxelX && isInsideVoxelY && isInsideVoxelZ;
    return isInsideVoxel;
}

// 步骤7: 从体素缓冲区解码SH系数（StructuredBuffer版本）
void DecodeSHCoefficientFromVoxel(inout float3 c[9], in StructuredBuffer<int> coefficientVoxel, int probeIndex)
{
    const int coefficientByteSize = 27; // 3x9 for SH9 RGB
    int offset = probeIndex * coefficientByteSize;   
    for(int i=0; i<9; i++)
    {
        c[i].x = DecodeFloatFromInt(coefficientVoxel[offset + i*3+0]);
        c[i].y = DecodeFloatFromInt(coefficientVoxel[offset + i*3+1]);
        c[i].z = DecodeFloatFromInt(coefficientVoxel[offset + i*3+2]);
    }
}

// 步骤7.1: 从体素缓冲区解码SH系数（RWStructuredBuffer版本）
void DecodeSHCoefficientFromVoxelRW(inout float3 c[9], in RWStructuredBuffer<int> coefficientVoxel, int probeIndex)
{
    const int coefficientByteSize = 27; // 3x9 for SH9 RGB
    int offset = probeIndex * coefficientByteSize;   
    for(int i=0; i<9; i++)
    {
        c[i].x = DecodeFloatFromInt(coefficientVoxel[offset + i*3+0]);
        c[i].y = DecodeFloatFromInt(coefficientVoxel[offset + i*3+1]);
        c[i].z = DecodeFloatFromInt(coefficientVoxel[offset + i*3+2]);
    }
}

// 步骤8: 从3D索引计算探针世界位置
float3 GetProbePositionFromIndex3D(int3 probeIndex3, float coefficientVoxelGridSize, float3 coefficientVoxelCorner)
{
    float3 res = float3(probeIndex3.x, probeIndex3.y, probeIndex3.z) * coefficientVoxelGridSize + coefficientVoxelCorner;
    return res;
}

// 步骤9: 三线性插值（float3版本）
float3 TrilinearInterpolationFloat3(in float3 value[8], float3 rate)
{
    float3 a = lerp(value[0], value[4], rate.x);    // 000, 100
    float3 b = lerp(value[2], value[6], rate.x);    // 010, 110
    float3 c = lerp(value[1], value[5], rate.x);    // 001, 101
    float3 d = lerp(value[3], value[7], rate.x);    // 011, 111
    float3 e = lerp(a, b, rate.y);
    float3 f = lerp(c, d, rate.y);
    float3 g = lerp(e, f, rate.z); 
    return g;
}

// 步骤10: 从SH体素采样间接光照
// 实现三线性插值和法线权重混合
float3 SampleSHVoxel(
    in float4 worldPos, 
    in float3 albedo, 
    in float3 normal,
    in StructuredBuffer<int> coefficientVoxel,
    in float coefficientVoxelGridSize,
    in float4 coefficientVoxelCorner,
    in float4 coefficientVoxelSize
    )
{
    // 步骤10.1: 计算当前片元的探针网格索引
    int3 probeIndex3 = GetProbeIndex3DFromWorldPos(worldPos.xyz, coefficientVoxelSize.xyz, coefficientVoxelGridSize, coefficientVoxelCorner.xyz);
    // 步骤10.2: 定义8个相邻探针的偏移
    int3 offset[8] = {
        int3(0, 0, 0), int3(0, 0, 1), int3(0, 1, 0), int3(0, 1, 1), 
        int3(1, 0, 0), int3(1, 0, 1), int3(1, 1, 0), int3(1, 1, 1), 
    };

    float3 c[9];
    float3 Lo[8] = { float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), };
    float3 BRDF = albedo / PI;
    float weight = 0.0005;

    // 步骤10.3: 遍历相邻的8个探针
    for(int i=0; i<8; i++)
    {
        int3 idx3 = probeIndex3 + offset[i];
        bool isInsideVoxel = IsIndex3DInsideVoxel(idx3, coefficientVoxelSize.xyz);
        if(!isInsideVoxel) 
        {
            Lo[i] = float3(0, 0, 0);
            continue;
        }

        // 步骤10.4: 计算法线权重
        float3 probePos = GetProbePositionFromIndex3D(idx3, coefficientVoxelGridSize, coefficientVoxelCorner.xyz);
        float3 dir = normalize(probePos - worldPos.xyz);
        float normalWeight = saturate(dot(dir, normal));
        weight += normalWeight;

        // 步骤10.5: 解码SH9系数并计算辐照度
        int probeIndex = GetProbeIndex1DFromIndex3D(idx3, coefficientVoxelSize.xyz);
        DecodeSHCoefficientFromVoxel(c, coefficientVoxel, probeIndex);
        Lo[i] = IrradianceSH9(c, normal) * BRDF * normalWeight;      
    }

    // 步骤10.6: 三线性插值
    float3 minCorner = GetProbePositionFromIndex3D(probeIndex3, coefficientVoxelGridSize, coefficientVoxelCorner.xyz);
    float3 maxCorner = minCorner + float3(1, 1, 1) * coefficientVoxelGridSize;
    float3 rate = (worldPos.xyz - minCorner) / coefficientVoxelGridSize;
    float3 color = TrilinearInterpolationFloat3(Lo, rate) / weight;
    
    return color;
}
