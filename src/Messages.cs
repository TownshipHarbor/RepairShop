using System;

namespace RepairShopQuest
{
    internal static class Messages
    {
        internal const string Ready = "REPAIR_SHOP_READY_V1|";
        internal const string Clone = "REPAIR_SHOP_CLONE_V1|";
        internal static readonly string[] Quiet = { "REPAIR_SHOP_WORKER_V2|", "REPAIR_SHOP_WORK_ITEM_V1|", "REPAIR_SHOP_STATION_V1|", "REPAIR_SHOP_SEQUENCE_V1|" };
        internal static int Hidden;

        internal static bool Show(string message)
        {
            if (message == null || !message.StartsWith("REPAIR_SHOP_", StringComparison.Ordinal)) return true;
            if (message.StartsWith(Ready, StringComparison.Ordinal))
            {
                Hidden++;
                Handshake.Confirm(message.Substring(Ready.Length));
                return false;
            }
            if (message.StartsWith(Clone, StringComparison.Ordinal))
            {
                Hidden++;
                if (Handshake.Available) Look.Mark(message.Substring(Clone.Length));
                return false;
            }
            for (int i = 0; i < Quiet.Length; i++)
            {
                string q = Quiet[i];
                if (!message.StartsWith(q, StringComparison.Ordinal)) continue;
                Hidden++;
                if (!Handshake.Available) return false;
                string body = message.Substring(q.Length);
                if (i == 0) WorkerView.Mark(body);
                else if (i == 1) WorkerView.MarkWork(body);
                else if (i == 2) WorkerView.MarkStation(body);
                else WorkerView.MarkSequence(body);
                return false;
            }
            return true;
        }
    }
}
