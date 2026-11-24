using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Character;
using Il2CppAlta.Customization;

namespace RepairShopQuest
{
    internal static class RepairWorkerBodyPose
    {
        private static readonly string[] States = new string[8] { "F_small", "M_small", "F_medium", "M_medium", "F_buff", "M_buff", "F_large", "M_large" };

        internal static int Requested(CharacterDummy dummy)
        {
            if (dummy == null)
            {
                return -1;
            }
            if (dummy.CosmeticPacker != null)
            {
                return dummy.CosmeticPacker.BodyTypeIndex;
            }
            Animator animator = dummy.Animator;
            if (!(animator == null) && !(animator.runtimeAnimatorController == null))
            {
                return animator.GetInteger(CosmeticBodyType.BodyType);
            }
            return -1;
        }

        internal static bool TryApply(Animator animator, int index)
        {
            if (animator == null)
            {
                return index == -1;
            }
            if (animator.runtimeAnimatorController == null || index < 0 || index >= States.Length)
            {
                return false;
            }
            int num = Animator.StringToHash(States[index]);
            bool enabled = animator.enabled;
            AnimatorCullingMode cullingMode = animator.cullingMode;
            float[] array = null;
            try
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                if (!animator.isInitialized)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
                if (animator.layerCount == 0)
                {
                    return false;
                }
                int stateID = Animator.StringToHash(animator.GetLayerName(0) + "." + States[index]);
                if (!animator.HasState(0, num) && !animator.HasState(0, stateID))
                {
                    return false;
                }
                array = new float[animator.layerCount - 1];
                for (int i = 1; i < animator.layerCount; i++)
                {
                    array[i - 1] = animator.GetLayerWeight(i);
                    animator.SetLayerWeight(i, 0f);
                }
                AnimatorControllerParameter[] parameters = animator.parameters;
                foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
                {
                    if (animatorControllerParameter.name == "LeftEyeBlink" || animatorControllerParameter.name == "RightEyeBlink")
                    {
                        animator.ResetTrigger(animatorControllerParameter.nameHash);
                    }
                }
                CosmeticBodyType.ApplyToAnimator(animator, index);
                return Geo.StateHash(animator.GetCurrentAnimatorStateInfo(0)) == num;
            }
            finally
            {
                if (array != null)
                {
                    for (int k = 1; k <= array.Length; k++)
                    {
                        animator.SetLayerWeight(k, array[k - 1]);
                    }
                }
                animator.enabled = enabled;
                animator.cullingMode = cullingMode;
            }
        }
    }
}
