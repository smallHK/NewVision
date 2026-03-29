float2 IntegrateEdge(float3 v1, float3 v2)
{
    float cosTheta = dot(v1, v2);
    float theta = acos(cosTheta);
    float2 result;
    result.x = 0.5 * (1.0 + cosTheta);
    result.y = theta - sin(theta) * cosTheta;
    return result;
}

float2 IntegratePolygon(float3 n, float3x3 basis, float3 p0, float3 p1, float3 p2, float3 p3)
{
    float2 sum = float2(0, 0);
    float3 v0 = p0;
    float3 v1 = p1;
    float3 v2 = p2;
    float3 v3 = p3;
    
    sum += IntegrateEdge(v0, v1);
    sum += IntegrateEdge(v1, v2);
    sum += IntegrateEdge(v2, v3);
    sum += IntegrateEdge(v3, v0);
    
    return sum;
}

float4 LTC_Evaluate(float3 N, float3 V, float3 P, float3x3 invMat, float4x4 lightVertices)
{
    float3x3 R = float3x3(
        lightVertices[0].xyz - P,
        lightVertices[1].xyz - P,
        lightVertices[2].xyz - P
    );
    
    float3x3 M = mul(invMat, R);
    
    float3x3 basis = float3x3(
        normalize(M[0]),
        normalize(cross(M[0], M[1])),
        normalize(cross(normalize(M[0]), normalize(cross(M[0], M[1]))))
    );
    
    float3 p0 = mul(basis, M[0]);
    float3 p1 = mul(basis, M[1]);
    float3 p2 = mul(basis, M[2]);
    float3 p3 = mul(basis, M[3].xyz - P);
    
    float2 integral = IntegratePolygon(float3(0, 0, 1), basis, p0, p1, p2, p3);
    
    float4 result;
    result.r = integral.x;
    result.g = integral.y;
    result.b = 0;
    result.a = 0;
    
    return result;
}