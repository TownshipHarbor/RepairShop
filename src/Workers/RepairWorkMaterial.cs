using System;
using System.Linq;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal static class RepairWorkMaterial
    {
        private static readonly uint[] WoodHashes = new uint[5] { 19966u, 37260u, 24722u, 10962u, 15500u };

        private static readonly string[] Woods = new string[5] { "ash", "birch", "oak", "redwood", "walnut" };

        private static string Key(string value)
        {
            return new string((value ?? "").Replace("(Clone)", "").Replace("(Instance)", "").ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        internal static bool IsWoodName(string name)
        {
            return Woods.Contains(Key(name));
        }

        internal static bool IsWoodIdentity(uint material, string name)
        {
            if (!WoodHashes.Contains(material))
            {
                return IsWoodName(name);
            }
            return true;
        }

        internal static bool IsWood(Pickup part)
        {
            PhysicalMaterial physicalMaterial = ((part == null || part.PhysicalMaterial == null) ? null : part.PhysicalMaterial.PhysicalMaterial);
            if (physicalMaterial != null)
            {
                return IsWoodIdentity(physicalMaterial.Hash, physicalMaterial.name);
            }
            return false;
        }

        internal static bool IsMetal(Pickup part)
        {
            if (part == null || IsWood(part))
            {
                return false;
            }
            PhysicalMaterial physicalMaterial = ((part.PhysicalMaterial == null) ? null : part.PhysicalMaterial.PhysicalMaterial);
            if (physicalMaterial != null)
            {
                if (RepairEconomy.Find(physicalMaterial.name) != null)
                {
                    return true;
                }
                string @object = Key(physicalMaterial.name);
                if (new string[6] { "leather", "cloth", "rope", "straw", "bone", "fiber" }.Any(@object.Contains))
                {
                    return false;
                }
                try
                {
                    Material material = physicalMaterial.GetMaterial(PhysicalMaterialChannel.A);
                    if (material != null && material.shader != null && material.shader.name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }
            Renderer[] componentsInChildren = Plat.All<Renderer>(part, true).ToArray();
            foreach (Renderer renderer in componentsInChildren)
            {
                if (!(Plat.InParent<Pickup>(renderer) != part) && renderer.sharedMaterials.Any((Material m) => m != null && m.shader != null && (m.shader.name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 || (m.HasProperty("_UseMetalMask") && m.GetFloat("_UseMetalMask") > 0.5f))))
                {
                    return true;
                }
            }
            return physicalMaterial == null;
        }

        internal static RepairWorkMode Choose(Pickup source, RepairPartQuote[] saved, float progress)
        {
            if (!(source == null))
            {
                return Choose(saved, progress);
            }
            return RepairWorkMode.Metal;
        }

        internal static RepairWorkMode Choose(RepairPartQuote[] saved, float progress)
        {
            if (saved == null || saved.Length == 0 || saved.Length > 128 || float.IsNaN(progress) || float.IsInfinity(progress))
            {
                return RepairWorkMode.Metal;
            }
            double num = 0.0;
            double num2 = 0.0;
            foreach (RepairPartQuote repairPartQuote in saved)
            {
                if (repairPartQuote == null || double.IsNaN(repairPartQuote.condition) || double.IsInfinity(repairPartQuote.condition) || repairPartQuote.condition < 0.0 || repairPartQuote.condition > 1.0 || double.IsNaN(repairPartQuote.weight) || double.IsInfinity(repairPartQuote.weight) || repairPartQuote.weight <= 0.0)
                {
                    return RepairWorkMode.Metal;
                }
                double num3 = repairPartQuote.weight * (1.0 - repairPartQuote.condition);
                if (!(num3 <= 1E-06))
                {
                    if (IsWoodIdentity(repairPartQuote.material, repairPartQuote.metal))
                    {
                        num += num3;
                    }
                    else
                    {
                        num2 += num3;
                    }
                }
            }
            if (num <= 0.0)
            {
                return RepairWorkMode.Metal;
            }
            if (num2 <= 0.0)
            {
                return RepairWorkMode.Wood;
            }
            double num4 = Math.Max(0.25, Math.Min(0.75, num2 / (num2 + num)));
            if (!((double)progress < num4))
            {
                return RepairWorkMode.Wood;
            }
            return RepairWorkMode.Metal;
        }

        internal static RepairWorkStage Stage(RepairPartQuote[] saved, float progress)
        {
            return Stage(saved, progress, 0f);
        }

        internal static RepairWorkStage Stage(RepairPartQuote[] saved, float progress, float duration)
        {
            if (saved == null || saved.Length == 0 || saved.Length > 128 || float.IsNaN(progress) || float.IsInfinity(progress))
            {
                return RepairWorkStage.Metal;
            }
            double num = 0.0;
            double num2 = 0.0;
            foreach (RepairPartQuote repairPartQuote in saved)
            {
                if (repairPartQuote == null || double.IsNaN(repairPartQuote.condition) || double.IsInfinity(repairPartQuote.condition) || repairPartQuote.condition < 0.0 || repairPartQuote.condition > 1.0 || double.IsNaN(repairPartQuote.weight) || double.IsInfinity(repairPartQuote.weight) || repairPartQuote.weight <= 0.0)
                {
                    return RepairWorkStage.Metal;
                }
                double num3 = repairPartQuote.weight * (1.0 - repairPartQuote.condition);
                if (!(num3 <= 1E-06))
                {
                    if (IsWoodIdentity(repairPartQuote.material, repairPartQuote.metal))
                    {
                        num += num3;
                    }
                    else
                    {
                        num2 += num3;
                    }
                }
            }
            if (num2 <= 0.0)
            {
                if (!(num > 0.0))
                {
                    return RepairWorkStage.Metal;
                }
                return RepairWorkStage.Wood;
            }
            double num4 = ((num <= 0.0) ? 1.0 : Math.Max(0.25, Math.Min(0.75, num2 / (num2 + num))));
            if (num > 0.0 && !float.IsNaN(duration) && !float.IsInfinity(duration) && duration > 0f)
            {
                num4 = Math.Max(num4, Math.Min(0.55, 15.0 / (double)duration));
            }
            if ((double)progress >= num4 && num > 0.0)
            {
                return RepairWorkStage.Wood;
            }
            double num5 = num4 * 0.25;
            if (!float.IsNaN(duration) && !float.IsInfinity(duration) && duration > 0f)
            {
                num5 = Math.Min(num4 * 0.65, Math.Max(num5, 7.0 / (double)duration));
            }
            if (!((double)progress < num5))
            {
                return RepairWorkStage.Metal;
            }
            return RepairWorkStage.Heating;
        }
    }
}
