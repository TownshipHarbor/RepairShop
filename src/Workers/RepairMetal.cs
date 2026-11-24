using Il2Cpp;

namespace RepairShopQuest
{
    public sealed class RepairMetal
    {
        public string name;

        public double damage;

        public double durability;

        public double Factor => (1.0 + damage / 100.0 + (1.0 + durability / 100.0)) / 2.0;
    }
}
