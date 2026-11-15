using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using HowToHunt.Core;
using HowToHunt.Net;
using HowToHunt.Player;
using UnityEngine;

[assembly: FeatureModule(typeof(PlayerModule))]

namespace HowToHunt.Player
{
    /// <summary>Player movement cvars, spawning, and the local camera.</summary>
    public sealed class PlayerModule : IFeatureModule
    {
        private NetworkObject _playerPrefab;
        private int _nextSpawn;

        public string Id => "player";
        public int Order => 0;

        public void Register(ModuleRegistrar r)
        {
            MovementCvars.Register(r.Console);
            DemoRecorder.Register(r.Console);
            r.Cvar(MovementHud.ShowSpeed);
            r.Cvar(MovementHud.Crosshair);
            PlayerCamera.Register(r.Console);
            r.Command("respawn", "Move yourself back to a spawn point (host only for now).", _ => RespawnLocal());
            r.Command("noclip", "Fly through walls (sv_cheats 1).", _ =>
            {
                if (PlayerMotor.Local == null) { r.Console.Warn("noclip: no local player."); return; }
                if (!r.Console.Cheats.Value) { r.Console.Warn("noclip requires sv_cheats 1 (host)."); return; }
                PlayerMotor.Local.CmdToggleNoclip();
            });
            r.Command("setpos", "setpos <x> <y> <z>: teleport your feet there (sv_cheats 1).", args =>
            {
                if (PlayerMotor.Local == null || args.Count < 3) { r.Console.Warn("Usage: setpos x y z"); return; }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float, inv, out float x) ||
                    !float.TryParse(args[1], System.Globalization.NumberStyles.Float, inv, out float y) ||
                    !float.TryParse(args[2], System.Globalization.NumberStyles.Float, inv, out float z)) { r.Console.Warn("setpos: bad numbers."); return; }
                PlayerMotor.Local.CmdSetPos(new Vector3(x, y, z));
            });
            r.Command("getpos", "Print your feet position, view angles and speed.", _ =>
            {
                PlayerMotor m = PlayerMotor.Local;
                if (m == null) return;
                var s = m.State;
                float feetY = s.Position.Y - MovementCvars.Config.Hull(s.Ducked).Y;
                r.Console.Print(System.FormattableString.Invariant($"setpos {s.Position.X:0.00} {feetY:0.00} {s.Position.Z:0.00}; yaw {m.ViewYaw:0.0} pitch {m.ViewPitch:0.0}; speed {Sim.Movement.PlayerMove.HorizontalSpeed(s.Velocity):0.00} m/s"));
            });
        }

        public void Initialize()
        {
            _playerPrefab = Resources.Load<NetworkObject>("Player/PlayerAvatar");
            if (_playerPrefab == null) Debug.LogError("[Player] Missing Resources/Player/PlayerAvatar prefab.");
            NetSession.ManagerCreated += OnManagerCreated;
            PlayerMotor.LocalPlayerStarted += PlayerCamera.Attach;
            MovementHud.Ensure();
            Game.Root.gameObject.AddComponent<MovementAudio>();
            Game.Root.gameObject.AddComponent<LandingDust>();
        }

        public void Shutdown()
        {
            DemoRecorder.Unregister();
            NetSession.ManagerCreated -= OnManagerCreated;
            PlayerMotor.LocalPlayerStarted -= PlayerCamera.Attach;
        }

        private void OnManagerCreated(NetworkManager manager)
        {
            _nextSpawn = 0;
            manager.SceneManager.OnClientLoadedStartScenes += (conn, asServer) =>
            {
                if (asServer) SpawnPlayer(manager, conn);
            };
        }

        private void SpawnPlayer(NetworkManager manager, NetworkConnection conn)
        {
            if (_playerPrefab == null) return;
            GetSpawn(out Vector3 feet, out float yaw);
            float halfHeight = MovementCvars.Config.HullHalfExtents.Y;
            NetworkObject nob = manager.GetPooledInstantiated(_playerPrefab, feet + new Vector3(0f, halfHeight, 0f), Quaternion.Euler(0f, yaw, 0f), true);
            nob.name = $"Player #{conn.ClientId}";
            manager.ServerManager.Spawn(nob, conn);
        }

        private void GetSpawn(out Vector3 feet, out float yaw)
        {
            SpawnPoint[] points = Object.FindObjectsByType<SpawnPoint>();
            System.Array.Sort(points, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (points.Length == 0)
            {
                feet = new Vector3(0f, 0.1f, 0f);
                yaw = 0f;
                return;
            }
            SpawnPoint p = points[_nextSpawn++ % points.Length];
            feet = p.transform.position + new Vector3(0f, 0.02f, 0f);
            yaw = p.transform.eulerAngles.y;
        }

        private void RespawnLocal()
        {
            PlayerMotor local = PlayerMotor.Local;
            if (local == null || !local.IsServerStarted)
            {
                Game.Console.Warn("respawn: only the host can respawn for now.");
                return;
            }
            GetSpawn(out Vector3 feet, out _);
            local.ServerTeleport(feet + new Vector3(0f, MovementCvars.Config.HullHalfExtents.Y, 0f));
        }
    }
}
