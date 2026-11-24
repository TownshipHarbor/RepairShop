using System;
using UnityEngine;
using Il2Cpp;
using Il2CppATT.Character.QuickAccessMenu;
using Il2CppAlta.Networking;
using Il2CppAlta.Networking.Scripts.Player;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace RepairShopQuest
{
    internal static class Handshake
    {
        static long _server, _player;
        static string _nonce;
        static float _next, _delay = 1f;
        static bool _acknowledged;
        internal static int Sent;
        static Il2CppReferenceArray<IPlayer> _none;

        internal static bool Available
        {
            get { return _acknowledged && _server != 0 && _server == ServerId() && _player == Plat.Id(Player.Current); }
        }

        internal static void Clear()
        {
            _server = 0;
            _player = 0;
            _nonce = null;
            _next = 0f;
            _delay = 1f;
            _acknowledged = false;
        }

        static long ServerId()
        {
            return Plat.Id(JoinedServerInfo.Current);
        }

        internal static void Tick()
        {
            long server = ServerId();
            long player = Plat.Id(Player.Current);
            if (server != _server || player != _player)
            {
                Clear();
                _server = server;
                _player = player;
            }
            if (!Shop.Done || server == 0 || player == 0 || Time.unscaledTime < _next) return;
            if (_nonce == null) _nonce = Guid.NewGuid().ToString("N");
            bool sent = Send("repairshop ping " + _nonce);
            if (sent)
            {
                Sent++;
                Send("repairshop ping " + _nonce + " work-v1");
                Send("repairshop ping " + _nonce + " stations-v1");
                Send("repairshop ping " + _nonce + " sequence-v1");
            }
            _next = Time.unscaledTime + (!sent ? 1f : _acknowledged ? 10f : _delay);
            if (sent) _delay = Math.Min(_delay * 2f, 30f);
        }

        internal static bool Confirm(string nonce)
        {
            if (string.IsNullOrEmpty(_nonce) || string.IsNullOrEmpty(nonce) || _server != ServerId() || _player != Plat.Id(Player.Current) || !string.Equals(_nonce, nonce, StringComparison.Ordinal)) return false;
            if (!_acknowledged) Core.Log("REPAIR_SERVER_READY");
            _acknowledged = true;
            return true;
        }

        static bool Send(string command)
        {
            IPlayer current = Player.Current;
            if (current == null) return false;
            CommandSync sync = CommandSync.Instance;
            if (sync == null || sync.syncCommand == null) return false;
            string ready = CommandSync.SetupPlayerForCommand(command, current);
            if (string.IsNullOrEmpty(ready)) return false;
            sync.commandToSync = ready;
            if (_none == null) _none = new Il2CppReferenceArray<IPlayer>(Il2CppSystem.Array.CreateInstance(Il2CppType.Of<IPlayer>(), 0).Pointer);
            sync.syncCommand.SendToChunks(_none);
            return true;
        }
    }
}
