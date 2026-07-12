/// this file stores all the SDF functions that are used in the SDF shaders

// SDFS
float SphereSDF(float3 p, float3 center, float radius)
{
    return length(p - center) - radius;
}





// the scene, given a position
float FinalSDF(float3 pos)
{
    
    return SphereSDF(pos, float3(0, 0, 0), 10.0);
    
}




