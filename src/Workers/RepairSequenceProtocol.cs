using System;
using System.Globalization;
using Il2Cpp;

namespace RepairShopQuest
{
    internal static class RepairSequenceProtocol
    {
        internal const string Prefix = "REPAIR_SHOP_SEQUENCE_V1|";

        internal const string Capability = "sequence-v1";

        internal static string Encode(uint worker, RepairSequenceStage stage)
        {
            if (worker == 0 || stage < RepairSequenceStage.Idle || stage > RepairSequenceStage.Moving)
            {
                throw new InvalidOperationException("Invalid repair sequence state.");
            }
            string text = worker.ToString(CultureInfo.InvariantCulture);
            int num = (int)stage;
            return text + "|" + num.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParse(string text, out uint worker, out RepairSequenceStage stage)
        {
            worker = 0u;
            stage = RepairSequenceStage.Idle;
            if (string.IsNullOrEmpty(text) || text.Length > 24)
            {
                return false;
            }
            string[] array = text.Split('|');
            if (array.Length != 2 || !uint.TryParse(array[0], NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result == 0 || !int.TryParse(array[1], NumberStyles.None, CultureInfo.InvariantCulture, out var result2) || result2 < 0 || result2 > 4)
            {
                return false;
            }
            worker = result;
            stage = (RepairSequenceStage)result2;
            return true;
        }
    }
}
