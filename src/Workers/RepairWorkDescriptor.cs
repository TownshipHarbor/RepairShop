using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkDescriptor
    {
        internal uint WorkerId;

        internal uint ServiceId;

        internal uint ItemId;

        internal int Slot;

        internal string JobId;

        internal float Progress;

        internal float Duration;

        internal bool Active => ItemId != 0;
    }
}
