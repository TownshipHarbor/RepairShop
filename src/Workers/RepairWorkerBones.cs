using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Networking;

namespace RepairShopQuest
{
    internal static class RepairWorkerBones
    {
        internal static string Name(Transform bone)
        {
            if (bone == null)
            {
                return string.Empty;
            }
            NetworkEntity component = Plat.Get<NetworkEntity>(bone);
            string text = ((component != null && !string.IsNullOrEmpty(component.OriginalName)) ? component.OriginalName : bone.name);
            int num = text.IndexOf(" - ", StringComparison.Ordinal);
            if (num >= 0)
            {
                text = text.Substring(num + 3);
            }
            return text.Replace("(Clone)", "").Trim();
        }

        internal static Transform Find(IDictionary<string, Transform> bones, string name)
        {
            if (!bones.TryGetValue(name, out var value))
            {
                return null;
            }
            return value;
        }
    }
}
