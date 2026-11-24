using System;
using System.Text;
using UnityEngine;
using Il2Cpp;
using Object = UnityEngine.Object;

namespace RepairShopQuest
{
    internal static class RepairWorkerProps
    {
        internal static GameObject HammerModel;

        internal static RepairHammerGripData HammerGrip;

        static bool started;

        static GameObject holder;

        internal static void Load()
        {
            if (started) return;
            started = true;
            try
            {
                Pack.Read();
                holder = new GameObject("RepairShop worker props");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
                HammerModel = Pack.Build("RepairWorkerHammer", holder.transform);
                byte[] blob = Pack.Blob("RepairWorkerHammerGrip");
                if (blob == null) throw new InvalidOperationException("the hammer grip is missing from the pack");
                HammerGrip = RepairHammerGripData.Parse(Encoding.UTF8.GetString(blob));
            }
            catch (Exception ex)
            {
                Core.Warn("Repair worker hammer: " + ex.Message);
                HammerModel = null;
                HammerGrip = null;
                return;
            }
            if (HammerGrip.grips == null || HammerGrip.grips.Length == 0 || float.IsNaN(HammerGrip.scale) || float.IsInfinity(HammerGrip.scale) || HammerGrip.scale <= 0f || HammerGrip.axis.sqrMagnitude < 0.0001f || Vector3.Cross(HammerGrip.axis, HammerGrip.strikeAxis).sqrMagnitude < 0.0001f)
            {
                HammerModel = null;
                HammerGrip = null;
                Core.Warn("Repair worker hammer model or native grip missing/invalid");
                return;
            }
            Core.Log("REPAIR_WORKER_HAMMER_READY grips=" + HammerGrip.grips.Length);
        }

        internal static Transform CreateHammer(Transform workerRoot)
        {
            if (HammerModel == null || HammerGrip == null || workerRoot == null) return null;
            GameObject copy = Plat.Copy(HammerModel, workerRoot);
            copy.name = "Repair worker hammer (visual only)";
            return copy.transform;
        }

        internal static void DestroyHammer(Transform model)
        {
            if (model != null) Object.Destroy(model.gameObject);
        }
    }
}
