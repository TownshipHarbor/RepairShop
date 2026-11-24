using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerEyePose
    {
        private static readonly Quaternion LeftOpen = new Quaternion(0.09199221f, -0.13806877f, 0.8742936f, 0.45616364f);

        private static readonly Quaternion RightOpen = new Quaternion(-0.13806877f, -0.09199221f, -0.45616364f, 0.8742936f);

        private Transform left;

        private Transform right;

        internal void Bind(Transform rigRoot)
        {
            Clear();
            if (rigRoot == null)
            {
                return;
            }
            Transform[] componentsInChildren = Plat.All<Transform>(rigRoot, true).ToArray();
            foreach (Transform bone in componentsInChildren)
            {
                string text = RepairWorkerBones.Name(bone);
                if (text == "eyelidRoot_L_jnt")
                {
                    left = bone;
                }
                else if (text == "eyelidRoot_R_jnt")
                {
                    right = bone;
                }
            }
            Apply();
        }

        internal void Apply()
        {
            if (left != null)
            {
                left.localRotation = LeftOpen;
            }
            if (right != null)
            {
                right.localRotation = RightOpen;
            }
        }

        internal void Clear()
        {
            left = null;
            right = null;
        }
    }
}
