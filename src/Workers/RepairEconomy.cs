using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;

namespace RepairShopQuest
{
    public static class RepairEconomy
    {
        public static readonly RepairMetal[] Metals = new RepairMetal[17]
        {
            M("Gold", -50.0, -50.0),
            M("Copper", -30.0, -30.0),
            M("Iron", 0.0, 0.0),
            M("Red Iron", 50.0, -15.0),
            M("Electrum", 50.0, 50.0),
            M("Palladium", 50.0, 400.0),
            M("Viridium", 75.0, 50.0),
            M("Silver", 100.0, 100.0),
            M("Mythril", 150.0, 200.0),
            M("Valyan", 200.0, 250.0),
            M("Darksteel", 200.0, 250.0),
            M("Jade Steel", 215.0, 337.5),
            M("Crysteel", 230.0, 267.5),
            M("Pink Diamond", 230.0, 302.5),
            M("Ember Steel", 245.0, 267.5),
            M("Light & Dark Steel", 260.0, 320.0),
            M("Death Steel", 290.0, 355.0)
        };

        private static RepairMetal M(string n, double d, double u)
        {
            return new RepairMetal
            {
                name = n,
                damage = d,
                durability = u
            };
        }

        private static string Key(string n)
        {
            return new string((n ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        public static RepairMetal Find(string name)
        {
            string k = Key(name);
            if (k != null)
            {
                switch (k)
                {
                case "whitegold":
                    k = "electrum";
                    break;
                case "carsi":
                    k = "palladium";
                    break;
                case "orchi":
                    k = "viridium";
                    break;
                case "evinon":
                case "evinonsteel":
                    k = "valyan";
                    break;
                case "lightdarksteel":
                case "lightanddarksteel":
                    k = "lightdarksteel";
                    break;
                }
            }
            return Metals.FirstOrDefault((RepairMetal m) => Key(m.name) == k);
        }

        public static ServiceQuote Quote(string name, IEnumerable<RepairPartQuote> source)
        {
            RepairPartQuote[] array = source.ToArray();
            if (array.Length == 0 || array.Length > 128)
            {
                throw new InvalidOperationException("No supported repairable parts.");
            }
            RepairPartQuote[] array2 = array;
            foreach (RepairPartQuote repairPartQuote in array2)
            {
                if (!Finite(repairPartQuote.condition) || repairPartQuote.condition < 0.0 || repairPartQuote.condition > 1.0 || !Finite(repairPartQuote.weight) || repairPartQuote.weight <= 0.0 || !Finite(repairPartQuote.factor) || repairPartQuote.factor <= 0.0 || repairPartQuote.factor > 100.0)
                {
                    throw new InvalidOperationException("Invalid part durability or material.");
                }
            }
            double num = array.Sum((RepairPartQuote v) => v.weight);
            double num2 = array.Sum((RepairPartQuote v) => v.weight * (1.0 - v.condition)) / num;
            if (num2 < 0.0001)
            {
                throw new InvalidOperationException("This item does not need repair.");
            }
            double num3 = array.Sum((RepairPartQuote v) => v.weight * (1.0 - v.condition) * v.factor) / num;
            double num4 = array.Sum((RepairPartQuote v) => v.weight * (1.0 - v.condition) * Math.Sqrt(v.factor)) / num;
            return new ServiceQuote
            {
                item = name,
                parts = array,
                condition = 1.0 - num2,
                coins = Math.Max(5, (int)Math.Ceiling(25.0 * num3 - 1E-08)),
                seconds = Math.Max(30, (int)Math.Ceiling(360.0 * num4 - 1E-08))
            };
        }

        private static bool Finite(double d)
        {
            if (!double.IsNaN(d))
            {
                return !double.IsInfinity(d);
            }
            return false;
        }
    }
}
