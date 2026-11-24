using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerThumbGrip
    {
        private Transform root;

        private Transform mcp;

        private Transform ip;

        private Transform tip;

        private Transform hand;

        private Quaternion rootRest;

        private Quaternion mcpRest;

        private Quaternion ipRest;

        private Vector3 forward;

        private Vector3 across;

        private Vector3 normal;

        internal void Bind(Transform handBone, Transform thumbRoot, Transform thumbMcp, Transform thumbIp, Transform thumbTip, Vector3 palmForward, Vector3 palmAcross, float normalSign = 1f)
        {
            hand = handBone;
            root = thumbRoot;
            mcp = thumbMcp;
            ip = thumbIp;
            tip = thumbTip;
            if (!(root == null) && !(mcp == null) && !(ip == null) && !(tip == null))
            {
                rootRest = root.localRotation;
                mcpRest = mcp.localRotation;
                ipRest = ip.localRotation;
                forward = palmForward.normalized;
                across = Vector3.ProjectOnPlane(palmAcross, forward).normalized;
                normal = Vector3.Cross(forward, across).normalized * normalSign;
            }
        }

        internal void Apply(Vector3 palmCenter, Vector3 baseDirection, Vector3 contactOffset, float closure)
        {
            if (!(hand == null) && !(root == null) && !(mcp == null) && !(ip == null) && !(tip == null))
            {
                root.localRotation = rootRest;
                mcp.localRotation = mcpRest;
                ip.localRotation = ipRest;
                closure = Mathf.Clamp01(closure);
                if (!(closure <= 0f))
                {
                    Vector3 vector = forward * baseDirection.x + across * baseDirection.y + normal * baseDirection.z;
                    Vector3 toDirection = hand.TransformDirection(vector.normalized);
                    root.rotation = Quaternion.FromToRotation(mcp.position - root.position, toDirection) * root.rotation;
                    Vector3 position = palmCenter + forward * contactOffset.x + across * contactOffset.y + normal * contactOffset.z;
                    Vector3 vector2 = hand.TransformPoint(position);
                    Vector3 normalized = (mcp.position - root.position).normalized;
                    Vector3 normalized2 = (vector2 - mcp.position).normalized;
                    Vector3 vector3 = Vector3.Cross(normalized, normalized2);
                    Vector3 pole = ((vector3.sqrMagnitude > 1E-06f) ? (-Vector3.Cross(vector3.normalized, normalized2).normalized) : hand.TransformDirection(-forward));
                    CloneMotionMath.Reach(mcp, ip, tip, vector2, pole);
                    Quaternion localRotation = root.localRotation;
                    Quaternion localRotation2 = mcp.localRotation;
                    Quaternion localRotation3 = ip.localRotation;
                    root.localRotation = Quaternion.Slerp(rootRest, localRotation, closure);
                    mcp.localRotation = Quaternion.Slerp(mcpRest, localRotation2, closure);
                    ip.localRotation = Quaternion.Slerp(ipRest, localRotation3, closure);
                }
            }
        }
    }
}
