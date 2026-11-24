using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    public static class CloneMotionMath
    {
        public static void Pose(Transform bone, Quaternion rest, Transform body, float pitch, float yaw, float roll)
        {
            if (!(bone == null))
            {
                bone.rotation = Quaternion.AngleAxis(yaw, body.up) * Quaternion.AngleAxis(pitch, body.right) * Quaternion.AngleAxis(roll, body.forward) * (bone.parent.rotation * rest);
            }
        }

        public static void Reach(Transform shoulder, Transform elbow, Transform wrist, Vector3 target, Vector3 pole)
        {
            if (shoulder == null || elbow == null || wrist == null)
            {
                return;
            }
            Vector3 position = shoulder.position;
            float num = Vector3.Distance(position, elbow.position);
            float num2 = Vector3.Distance(elbow.position, wrist.position);
            if (num < 0.001f || num2 < 0.001f)
            {
                return;
            }
            Vector3 vector = target - position;
            if (!(vector.sqrMagnitude < 1E-06f))
            {
                Vector3 normalized = vector.normalized;
                float num3 = Mathf.Clamp(vector.magnitude, Mathf.Abs(num - num2) + 0.001f, num + num2 - 0.001f);
                float num4 = (num * num - num2 * num2 + num3 * num3) / (2f * num3);
                float num5 = Mathf.Sqrt(Mathf.Max(0f, num * num - num4 * num4));
                Vector3 normalized2 = Vector3.ProjectOnPlane(pole, normalized).normalized;
                if (normalized2.sqrMagnitude < 0.001f)
                {
                    normalized2 = Vector3.Cross(normalized, Vector3.right).normalized;
                }
                Vector3 vector2 = position + normalized * num4 + normalized2 * num5;
                shoulder.rotation = Quaternion.FromToRotation(elbow.position - position, vector2 - position) * shoulder.rotation;
                elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, position + normalized * num3 - elbow.position) * elbow.rotation;
            }
        }

        public static Quaternion Aim(Quaternion rest, Vector3 childOffset, Vector3 parentSpaceDirection)
        {
            if (childOffset.sqrMagnitude < 1E-06f || parentSpaceDirection.sqrMagnitude < 1E-06f)
            {
                return rest;
            }
            return Quaternion.FromToRotation(rest * childOffset, parentSpaceDirection) * rest;
        }
    }
}
