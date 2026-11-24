using System;
using System.Globalization;
using Il2Cpp;

namespace RepairShopQuest
{
    internal static class RepairWorkProtocol
    {
        internal const string Prefix = "REPAIR_SHOP_WORK_ITEM_V1|";

        internal const string Capability = "work-v1";

        internal const int MaxPayloadLength = 256;

        internal const int MaxSlot = 999;

        internal const float MaxDuration = 86400f;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static RepairWorkDescriptor Clear(uint workerId)
        {
            return new RepairWorkDescriptor
            {
                WorkerId = workerId,
                Slot = -1,
                JobId = ""
            };
        }

        internal static string Encode(RepairWorkDescriptor value)
        {
            if (!IsValid(value))
            {
                throw new InvalidOperationException("Invalid repair work descriptor.");
            }
            return value.WorkerId.ToString(Inv) + "|" + value.ServiceId.ToString(Inv) + "|" + value.Slot.ToString(Inv) + "|" + value.ItemId.ToString(Inv) + "|" + value.JobId + "|" + value.Progress.ToString("R", Inv) + "|" + value.Duration.ToString("R", Inv);
        }

        internal static bool TryParse(string payload, out RepairWorkDescriptor value)
        {
            value = null;
            if (string.IsNullOrEmpty(payload) || payload.Length > 256)
            {
                return false;
            }
            string[] array = payload.Split('|');
            if (array.Length != 7)
            {
                return false;
            }
            RepairWorkDescriptor repairWorkDescriptor = new RepairWorkDescriptor();
            if (!uint.TryParse(array[0], NumberStyles.None, Inv, out repairWorkDescriptor.WorkerId) || !uint.TryParse(array[1], NumberStyles.None, Inv, out repairWorkDescriptor.ServiceId) || !int.TryParse(array[2], NumberStyles.AllowLeadingSign, Inv, out repairWorkDescriptor.Slot) || !uint.TryParse(array[3], NumberStyles.None, Inv, out repairWorkDescriptor.ItemId) || array[5].Trim() != array[5] || array[6].Trim() != array[6] || !float.TryParse(array[5], NumberStyles.Float, Inv, out repairWorkDescriptor.Progress) || !float.TryParse(array[6], NumberStyles.Float, Inv, out repairWorkDescriptor.Duration))
            {
                return false;
            }
            repairWorkDescriptor.JobId = array[4];
            if (!IsValid(repairWorkDescriptor))
            {
                return false;
            }
            value = repairWorkDescriptor;
            return true;
        }

        internal static bool IsValid(RepairWorkDescriptor value)
        {
            if (value == null || value.WorkerId == 0 || !Finite(value.Progress) || !Finite(value.Duration))
            {
                return false;
            }
            if (!value.Active)
            {
                if (value.ServiceId == 0 && value.Slot == -1 && string.IsNullOrEmpty(value.JobId) && value.Progress == 0f)
                {
                    return value.Duration == 0f;
                }
                return false;
            }
            if (value.ServiceId == 0 || value.Slot < 0 || value.Slot > 999 || value.Progress < 0f || value.Progress > 1f || value.Duration <= 0f || value.Duration > 86400f || string.IsNullOrEmpty(value.JobId) || value.JobId.Length > 64)
            {
                return false;
            }
            string jobId = value.JobId;
            foreach (char c in jobId)
            {
                if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '-' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool TryTiming(string state, double start, double ready, double now, out float progress, out float duration)
        {
            progress = (duration = 0f);
            if (state != "paid" || !Finite(start) || !Finite(ready) || !Finite(now) || start < 0.0 || ready <= start || ready - start > 86400.0 || now < start || now >= ready)
            {
                return false;
            }
            duration = (float)(ready - start);
            progress = (float)((now - start) / (ready - start));
            if (Finite(duration) && duration > 0f && Finite(progress) && progress >= 0f)
            {
                return progress <= 1f;
            }
            return false;
        }

        private static bool Finite(float value)
        {
            if (!float.IsNaN(value))
            {
                return !float.IsInfinity(value);
            }
            return false;
        }

        private static bool Finite(double value)
        {
            if (!double.IsNaN(value))
            {
                return !double.IsInfinity(value);
            }
            return false;
        }
    }
}
