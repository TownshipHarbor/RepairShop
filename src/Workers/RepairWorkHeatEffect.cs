using System;
using System.Linq;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Heat;
using Il2CppAlta.Networking;

namespace RepairShopQuest
{
    internal sealed class RepairWorkHeatEffect : IDisposable
    {
        private static PooledObjectDefinition ironEffect;

        private static float nextIronLookup;

        private Transform anchor;

        private PooledObject effect;

        private PooledObjectDefinition activeDefinition;

        private float nextRequest;

        private float warnAfter;

        private bool disposed;

        internal bool IsActive
        {
            get
            {
                if (!disposed && effect != null)
                {
                    return !effect.IsReleased;
                }
                return false;
            }
        }

        internal void Update(PhysicalMaterial material, Transform parent, Vector3 worldPoint, float temperature)
        {
            if (disposed)
            {
                return;
            }
            if (material == null || parent == null || !Finite(worldPoint) || !Finite(temperature))
            {
                Release(clear: true);
                return;
            }
            float maxTemperatureForParticles = material.MaxTemperatureForParticles;
            if (!Finite(maxTemperatureForParticles) || temperature <= maxTemperatureForParticles)
            {
                Release(clear: false);
                return;
            }
            float unscaledTime = Time.unscaledTime;
            try
            {
                PooledObjectDefinition pooledObjectDefinition = material.EffectOnFullyHeated;
                if (pooledObjectDefinition == null)
                {
                    pooledObjectDefinition = FindIronEffect(unscaledTime);
                }
                if (pooledObjectDefinition == null)
                {
                    Release(clear: false);
                    return;
                }
                if (effect != null && (effect.IsReleased || activeDefinition != pooledObjectDefinition))
                {
                    Release(clear: false);
                }
                if (anchor == null)
                {
                    anchor = new GameObject("Repair work native heat effect anchor").transform;
                    anchor.gameObject.hideFlags = HideFlags.DontSave;
                }
                if (anchor.parent != parent)
                {
                    anchor.SetParent(parent, worldPositionStays: false);
                }
                anchor.SetPositionAndRotation(worldPoint, parent.rotation);
                anchor.localScale = Vector3.one;
                if (!(effect != null) && !(unscaledTime < nextRequest))
                {
                    nextRequest = unscaledTime + 2f;
                    if (!Safe(pooledObjectDefinition))
                    {
                        Warn(unscaledTime, "Native hot-metal effect is unavailable or contains gameplay components.");
                        return;
                    }
                    effect = pooledObjectDefinition.Get(anchor);
                    activeDefinition = ((effect != null) ? pooledObjectDefinition : null);
                }
            }
            catch (Exception ex)
            {
                Release(clear: true);
                nextRequest = unscaledTime + 5f;
                Warn(unscaledTime, ex.Message);
            }
        }

        private static PooledObjectDefinition FindIronEffect(float now)
        {
            if (ironEffect != null)
            {
                return ironEffect;
            }
            if (now < nextIronLookup)
            {
                return null;
            }
            nextIronLookup = now + 5f;
            PhysicalMaterial[] array = Plat.Loaded<PhysicalMaterial>().Select(o => Plat.As<PhysicalMaterial>(o)).Where(x => x != null).ToArray();
            foreach (PhysicalMaterial physicalMaterial in array)
            {
                if (physicalMaterial != null && string.Equals(physicalMaterial.name, "Iron", StringComparison.OrdinalIgnoreCase) && physicalMaterial.EffectOnFullyHeated != null)
                {
                    ironEffect = physicalMaterial.EffectOnFullyHeated;
                    break;
                }
            }
            return ironEffect;
        }

        private static bool Safe(PooledObjectDefinition definition)
        {
            PooledObject prefab = definition.Prefab;
            if (prefab != null && Plat.All<ParticleSystem>(prefab, true).ToArray().Length != 0 && Plat.All<Pickup>(prefab, true).ToArray().Length == 0 && Plat.All<NetworkEntity>(prefab, true).ToArray().Length == 0 && Plat.All<Collider>(prefab, true).ToArray().Length == 0 && Plat.All<Rigidbody>(prefab, true).ToArray().Length == 0)
            {
                return Plat.All<HeatPoint>(prefab, true).ToArray().Length == 0;
            }
            return false;
        }

        private void Release(bool clear)
        {
            PooledObject pooledObject = effect;
            effect = null;
            activeDefinition = null;
            if (pooledObject == null)
            {
                return;
            }
            try
            {
                pooledObject.transform.SetParent(null, worldPositionStays: true);
                if (clear)
                {
                    ParticleSystem[] componentsInChildren = Plat.All<ParticleSystem>(pooledObject, true).ToArray();
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        componentsInChildren[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
                if (!pooledObject.IsReleased)
                {
                    pooledObject.Release();
                }
            }
            catch (Exception ex)
            {
                Warn(Time.unscaledTime, ex.Message);
            }
        }

        private void Warn(float now, string reason)
        {
            if (!(now < warnAfter))
            {
                warnAfter = now + 15f;
                Core.Warn("REPAIR_WORK_HEAT_WAIT reason=" + reason);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Release(clear: true);
            if (anchor != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(anchor.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(anchor.gameObject);
                }
            }
            anchor = null;
        }

        private static bool Finite(float value)
        {
            if (!float.IsNaN(value))
            {
                return !float.IsInfinity(value);
            }
            return false;
        }

        private static bool Finite(Vector3 value)
        {
            if (Finite(value.x) && Finite(value.y))
            {
                return Finite(value.z);
            }
            return false;
        }
    }
}
