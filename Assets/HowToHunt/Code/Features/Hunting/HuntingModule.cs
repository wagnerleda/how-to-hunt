using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Net;

[assembly: FeatureModule(typeof(HuntingModule))]

namespace HowToHunt.Hunting
{
    /// <summary>
    /// Hunting: weapons, hit registration and targets. M2 ships the dev pistol and shooting-range dummies to prove
    /// lag-compensated hit registration; prey, capture tools and kill tools build on this in M3.
    /// </summary>
    public sealed class HuntingModule : IFeatureModule
    {
        private NetworkManager _manager;

        public string Id => "hunting";
        public int Order => 10;

        public void Register(ModuleRegistrar r)
        {
            HuntingCvars.Register(r.Console);
            r.Command("wpn_autofire", "wpn_autofire <dummy 0-2> <shots>: test aimbot, shoots a dummy where this machine sees it (sv_cheats 1).", args =>
            {
                if (args.Count < 2 || !int.TryParse(args[0], out int d) || !int.TryParse(args[1], out int n)) { r.Console.Warn("Usage: wpn_autofire <dummy> <shots>"); return; }
                PlayerWeapon.ShotsFired = 0;
                PlayerWeapon.HitsConfirmed = 0;
                PlayerWeapon.AutoFireTarget = d;
                PlayerWeapon.AutoFireShots = n;
            }, Foundation.CommandFlags.Cheat);
            r.Command("wpn_stats", "Shots fired and hits confirmed by the host since the last wpn_autofire.", _ =>
                r.Console.Print($"[hit] {PlayerWeapon.HitsConfirmed}/{PlayerWeapon.ShotsFired} shots confirmed as hits"));
            r.Command("dummy_reset", "Restore all target dummies to full health (host).", _ =>
            {
                if (!NetSession.IsServer) return;
                for (int i = 0; i < TargetDummy.All.Count; i++) SetAndSend(i, TargetDummy.All[i].MaxHealth, false);
            });
        }

        public void Initialize()
        {
            Game.Root.gameObject.AddComponent<HitFeedback>();
            NetSession.ManagerCreated += OnManagerCreated;
        }

        public void Shutdown() => NetSession.ManagerCreated -= OnManagerCreated;

        private void OnManagerCreated(NetworkManager m)
        {
            _manager = m;
            m.TimeManager.OnTick += OnTick;
            m.ClientManager.RegisterBroadcast<DummyStateBroadcast>(OnDummyState);
            m.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
        }

        /// <summary>Host: record every dummy's pose each tick (lag compensation history).</summary>
        private void OnTick()
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            uint tick = _manager.TimeManager.Tick;
            int rate = _manager.TimeManager.TickRate;
            var dummies = TargetDummy.All;
            for (int i = 0; i < dummies.Count; i++)
            {
                bool wasDown = dummies[i].IsDown;
                dummies[i].RecordTick(tick, rate);
                if (wasDown && !dummies[i].IsDown) Send(i);
            }
        }

        /// <summary>Host: a player just loaded in; tell them every dummy's state.</summary>
        private void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn.IsLocalClient) return;
            var dummies = TargetDummy.All;
            for (int i = 0; i < dummies.Count; i++)
                _manager.ServerManager.Broadcast(conn, new DummyStateBroadcast { Index = i, Health = dummies[i].Health, Down = dummies[i].IsDown });
        }

        private void OnDummyState(DummyStateBroadcast msg, Channel channel)
        {
            if (_manager != null && _manager.IsServerStarted) return; // host already applied it
            if (msg.Index >= 0 && msg.Index < TargetDummy.All.Count) TargetDummy.All[msg.Index].SetState(msg.Health, msg.Down);
        }

        private void SetAndSend(int index, float health, bool down)
        {
            TargetDummy.All[index].SetState(health, down);
            Send(index);
        }

        private void Send(int index)
        {
            if (_manager == null || !_manager.IsServerStarted) return;
            TargetDummy d = TargetDummy.All[index];
            _manager.ServerManager.Broadcast(new DummyStateBroadcast { Index = index, Health = d.Health, Down = d.IsDown });
        }
    }
}
