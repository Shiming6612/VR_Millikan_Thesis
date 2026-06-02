using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public static class DebugTools
    {
        public static GameObject DebugPrimitive(Vector3 pos, Quaternion rotation = default, PrimitiveType type = PrimitiveType.Cube, float scale = 0.01f, float delayBeforeDestroy = 3, string name = null)
        {
            if(rotation == default) rotation = Quaternion.identity;
            var primitive = GameObject.CreatePrimitive(type);
            primitive.transform.position = pos;
            primitive.transform.rotation = rotation;
            primitive.transform.localScale = scale * Vector3.one;
            if (delayBeforeDestroy > 0) GameObject.Destroy(primitive, delayBeforeDestroy);
            if(name != null) primitive.name = name;
            return primitive;
        }
    }
}

