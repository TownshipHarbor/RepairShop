using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerHand
    {
        private struct Joint
        {
            internal Transform Bone;

            internal Quaternion Rest;

            internal Quaternion ClosedRoot;

            internal Vector3 Axis;

            internal float Relax;

            internal float Curl;

            internal int Part;

            internal int Digit;
        }

        private readonly List<Joint> joints = new List<Joint>();

        private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();

        private readonly Dictionary<Transform, Transform> followers = new Dictionary<Transform, Transform>();

        private readonly Dictionary<Transform, Quaternion> followerRest = new Dictionary<Transform, Quaternion>();

        private readonly RepairWorkerThumbGrip thumb = new RepairWorkerThumbGrip();

        private Transform hand;

        private bool right;

        internal Vector3 PalmOffset { get; private set; }

        internal Quaternion PalmFrame { get; private set; }

        internal Vector3 PalmPosition
        {
            get
            {
                if (!(hand != null))
                {
                    return Vector3.zero;
                }
                return hand.TransformPoint(PalmOffset);
            }
        }

        internal Quaternion PalmRotation
        {
            get
            {
                if (!(hand != null))
                {
                    return Quaternion.identity;
                }
                return hand.rotation * PalmFrame;
            }
        }

        internal void Bind(Transform wrist, IDictionary<string, Transform> bones, Transform renderers, bool rightHand)
        {
            Restore();
            joints.Clear();
            rest.Clear();
            followers.Clear();
            followerRest.Clear();
            hand = wrist;
            right = rightHand;
            PalmOffset = Vector3.zero;
            PalmFrame = Quaternion.identity;
            if (hand == null)
            {
                return;
            }
            string text = (right ? "R" : "L");
            Transform transform = RepairWorkerBones.Find(bones, "middleRoot_" + text + "_jnt");
            Transform transform2 = RepairWorkerBones.Find(bones, "indexRoot_" + text + "_jnt");
            Transform transform3 = RepairWorkerBones.Find(bones, "littleRoot_" + text + "_jnt");
            if (transform == null || transform2 == null || transform3 == null)
            {
                return;
            }
            Vector3 vector = hand.InverseTransformPoint(transform.position);
            Vector3 vector2 = hand.InverseTransformDirection(transform2.position - transform3.position);
            Vector3 vector3 = Vector3.Cross(vector, vector2).normalized * (right ? 1f : (-1f));
            PalmOffset = vector * 0.9f + vector3 * 0.055f + (right ? new Vector3(-0.015f, 0f, 0.015f) : new Vector3(0.015f, 0f, -0.015f));
            PalmFrame = Quaternion.LookRotation(vector.normalized, vector3);
            string[] array = new string[4] { "index", "middle", "ring", "little" };
            Vector3[] array2 = new Vector3[4]
            {
                new Vector3(17f, 91f, 47.5f),
                new Vector3(30f, 83f, 65f),
                new Vector3(25f, 78f, 60f),
                new Vector3(12f, 53f, 40f)
            };
            float[] array3 = new float[3] { 7f, 14f, 9f };
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    string[] obj = new string[5]
                    {
                        array[i],
                        null,
                        null,
                        null,
                        null
                    };
                    object obj2;
                    switch (j)
                    {
                    default:
                        obj2 = "_02";
                        break;
                    case 1:
                        obj2 = "_01";
                        break;
                    case 0:
                        obj2 = "Root";
                        break;
                    }
                    obj[1] = (string)obj2;
                    obj[2] = "_";
                    obj[3] = text;
                    obj[4] = "_jnt";
                    string name = string.Concat(obj);
                    Transform transform4 = RepairWorkerBones.Find(bones, name);
                    if (!(transform4 == null) && transform4.childCount != 0 && !(transform4.parent == null))
                    {
                        Quaternion localRotation = transform4.localRotation;
                        Quaternion quaternion = localRotation;
                        Vector3 vector4 = hand.TransformDirection(vector2.normalized);
                        Vector3 normalized = transform4.InverseTransformDirection(vector4).normalized;
                        Vector3 position = transform4.GetChild(0).position;
                        transform4.localRotation = localRotation * Quaternion.AngleAxis(1f, normalized);
                        if (Vector3.Dot(transform4.GetChild(0).position - position, hand.TransformDirection(vector3)) < 0f)
                        {
                            vector4 = -vector4;
                        }
                        transform4.localRotation = localRotation;
                        normalized = transform4.InverseTransformDirection(vector4).normalized;
                        if (j == 0)
                        {
                            Vector3 fromDirection = localRotation * transform4.GetChild(0).localPosition;
                            Vector3 toDirection = transform4.parent.InverseTransformDirection(hand.TransformDirection(vector.normalized));
                            quaternion = Quaternion.FromToRotation(fromDirection, toDirection) * localRotation;
                            normalized = Quaternion.Inverse(quaternion) * transform4.parent.InverseTransformDirection(vector4);
                        }
                        joints.Add(new Joint
                        {
                            Bone = transform4,
                            Rest = localRotation,
                            ClosedRoot = quaternion,
                            Axis = normalized,
                            Part = j,
                            Digit = i,
                            Relax = array3[j] * (1f + (float)i * 0.12f),
                            Curl = array2[i][j]
                        });
                        rest[transform4] = localRotation;
                    }
                }
            }
            foreach (KeyValuePair<string, Transform> bone in bones)
            {
                if (bone.Value != null && bone.Key.EndsWith("_" + text + "_jnt", StringComparison.OrdinalIgnoreCase) && (bone.Key.StartsWith("thumb", StringComparison.OrdinalIgnoreCase) || bone.Key.StartsWith("index", StringComparison.OrdinalIgnoreCase) || bone.Key.StartsWith("middle", StringComparison.OrdinalIgnoreCase) || bone.Key.StartsWith("ring", StringComparison.OrdinalIgnoreCase) || bone.Key.StartsWith("little", StringComparison.OrdinalIgnoreCase)))
                {
                    rest[bone.Value] = bone.Value.localRotation;
                }
            }
            thumb.Bind(hand, RepairWorkerBones.Find(bones, "thumbRoot_" + text + "_jnt"), RepairWorkerBones.Find(bones, "thumb_01_" + text + "_jnt"), RepairWorkerBones.Find(bones, "thumb_02_" + text + "_jnt"), RepairWorkerBones.Find(bones, "thumbEnd_" + text + "_jnt"), vector, vector2, right ? 1f : (-1f));
            if (renderers == null)
            {
                return;
            }
            Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<Transform, Quaternion> item in rest)
            {
                dictionary[RepairWorkerBones.Name(item.Key)] = item.Key;
            }
            SkinnedMeshRenderer[] componentsInChildren = Plat.All<SkinnedMeshRenderer>(renderers, true).ToArray();
            for (int k = 0; k < componentsInChildren.Length; k++)
            {
                Transform[] bones2 = componentsInChildren[k].bones;
                foreach (Transform transform5 in bones2)
                {
                    if (!(transform5 == null) && !rest.ContainsKey(transform5) && !followers.ContainsKey(transform5) && dictionary.TryGetValue(RepairWorkerBones.Name(transform5), out var value))
                    {
                        followers[transform5] = value;
                        followerRest[transform5] = transform5.localRotation;
                    }
                }
            }
        }

        internal void Apply(float relaxed, float grip)
        {
            Restore();
            relaxed = Mathf.Clamp01(relaxed);
            grip = Mathf.Clamp01(grip);
            foreach (Joint joint in joints)
            {
                if (!(joint.Bone == null))
                {
                    float num = 1f - Mathf.Pow(1f - grip, 1f + (float)joint.Digit * 0.04f);
                    float num2 = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Pow(1f - num, 1.6f));
                    float t = ((joint.Part == 0) ? num2 : ((joint.Part == 1) ? Mathf.SmoothStep(0f, 1f, num) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.1f) / 0.9f))));
                    Quaternion quaternion = ((joint.Part == 0) ? Quaternion.Slerp(joint.Rest, joint.ClosedRoot, num2) : joint.Rest);
                    float angle = Mathf.Lerp(joint.Relax * relaxed, joint.Curl, t);
                    joint.Bone.localRotation = quaternion * Quaternion.AngleAxis(angle, joint.Axis);
                }
            }
            if (grip > 0f)
            {
                thumb.Apply(PalmOffset, new Vector3(0.3f, 0.8f, 0.6f), new Vector3(-0.02f, 0.04f, 0.018f), grip);
            }
            foreach (KeyValuePair<Transform, Transform> follower in followers)
            {
                if (follower.Key != null && follower.Value != null)
                {
                    follower.Key.localRotation = followerRest[follower.Key] * Quaternion.Inverse(rest[follower.Value]) * follower.Value.localRotation;
                }
            }
        }

        internal void Restore()
        {
            foreach (KeyValuePair<Transform, Quaternion> item in rest)
            {
                if (item.Key != null)
                {
                    item.Key.localRotation = item.Value;
                }
            }
            foreach (KeyValuePair<Transform, Quaternion> item2 in followerRest)
            {
                if (item2.Key != null)
                {
                    item2.Key.localRotation = item2.Value;
                }
            }
        }
    }
}
