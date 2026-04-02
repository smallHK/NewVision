/**
 * LTC（Linearly Transformed Cosines）核心算法实现
 * 
 * 功能说明：
 * 实现LTC算法的核心函数，用于计算区域光的漫反射和高光贡献
 * 
 * 算法原理：
 * LTC是一种实时渲染区域光的技术，通过线性变换余弦分布来近似BRDF
 * 主要步骤：
 * 1. 将区域光多边形变换到规范化的积分域
 * 2. 使用边缘积分公式计算光照贡献
 * 3. 处理地平线裁剪
 * 
 * 参考文献：
 * "Real-Time Polygonal-Light Shading with Linearly Transformed Cosines"
 * by Eric Heitz et al., SIGGRAPH 2016
 */

#ifndef LTC_CORE_INCLUDED
#define LTC_CORE_INCLUDED

/**
 * 边缘积分函数
 * 
 * 功能：
 * 计算两个方向向量之间的边缘积分
 * 用于LTC算法中的多边形光照积分
 * 
 * 数学原理：
 * 对于两个单位向量v1和v2，计算：
 * I = ∫θ dθ  和  I*cos(θ) dθ
 * 其中θ是v1和v2之间的夹角
 * 
 * @param v1 第一个方向向量（需归一化）
 * @param v2 第二个方向向量（需归一化）
 * @return float2，x=θ（夹角），y=θ*cos(θ)
 */
float2 IntegrateEdge(float3 v1, float3 v2)
{
    // 计算两个向量的点积（cos夹角）
    float cosTheta = dot(v1, v2);
    
    // 使用acos计算夹角（弧度）
    float theta = acos(cosTheta);
    
    // 返回积分结果
    float2 result;
    result.x = theta;           // 边缘角度
    result.y = theta * cosTheta; // 加权角度（用于漫反射计算）
    return result;
}

/**
 * 将四边形裁剪到地平线以上
 * 
 * 功能：
 * 当区域光部分在地平线以下时，裁剪掉不可见的部分
 * 地平线定义为z=0平面（表面法线方向）
 * 
 * 算法步骤：
 * 1. 遍历四边形的每条边
 * 2. 判断边的两个端点是否在地平线以上（z>0）
 * 3. 根据端点状态进行裁剪：
 *    - 两点都在上方：保留终点
 *    - 起点在上方，终点在下方：计算交点
 *    - 起点在下方，终点在上方：计算交点并保留终点
 *    - 两点都在下方：不添加点
 * 
 * @param L 输入的四边形顶点数组（会被修改）
 * @return 裁剪后的顶点数量（0-5）
 */
int ClipQuadToHorizon(inout float3 L[5])
{
    int c = 0;  // 裁剪后的顶点计数
    float3 Lclipped[5];  // 裁剪后的顶点数组
    
    // 遍历四边形的四条边
    for (int i = 0; i < 4; i++)
    {
        int j = (i + 1) % 4;  // 下一个顶点的索引
        
        // 判断当前边两个端点是否在地平线以上
        bool iInside = L[i].z > 0.0;  // 起点是否可见
        bool jInside = L[j].z > 0.0;  // 终点是否可见
        
        // 根据端点可见性进行裁剪
        if (iInside && jInside)
        {
            // 情况1：两点都可见，保留终点
            Lclipped[c++] = L[j];
        }
        else if (iInside && !jInside)
        {
            // 情况2：起点可见，终点不可见
            // 计算与地平线的交点
            float t = L[i].z / (L[i].z - L[j].z);
            float3 intersect = L[i] + t * (L[j] - L[i]);
            Lclipped[c++] = intersect;
        }
        else if (!iInside && jInside)
        {
            // 情况3：起点不可见，终点可见
            // 计算与地平线的交点，并保留终点
            float t = L[i].z / (L[i].z - L[j].z);
            float3 intersect = L[i] + t * (L[j] - L[i]);
            Lclipped[c++] = intersect;
            Lclipped[c++] = L[j];
        }
        // 情况4：两点都不可见，不添加任何点
    }
    
    // 将裁剪后的顶点复制回原数组
    for (int k = 0; k < c; k++)
    {
        L[k] = Lclipped[k];
    }
    
    return c;  // 返回裁剪后的顶点数量
}

/**
 * LTC光照评估主函数
 * 
 * 功能：
 * 使用LTC算法计算区域光对表面的光照贡献
 * 
 * 算法流程：
 * 1. 构建切线空间基（基于表面法线和视图方向）
 * 2. 将区域光顶点变换到切线空间
 * 3. 应用LTC变换矩阵
 * 4. 裁剪到地平线以上
 * 5. 使用边缘积分公式计算光照
 * 
 * @param N 表面法线（世界空间，需归一化）
 * @param V 视图方向（世界空间，从表面指向相机，需归一化）
 * @param P 表面位置（世界空间）
 * @param Minv LTC变换逆矩阵（3x3）
 * @param lightVertices 区域光顶点矩阵（每列为一个顶点）
 * @return float4，r=漫反射强度，g=高光强度，b=未使用，a=1.0
 */
float4 LTC_Evaluate(float3 N, float3 V, float3 P, float3x3 Minv, float4x4 lightVertices)
{
    // ==================== 步骤1：构建切线空间基 ====================
    // 基于表面法线N和视图方向V构建正交基
    float3x3 basis;
    basis[2] = N;  // Z轴为法线方向
    // 计算副切线（在法线和视图方向构成的平面内，垂直于法线）
    basis[1] = normalize(V - N * dot(V, N));
    // 计算切线（垂直于法线和副切线）
    basis[0] = cross(basis[1], basis[2]);
    
    // ==================== 步骤2：变换光源顶点到切线空间 ====================
    // 将区域光的四个顶点变换到切线空间，并应用LTC变换
    float3 L[5];
    // 对于每个顶点：
    // 1. 计算顶点相对于表面位置P的偏移
    // 2. 应用LTC变换矩阵Minv
    // 3. 变换到切线空间
    L[0] = mul(basis, mul(Minv, lightVertices[0].xyz - P));
    L[1] = mul(basis, mul(Minv, lightVertices[1].xyz - P));
    L[2] = mul(basis, mul(Minv, lightVertices[2].xyz - P));
    L[3] = mul(basis, mul(Minv, lightVertices[3].xyz - P));
    L[4] = L[0];  // 闭合多边形
    
    // ==================== 步骤3：裁剪到地平线 ====================
    // 裁剪掉地平线以下的部分
    int n = ClipQuadToHorizon(L);
    
    // 如果裁剪后没有顶点，返回0
    if (n == 0)
        return float4(0, 0, 0, 1);
    
    // ==================== 步骤4：归一化顶点方向 ====================
    // 将顶点方向归一化，用于后续的边缘积分
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    if (n >= 3) L[3] = normalize(L[3]);
    if (n >= 4) L[4] = normalize(L[4]);
    
    // ==================== 步骤5：计算边缘积分 ====================
    // 使用边缘积分公式计算多边形光照
    float3 Lsum = float3(0, 0, 0);
    
    // 将多边形分解为三角形扇形，对每个三角形计算积分
    for (int i = 2; i < n; i++)
    {
        // 三角形的三个顶点
        float3 a = L[0];
        float3 b = L[i - 1];
        float3 c = L[i];
        
        // 计算三条边的积分
        float2 I1 = IntegrateEdge(a, b);
        float2 I2 = IntegrateEdge(b, c);
        float2 I3 = IntegrateEdge(c, a);
        
        // 累加边缘积分
        float2 I = I1 + I2 + I3;
        
        // 计算三角形法线方向（用于确定光照方向）
        float3 lightVec = cross(b - a, c - a);
        float len = length(lightVec);
        
        // 根据法线方向调整符号
        if (dot(lightVec, a) < 0.0)
            len = -len;
        
        // 累加光照贡献
        // x分量：漫反射贡献
        // y分量：高光贡献
        Lsum += len * float3(I.x, I.y, 0);
    }
    
    // ==================== 步骤6：归一化结果 ====================
    // 除以2π得到最终结果
    float3 result = Lsum / (2.0 * PI);
    
    return float4(result, 1.0);
}

#endif
