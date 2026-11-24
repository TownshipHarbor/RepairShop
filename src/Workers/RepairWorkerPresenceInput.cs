using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal struct RepairWorkerPresenceInput
    {
        internal bool Working;

        internal bool HasLookTarget;

        internal bool TargetEntered;

        internal Vector3 LocalLookDirection;

        internal float TargetDistance;
    }
}
