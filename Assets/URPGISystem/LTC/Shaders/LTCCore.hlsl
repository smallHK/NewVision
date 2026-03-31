#ifndef LTC_CORE_INCLUDED
#define LTC_CORE_INCLUDED

float2 IntegrateEdge(float3 v1, float3 v2)
{
    float cosTheta = dot(v1, v2);
    float theta = acos(cosTheta);
    float2 result;
    result.x = theta;
    result.y = theta * cosTheta;
    return result;
}

int ClipQuadToHorizon(inout float3 L[5])
{
    int c = 0;
    float3 Lclipped[5];
    
    for (int i = 0; i < 4; i++)
    {
        int j = (i + 1) % 4;
        
        bool iInside = L[i].z > 0.0;
        bool jInside = L[j].z > 0.0;
        
        if (iInside && jInside)
        {
            Lclipped[c++] = L[j];
        }
        else if (iInside && !jInside)
        {
            float t = L[i].z / (L[i].z - L[j].z);
            float3 intersect = L[i] + t * (L[j] - L[i]);
            Lclipped[c++] = intersect;
        }
        else if (!iInside && jInside)
        {
            float t = L[i].z / (L[i].z - L[j].z);
            float3 intersect = L[i] + t * (L[j] - L[i]);
            Lclipped[c++] = intersect;
            Lclipped[c++] = L[j];
        }
    }
    
    for (int k = 0; k < c; k++)
    {
        L[k] = Lclipped[k];
    }
    
    return c;
}

float4 LTC_Evaluate(float3 N, float3 V, float3 P, float3x3 Minv, float4x4 lightVertices)
{
    float3x3 basis;
    basis[2] = N;
    basis[1] = normalize(V - N * dot(V, N));
    basis[0] = cross(basis[1], basis[2]);
    
    float3 L[5];
    L[0] = mul(basis, mul(Minv, lightVertices[0].xyz - P));
    L[1] = mul(basis, mul(Minv, lightVertices[1].xyz - P));
    L[2] = mul(basis, mul(Minv, lightVertices[2].xyz - P));
    L[3] = mul(basis, mul(Minv, lightVertices[3].xyz - P));
    L[4] = L[0];
    
    int n = ClipQuadToHorizon(L);
    
    if (n == 0)
        return float4(0, 0, 0, 1);
    
    L[0] = normalize(L[0]);
    L[1] = normalize(L[1]);
    L[2] = normalize(L[2]);
    if (n >= 3) L[3] = normalize(L[3]);
    if (n >= 4) L[4] = normalize(L[4]);
    
    float3 Lsum = float3(0, 0, 0);
    
    for (int i = 2; i < n; i++)
    {
        float3 a = L[0];
        float3 b = L[i - 1];
        float3 c = L[i];
        
        float2 I1 = IntegrateEdge(a, b);
        float2 I2 = IntegrateEdge(b, c);
        float2 I3 = IntegrateEdge(c, a);
        
        float2 I = I1 + I2 + I3;
        
        float3 lightVec = cross(b - a, c - a);
        float len = length(lightVec);
        if (dot(lightVec, a) < 0.0)
            len = -len;
        
        Lsum += len * float3(I.x, I.y, 0);
    }
    
    float3 result = Lsum / (2.0 * PI);
    
    return float4(result, 1.0);
}

#endif
