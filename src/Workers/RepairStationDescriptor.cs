using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairStationDescriptor
    {
        internal uint WorkerId;

        internal RepairStationMode Mode;

        internal Vector3 WorkTarget;

        internal Vector3 FromPosition;

        internal Vector3 ToPosition;

        internal Quaternion FromRotation;

        internal Quaternion ToRotation;

        internal float Elapsed;

        internal float Duration;
    }
}
