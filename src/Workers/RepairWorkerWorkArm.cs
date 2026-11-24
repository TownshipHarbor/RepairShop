using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerWorkArm
    {
        private Transform shoulder;

        private Transform elbow;

        private Transform wrist;

        private Transform forearm;

        private Quaternion shoulderRest;

        private Quaternion elbowRest;

        private Quaternion wristRest;

        private Quaternion forearmRest;

        private Vector3 nativeUpper;

        private Vector3 hinge;

        private Vector3 forearmAxis;

        private float restBend;

        internal void Bind(Transform upper, Transform joint, Transform hand, Quaternion upperRest, Quaternion lowerRest, Quaternion handRest)
        {
            shoulder = upper;
            elbow = joint;
            wrist = hand;
            shoulderRest = upperRest;
            elbowRest = lowerRest;
            wristRest = handRest;
            forearm = null;
            forearmAxis = Vector3.zero;
            hinge = Vector3.zero;
            if (!(upper == null) && !(joint == null) && !(hand == null) && !(joint.parent != upper))
            {
                nativeUpper = joint.localPosition.normalized;
                Vector3 vector = lowerRest * joint.InverseTransformPoint(hand.position).normalized;
                hinge = Vector3.Cross(nativeUpper, vector);
                if (hinge.sqrMagnitude < 1E-06f)
                {
                    hinge = Vector3.ProjectOnPlane(lowerRest * Vector3.back, nativeUpper);
                }
                hinge.Normalize();
                restBend = Vector3.SignedAngle(nativeUpper, vector, hinge);
                if (hand.parent != null && hand.parent != joint && hand.parent.parent == joint)
                {
                    forearm = hand.parent;
                    forearmRest = forearm.localRotation;
                    forearmAxis = hand.localPosition.normalized;
                }
            }
        }

        internal void RestoreForearm()
        {
            if (forearm != null)
            {
                forearm.localRotation = forearmRest;
            }
        }

        internal void Reach(Vector3 target, Vector3 pole, float amount)
        {
            if (shoulder == null || elbow == null || wrist == null || hinge.sqrMagnitude < 0.5f || amount <= 0f)
            {
                return;
            }
            Vector3 position = shoulder.position;
            Vector3 vector = target - position;
            float num = Vector3.Distance(position, elbow.position);
            float num2 = Vector3.Distance(elbow.position, wrist.position);
            if (!(num < 0.001f) && !(num2 < 0.001f) && !(vector.sqrMagnitude < 1E-06f))
            {
                Vector3 normalized = vector.normalized;
                float min = Mathf.Sqrt(num * num + num2 * num2 + 2f * num * num2 * Mathf.Cos(1.8325957f));
                float max = Mathf.Sqrt(num * num + num2 * num2 + 2f * num * num2 * Mathf.Cos(0.38397244f));
                float num3 = Mathf.Clamp(vector.magnitude, min, max);
                Vector3 normalized2 = Vector3.ProjectOnPlane(pole, normalized).normalized;
                if (!(normalized2.sqrMagnitude < 0.5f))
                {
                    float num4 = (num * num - num2 * num2 + num3 * num3) / (2f * num3);
                    float num5 = Mathf.Sqrt(Mathf.Max(0f, num * num - num4 * num4));
                    Vector3 vector2 = position + normalized * num4 + normalized2 * num5;
                    Vector3 normalized3 = (vector2 - position).normalized;
                    Vector3 normalized4 = (position + normalized * num3 - vector2).normalized;
                    Vector3 normalized5 = Vector3.Cross(normalized3, normalized4).normalized;
                    Quaternion quaternion = ((shoulder.parent != null) ? shoulder.parent.rotation : Quaternion.identity) * shoulderRest;
                    Quaternion quaternion2 = Quaternion.FromToRotation(quaternion * nativeUpper, normalized3) * quaternion;
                    Quaternion b = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(quaternion2 * hinge, normalized5, normalized3), -40f, 40f), normalized3) * quaternion2;
                    Quaternion b2 = Quaternion.AngleAxis(Vector3.Angle(normalized3, normalized4) - restBend, hinge) * elbowRest;
                    shoulder.rotation = Quaternion.Slerp(shoulder.rotation, b, Mathf.Clamp01(amount));
                    elbow.localRotation = Quaternion.Slerp(elbow.localRotation, b2, Mathf.Clamp01(amount));
                }
            }
        }

        internal void AimTool(Quaternion desiredWrist, float amount)
        {
            if (wrist == null || amount <= 0f)
            {
                return;
            }
            amount = Mathf.Clamp01(amount);
            if (forearm != null && forearmAxis.sqrMagnitude > 0.5f)
            {
                Quaternion quaternion = ((forearm.parent != null) ? forearm.parent.rotation : Quaternion.identity) * forearmRest;
                Vector3 normalized = (quaternion * forearmAxis).normalized;
                Quaternion quaternion2 = desiredWrist * Quaternion.Inverse(quaternion * wristRest);
                Vector3 vector = Vector3.Project(new Vector3(quaternion2.x, quaternion2.y, quaternion2.z), normalized);
                float num = Mathf.Sqrt(vector.sqrMagnitude + quaternion2.w * quaternion2.w);
                if (num > 1E-05f)
                {
                    float target = 2f * Mathf.Atan2(Vector3.Dot(vector / num, normalized), quaternion2.w / num) * 57.29578f;
                    target = Mathf.DeltaAngle(0f, target);
                    forearm.localRotation = forearmRest * Quaternion.AngleAxis(Mathf.Clamp(target, -75f, 75f) * amount, forearmAxis);
                }
            }
            Quaternion b = Quaternion.RotateTowards((wrist.parent != null) ? (wrist.parent.rotation * wristRest) : wristRest, desiredWrist, 38f);
            wrist.rotation = Quaternion.Slerp(wrist.rotation, b, amount);
        }
    }
}
