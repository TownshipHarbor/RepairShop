using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerWaveArm
    {
        private Transform shoulder;

        private Transform elbow;

        private Transform wrist;

        private Transform forearm;

        private Quaternion elbowRest;

        private Quaternion nativeShoulderRest;

        private Quaternion nativeForearmRest;

        private Quaternion inverseNativeFrame;

        private Vector3 nativeUpper;

        private Vector3 forearmAxis;

        private Vector3 palmNormal;

        private float restBend;

        internal Vector3 HingeAxis { get; private set; }

        internal bool Bind(Transform upper, Transform joint, Transform hand, Quaternion rest, IDictionary<string, Transform> mappedBones = null)
        {
            shoulder = (elbow = (wrist = null));
            forearm = null;
            forearmAxis = (palmNormal = Vector3.zero);
            HingeAxis = Vector3.zero;
            if (upper == null || joint == null || hand == null || joint.parent != upper || hand == joint || !hand.IsChildOf(joint) || !Finite(rest))
            {
                return false;
            }
            Vector3 localPosition = joint.localPosition;
            Vector3 vector = rest * joint.InverseTransformPoint(hand.position);
            if (!Finite(localPosition) || !Finite(vector) || localPosition.sqrMagnitude < 1E-08f || vector.sqrMagnitude < 1E-08f)
            {
                return false;
            }
            localPosition.Normalize();
            vector.Normalize();
            Vector3 vector2 = Vector3.Cross(localPosition, vector);
            if (vector2.sqrMagnitude < 1E-06f)
            {
                vector2 = Vector3.ProjectOnPlane(rest * Vector3.back, localPosition);
            }
            if (vector2.sqrMagnitude < 1E-06f)
            {
                vector2 = Vector3.Cross(localPosition, Vector3.up);
            }
            if (vector2.sqrMagnitude < 1E-06f)
            {
                vector2 = Vector3.Cross(localPosition, Vector3.right);
            }
            if (vector2.sqrMagnitude < 1E-06f)
            {
                return false;
            }
            vector2.Normalize();
            shoulder = upper;
            elbow = joint;
            wrist = hand;
            elbowRest = rest;
            nativeShoulderRest = upper.localRotation;
            nativeUpper = localPosition;
            HingeAxis = vector2;
            restBend = Vector3.SignedAngle(localPosition, vector, vector2);
            inverseNativeFrame = Quaternion.Inverse(Quaternion.LookRotation(localPosition, vector2));
            if (hand.parent != joint && hand.parent != null && hand.parent.parent == joint)
            {
                forearm = hand.parent;
                nativeForearmRest = forearm.localRotation;
                forearmAxis = hand.localPosition.normalized;
                Transform transform = ((mappedBones != null) ? RepairWorkerBones.Find(mappedBones, "middleRoot_L_jnt") : null);
                Transform transform2 = ((mappedBones != null) ? RepairWorkerBones.Find(mappedBones, "indexRoot_L_jnt") : null);
                Transform transform3 = ((mappedBones != null) ? RepairWorkerBones.Find(mappedBones, "littleRoot_L_jnt") : null);
                Transform[] componentsInChildren = Plat.All<Transform>(hand, true).ToArray();
                foreach (Transform transform4 in componentsInChildren)
                {
                    string text = RepairWorkerBones.Name(transform4);
                    if (text.EndsWith("middleRoot_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        transform = transform4;
                    }
                    else if (text.EndsWith("indexRoot_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        transform2 = transform4;
                    }
                    else if (text.EndsWith("littleRoot_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        transform3 = transform4;
                    }
                }
                if (transform != null && transform2 != null && transform3 != null)
                {
                    palmNormal = -hand.InverseTransformDirection(Vector3.Cross(transform.position - hand.position, transform2.position - transform3.position)).normalized;
                }
            }
            return true;
        }

        internal void RestoreForearm()
        {
            if (forearm != null)
            {
                forearm.localRotation = nativeForearmRest;
            }
        }

        internal bool ApplyPalmFacing(Vector3 worldFacing, float amount)
        {
            RestoreForearm();
            if (forearm == null || wrist == null || forearmAxis.sqrMagnitude < 0.5f || palmNormal.sqrMagnitude < 0.5f || !Finite(worldFacing) || !Finite(amount))
            {
                return false;
            }
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f)
            {
                return true;
            }
            Vector3 normalized = forearm.TransformDirection(forearmAxis).normalized;
            Vector3 from = Vector3.ProjectOnPlane(wrist.TransformDirection(palmNormal), normalized);
            Vector3 to = Vector3.ProjectOnPlane(worldFacing, normalized);
            if (from.sqrMagnitude < 1E-06f || to.sqrMagnitude < 1E-06f)
            {
                return false;
            }
            float num = Mathf.Clamp(Vector3.SignedAngle(from, to, normalized), -85f, 85f);
            forearm.localRotation = nativeForearmRest * Quaternion.AngleAxis(num * amount, forearmAxis);
            return true;
        }

        internal bool ApplyGreeting(Vector3 upperDirection, float elbowBend, float axialRoll, float amount)
        {
            if (shoulder == null || elbow == null || wrist == null || !Finite(upperDirection) || !Finite(elbowBend) || !Finite(axialRoll) || !Finite(amount))
            {
                return false;
            }
            float magnitude = upperDirection.magnitude;
            if (magnitude < 0.0001f || !Finite(magnitude))
            {
                return false;
            }
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f)
            {
                return true;
            }
            Vector3 vector = upperDirection / magnitude;
            Quaternion quaternion = ((shoulder.parent != null) ? shoulder.parent.rotation : Quaternion.identity) * nativeShoulderRest;
            Quaternion quaternion2 = Quaternion.FromToRotation(quaternion * nativeUpper, vector) * quaternion;
            quaternion2 = Quaternion.AngleAxis(Mathf.Clamp(axialRoll, -40f, 40f), vector) * quaternion2;
            Quaternion b = Quaternion.AngleAxis(Mathf.Clamp(elbowBend, 90f, 107f) - restBend, HingeAxis) * elbowRest;
            Quaternion rotation = shoulder.rotation;
            Quaternion localRotation = elbow.localRotation;
            shoulder.rotation = Quaternion.Slerp(rotation, quaternion2, amount);
            elbow.localRotation = Quaternion.Slerp(localRotation, b, amount);
            return true;
        }

        private static bool Finite(Vector3 value)
        {
            if (Finite(value.x) && Finite(value.y))
            {
                return Finite(value.z);
            }
            return false;
        }

        private static bool Finite(Quaternion value)
        {
            if (Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w))
            {
                return value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 1E-06f;
            }
            return false;
        }

        private static bool Finite(float value)
        {
            if (!float.IsNaN(value))
            {
                return !float.IsInfinity(value);
            }
            return false;
        }
    }
}
