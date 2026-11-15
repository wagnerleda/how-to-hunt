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
            r.Command("respawn", "Move yourself back to a spawn point (host only for now).", _ => RespawnLocal());
        }

        public void Initialize()
        {
            _playerPrefab = Resources.Load<NetworkObject>("Player/PlayerAvatar");
            if (_playerPrefab == null) Debug.LogError("[Player] Missing Resources/Player/PlayerAvatar prefab.");
            NetSession.ManagerCreated += OnManagerCreated;
            PlayerMotor.LocalPlayerStarted += PlayerCamera.Attach;
        }

        public void Shutdown()
        {
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
