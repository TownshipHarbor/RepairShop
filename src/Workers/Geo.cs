using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace RepairShopQuest
{
    internal static class Geo
    {
        internal static Il2CppReferenceArray<Collider> Colliders(int n)
        {
            return new Il2CppReferenceArray<Collider>(Il2CppSystem.Array.CreateInstance(Il2CppType.Of<Collider>(), n).Pointer);
        }

        internal static Il2CppStructArray<RaycastHit> Hits(int n)
        {
            return new Il2CppStructArray<RaycastHit>(n);
        }

        internal static int OverlapAabb(PhysicsScene scene, Vector3 center, Vector3 half, Il2CppReferenceArray<Collider> hits, int mask, QueryTriggerInteraction triggers)
        {
            Vector3 axis;
            float along, radius;
            if (half.x >= half.y && half.x >= half.z) { axis = Vector3.right; along = half.x; radius = Mathf.Sqrt(half.y * half.y + half.z * half.z); }
            else if (half.y >= half.z) { axis = Vector3.up; along = half.y; radius = Mathf.Sqrt(half.x * half.x + half.z * half.z); }
            else { axis = Vector3.forward; along = half.z; radius = Mathf.Sqrt(half.x * half.x + half.y * half.y); }
            float reach = Mathf.Max(0f, along - radius);
            int n = scene.OverlapCapsule(center - axis * reach, center + axis * reach, radius, hits, mask, triggers);
            if (n >= hits.Length) return n;
            Vector3 min = center - half, max = center + half;
            int kept = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i];
                if (c == null) continue;
                Bounds b = c.bounds;
                Vector3 bmin = b.center - b.extents, bmax = b.center + b.extents;
                if (bmin.x > max.x || bmax.x < min.x || bmin.y > max.y || bmax.y < min.y || bmin.z > max.z || bmax.z < min.z) continue;
                hits[kept++] = c;
            }
            for (int i = kept; i < n; i++) hits[i] = null;
            return kept;
        }

        internal static float SqrDistance(Bounds b, Vector3 p)
        {
            Vector3 min = b.center - b.extents, max = b.center + b.extents;
            float dx = p.x < min.x ? min.x - p.x : p.x > max.x ? p.x - max.x : 0f;
            float dy = p.y < min.y ? min.y - p.y : p.y > max.y ? p.y - max.y : 0f;
            float dz = p.z < min.z ? min.z - p.z : p.z > max.z ? p.z - max.z : 0f;
            return dx * dx + dy * dy + dz * dz;
        }

        internal static bool Empty(MaterialPropertyBlock block)
        {
            return true;
        }

        internal static int StateHash(AnimatorStateInfo info)
        {
            return info.m_Name;
        }
    }
}
