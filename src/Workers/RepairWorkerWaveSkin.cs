using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerWaveSkin
    {
        private sealed class Skin
        {
            internal SkinnedMeshRenderer Renderer;

            internal Mesh Original;

            internal Mesh Instance;

            internal Transform[] Bones;

            internal Matrix4x4 BindToWorld;

            internal BoneWeight[] OriginalWeights;

            internal BoneWeight[] Working;

            internal Sample[] Samples;
        }

        private struct Sample
        {
            internal int Vertex;

            internal float Position;

            internal float Mask;
        }

        private const int FrameCount = 5;

        private readonly List<Skin> skins = new List<Skin>();

        private readonly Transform[] frames = new Transform[5];

        private readonly int[] blendIndices = new int[6];

        private readonly float[] blendWeights = new float[6];

        private Transform shoulder;

        private Transform elbow;

        private Transform wrist;

        private float nativeUpper;

        private float trim;

        private float lastAmount = -1f;

        internal int RendererCount => skins.Count;

        internal bool Bind(Transform rendererRoot, Transform upper, Transform joint, Transform hand)
        {
            Restore();
            if (rendererRoot == null || upper == null || joint == null || hand == null)
            {
                return false;
            }
            float num = Mathf.Abs(joint.lossyScale.x);
            float num2 = Vector3.Distance(upper.position, joint.position);
            float num3 = Vector3.Distance(joint.position, hand.position);
            if (num < 0.0001f || num2 < 0.001f || num3 < 0.001f)
            {
                return false;
            }
            shoulder = upper;
            elbow = joint;
            wrist = hand;
            nativeUpper = num2 / num;
            trim = Mathf.Min(0.055f, Mathf.Min(num2 * 0.22f, num3 * 0.18f) / num);
            Vector3 normalized = ((joint.position - upper.position).normalized + (hand.position - joint.position).normalized).normalized;
            if (normalized.sqrMagnitude < 0.5f)
            {
                Restore();
                return false;
            }
            SkinnedMeshRenderer[] componentsInChildren = Plat.All<SkinnedMeshRenderer>(rendererRoot, true).ToArray();
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
            {
                Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
                Transform[] bones = skinnedMeshRenderer.bones;
                if (sharedMesh == null || !sharedMesh.isReadable || bones == null || sharedMesh.bindposes.Length != bones.Length)
                {
                    continue;
                }
                HashSet<int> hashSet = new HashSet<int>();
                int num4 = -1;
                for (int j = 0; j < bones.Length; j++)
                {
                    if (bones[j] != null)
                    {
                        if (Named(bones[j], "elbow_L_End"))
                        {
                            num4 = j;
                        }
                        if (Named(bones[j], "shoulder_L_End") || Named(bones[j], "elbow_L_End") || Named(bones[j], "forearm_L_End"))
                        {
                            hashSet.Add(j);
                        }
                    }
                }
                if (hashSet.Count == 0 || num4 < 0)
                {
                    continue;
                }
                Matrix4x4 bindToWorld = bones[num4].localToWorldMatrix * sharedMesh.bindposes[num4];
                BoneWeight[] boneWeights = sharedMesh.boneWeights;
                Vector3[] vertices = sharedMesh.vertices;
                if (boneWeights.Length != vertices.Length)
                {
                    continue;
                }
                List<Sample> list = new List<Sample>();
                Dictionary<Vector3Int, Sample> dictionary = new Dictionary<Vector3Int, Sample>();
                for (int k = 0; k < vertices.Length; k++)
                {
                    if (Sum(boneWeights[k], hashSet) <= 0.8f)
                    {
                        continue;
                    }
                    Vector3 vector = bindToWorld.MultiplyPoint3x4(vertices[k]) - joint.position;
                    float num5 = Vector3.Dot(vector, normalized) / num;
                    float num6 = Vector3.ProjectOnPlane(vector, normalized).magnitude / num;
                    if (!(Mathf.Abs(num5) >= 0.06f) && !(num6 >= 0.1f))
                    {
                        Vector3Int key = new Vector3Int(Mathf.RoundToInt(vertices[k].x * 100000f), Mathf.RoundToInt(vertices[k].y * 100000f), Mathf.RoundToInt(vertices[k].z * 100000f));
                        if (!dictionary.TryGetValue(key, out var value))
                        {
                            float num7 = Mathf.Clamp01((0.06f - Mathf.Abs(num5)) / 0.025f);
                            Sample sample = default(Sample);
                            sample.Position = Mathf.Clamp01(0.5f + num5 / (trim * 2f));
                            sample.Mask = num7 * num7 * (3f - 2f * num7);
                            value = sample;
                            dictionary.Add(key, value);
                        }
                        value.Vertex = k;
                        list.Add(value);
                    }
                }
                if (list.Count != 0)
                {
                    skins.Add(new Skin
                    {
                        Renderer = skinnedMeshRenderer,
                        Original = sharedMesh,
                        Bones = bones,
                        BindToWorld = bindToWorld,
                        OriginalWeights = boneWeights,
                        Working = (BoneWeight[])boneWeights.Clone(),
                        Samples = list.ToArray()
                    });
                }
            }
            if (skins.Count == 0)
            {
                Restore();
                return false;
            }
            for (int l = 0; l < 5; l++)
            {
                GameObject gameObject = new GameObject("Repair worker wave elbow support " + l);
                gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                frames[l] = gameObject.transform;
                frames[l].SetParent(rendererRoot, worldPositionStays: false);
            }
            UpdateFrames();
            foreach (Skin skin in skins)
            {
                Transform[] array = new Transform[skin.Bones.Length + 5];
                skin.Bones.CopyTo(array, 0);
                Matrix4x4[] array2 = new Matrix4x4[array.Length];
                skin.Original.bindposes.CopyTo(array2, 0);
                for (int m = 0; m < 5; m++)
                {
                    array[skin.Bones.Length + m] = frames[m];
                    array2[skin.Bones.Length + m] = frames[m].worldToLocalMatrix * skin.BindToWorld;
                }
                skin.Instance = Plat.CopyAsset(skin.Original);
                skin.Instance.name = skin.Original.name + " (wave elbow instance)";
                skin.Instance.hideFlags = HideFlags.DontSave;
                skin.Instance.bindposes = array2;
                Plat.SetBones(skin.Renderer, array);
                skin.Renderer.sharedMesh = skin.Instance;
            }
            lastAmount = 0f;
            return true;
        }

        internal void Apply(float amount)
        {
            if (skins.Count == 0 || shoulder == null || elbow == null || wrist == null)
            {
                return;
            }
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                amount = 0f;
            }
            amount = Mathf.Clamp01(amount);
            UpdateFrames();
            if (amount == lastAmount)
            {
                return;
            }
            foreach (Skin skin in skins)
            {
                if (skin.Renderer == null || skin.Instance == null || skin.Renderer.sharedMesh != skin.Instance)
                {
                    continue;
                }
                if (amount <= 0f)
                {
                    skin.Instance.boneWeights = skin.OriginalWeights;
                    continue;
                }
                Sample[] samples = skin.Samples;
                for (int i = 0; i < samples.Length; i++)
                {
                    Sample sample = samples[i];
                    float num = sample.Position * 4f;
                    int num2 = Mathf.Min(3, Mathf.FloorToInt(num));
                    skin.Working[sample.Vertex] = Blend(skin.OriginalWeights[sample.Vertex], skin.Bones.Length + num2, num - (float)num2, amount * sample.Mask);
                }
                skin.Instance.boneWeights = skin.Working;
            }
            lastAmount = amount;
        }

        private void UpdateFrames()
        {
            float num = Vector3.Distance(shoulder.position, elbow.position) / nativeUpper;
            float num2 = trim * num;
            Vector3 normalized = (elbow.position - shoulder.position).normalized;
            Vector3 normalized2 = (wrist.position - elbow.position).normalized;
            Vector3 vector = Vector3.Cross(normalized, normalized2);
            float num3 = Vector3.Angle(normalized, normalized2);
            Vector3 vector2 = elbow.position - normalized * num2;
            Vector3 b = elbow.position + normalized2 * num2;
            bool flag = num3 > 0.5f && num3 < 170f && vector.sqrMagnitude > 1E-06f;
            Vector3 vector3 = Vector3.zero;
            if (flag)
            {
                vector.Normalize();
                float num4 = num2 / Mathf.Tan(num3 * 0.5f * ((float)Math.PI / 180f));
                vector3 = vector2 + Vector3.Cross(vector, normalized).normalized * num4;
            }
            for (int i = 0; i < 5; i++)
            {
                if (!(frames[i] == null))
                {
                    float num5 = (float)i / 4f;
                    Vector3 position = (flag ? (vector3 + Quaternion.AngleAxis(num3 * num5, vector) * (vector2 - vector3)) : Vector3.Lerp(vector2, b, num5));
                    frames[i].SetPositionAndRotation(position, Quaternion.Slerp(shoulder.rotation, elbow.rotation, num5));
                }
            }
        }

        private BoneWeight Blend(BoneWeight original, int first, float fraction, float amount)
        {
            if (amount <= 0f)
            {
                return original;
            }
            blendIndices[0] = original.boneIndex0;
            blendIndices[1] = original.boneIndex1;
            blendIndices[2] = original.boneIndex2;
            blendIndices[3] = original.boneIndex3;
            blendIndices[4] = first;
            blendIndices[5] = first + 1;
            blendWeights[0] = original.weight0 * (1f - amount);
            blendWeights[1] = original.weight1 * (1f - amount);
            blendWeights[2] = original.weight2 * (1f - amount);
            blendWeights[3] = original.weight3 * (1f - amount);
            blendWeights[4] = (1f - fraction) * amount;
            blendWeights[5] = fraction * amount;
            for (int i = 0; i < 5; i++)
            {
                for (int j = i + 1; j < 6; j++)
                {
                    if (blendWeights[j] > blendWeights[i])
                    {
                        float num = blendWeights[i];
                        blendWeights[i] = blendWeights[j];
                        blendWeights[j] = num;
                        int num2 = blendIndices[i];
                        blendIndices[i] = blendIndices[j];
                        blendIndices[j] = num2;
                    }
                }
            }
            float num3 = blendWeights[0] + blendWeights[1] + blendWeights[2] + blendWeights[3];
            BoneWeight result = default(BoneWeight);
            result.boneIndex0 = blendIndices[0];
            result.boneIndex1 = blendIndices[1];
            result.boneIndex2 = blendIndices[2];
            result.boneIndex3 = blendIndices[3];
            result.weight0 = blendWeights[0] / num3;
            result.weight1 = blendWeights[1] / num3;
            result.weight2 = blendWeights[2] / num3;
            result.weight3 = blendWeights[3] / num3;
            return result;
        }

        internal void Clear()
        {
            Restore();
        }

        internal void Restore()
        {
            foreach (Skin skin in skins)
            {
                if (skin.Renderer != null && skin.Renderer.sharedMesh == skin.Instance)
                {
                    skin.Renderer.sharedMesh = skin.Original;
                    Plat.SetBones(skin.Renderer, skin.Bones);
                }
                DestroyCreated(skin.Instance);
            }
            skins.Clear();
            for (int i = 0; i < 5; i++)
            {
                if (frames[i] != null)
                {
                    DestroyCreated(frames[i].gameObject);
                }
                frames[i] = null;
            }
            shoulder = (elbow = (wrist = null));
            lastAmount = -1f;
        }

        private static void DestroyCreated(UnityEngine.Object created)
        {
            if (!(created == null))
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(created);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }
        }

        private static bool Named(Transform bone, string name)
        {
            return RepairWorkerBones.Name(bone).Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        private static float Sum(BoneWeight weight, HashSet<int> indices)
        {
            return (indices.Contains(weight.boneIndex0) ? weight.weight0 : 0f) + (indices.Contains(weight.boneIndex1) ? weight.weight1 : 0f) + (indices.Contains(weight.boneIndex2) ? weight.weight2 : 0f) + (indices.Contains(weight.boneIndex3) ? weight.weight3 : 0f);
        }
    }
}
