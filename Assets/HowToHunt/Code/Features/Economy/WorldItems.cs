using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Sim.Hunting;
using UnityEngine;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Economy
{
    /// <summary>Host → everyone: an item appeared (carcass, drop, throw) with its starting motion at a tick.</summary>
    public struct WorldItemSpawnBroadcast : IBroadcast
    {
        public ushort Id;
        public byte Kind;
        public uint Content;
        public byte Flags;
        public ushort Count;
        public Vector3 Position;
        public Vector3 Velocity;
        public uint Tick;
        public bool Resting;
    }

    /// <summary>Host → everyone: an item came to rest here (corrects any drift), or was removed (picked up, rotted).</summary>
    public struct WorldItemUpdateBroadcast : IBroadcast
    {
        public ushort Id;
        public bool Removed;
        public Vector3 Position;
    }

    public sealed class WorldItem
    {
        public ushort Id;
        public ItemStack Item;
        public ItemBody Body;
        public Vec3 PreviousPosition;
        public float ExpireAt;
        public Vec3 HalfExtents;
        public GameObject Visual;
    }

    /// <summary>
    /// Items lying in or flying through the world: carcasses from kills, catches that didn't fit, drops and throws.
    /// The host owns them (spawn, pick-up, expiry). The motion is deterministic (ItemPhysics on the shared brush world),
    /// so every machine simulates it from the spawn message; only "came to rest here" and removals are sent after that.
    /// </summary>
    public static class WorldItems
    {
        public static readonly FloatCvar Lifetime = new FloatCvar("sv_item_lifetime", 120f, "Seconds a dropped item or carcass stays before it disappears.", CvarFlags.Replicated | CvarFlags.Cheat, 5f, 3600f);
        public static readonly FloatCvar PickupReach = new FloatCvar("sv_pickup_reach", 2.6f, "How close (m) a player must be to pick an item up.", CvarFlags.Replicated | CvarFlags.Cheat, 0.5f, 10f);
        private const int MaxItems = 256;

        private static readonly List<WorldItem> Items = new List<WorldItem>(64);
        private static NetworkManager _manager;
        private static ushort _nextId = 1;

        public static IReadOnlyList<WorldItem> All => Items;
        private static bool IsHost => _manager != null && _manager.IsServerStarted;

        public static void Attach(NetworkManager m)
        {
            Clear();
            _manager = m;
            m.TimeManager.OnTick += OnTick;
            m.ClientManager.RegisterBroadcast<WorldItemSpawnBroadcast>(OnSpawn);
            m.ClientManager.RegisterBroadcast<WorldItemUpdateBroadcast>(OnUpdate);
            m.SceneManager.OnClientLoadedStartScenes += OnClientLoaded;
        }

        public static void Detach()
        {
            _manager = null;
            Clear();
        }

        private static void Clear()
        {
            foreach (WorldItem w in Items) if (w.Visual != null) UnityEngine.Object.Destroy(w.Visual);
            Items.Clear();
        }

        public static WorldItem Get(ushort id)
        {
            for (int i = 0; i < Items.Count; i++) if (Items[i].Id == id) return Items[i];
            return null;
        }

        private static Vec3 HalfExtentsFor(in ItemStack item)
        {
            PreyDefinition def = ContentRegistry.Get<PreyDefinition>(new ContentId(item.Content));
            Vector3 h = def != null ? def.HalfExtents * Mathf.Min(1f, def.VisualScale) : new Vector3(0.1f, 0.1f, 0.1f);
            return new Vec3(Mathf.Max(0.06f, h.x), Mathf.Max(0.06f, h.y), Mathf.Max(0.06f, h.z));
        }

        // --- host -------------------------------------------------------------------------------------------

        /// <summary>Host: put an item into the world.</summary>
        public static void HostSpawn(in ItemStack item, Vector3 position, Vector3 velocity)
        {
            if (!IsHost || item.IsEmpty) return;
            if (Items.Count >= MaxItems) HostRemove(Items[0]); // oldest goes first
            var w = new WorldItem
            {
                Id = _nextId++,
                Item = item,
                Body = new ItemBody { Position = position.ToSim(), Velocity = velocity.ToSim() },
                ExpireAt = Time.time + Lifetime.Value,
            };
            if (_nextId == 0) _nextId = 1;
            w.HalfExtents = HalfExtentsFor(item);
            w.PreviousPosition = w.Body.Position;
            Items.Add(w);
            WorldItemRenderer.Create(w);
            SendToReady(SpawnMessage(w));
        }

        public static void HostRemove(WorldItem w)
        {
            if (w.Visual != null) UnityEngine.Object.Destroy(w.Visual);
            Items.Remove(w);
            if (IsHost) SendToReady(new WorldItemUpdateBroadcast { Id = w.Id, Removed = true });
        }

        private static WorldItemSpawnBroadcast SpawnMessage(WorldItem w) => new WorldItemSpawnBroadcast
        {
            Id = w.Id,
            Kind = (byte)w.Item.Kind,
            Content = w.Item.Content,
            Flags = w.Item.Flags,
            Count = w.Item.Count,
            Position = w.Body.Position.ToUnity(),
            Velocity = w.Body.Velocity.ToUnity(),
            Tick = _manager.TimeManager.Tick,
            Resting = w.Body.Resting,
        };

        private static void SendToReady<T>(T msg) where T : struct, IBroadcast
        {
            foreach (NetworkConnection c in _manager.ServerManager.Clients.Values)
                if (c.IsAuthenticated) _manager.ServerManager.Broadcast(c, msg);
        }

        private static void OnClientLoaded(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn.IsLocalClient || !IsHost) return;
            foreach (WorldItem w in Items) _manager.ServerManager.Broadcast(conn, SpawnMessage(w));
        }

        // --- everyone ---------------------------------------------------------------------------------------

        private static void OnTick()
        {
            if (_manager == null) return;
            float dt = (float)_manager.TimeManager.TickDelta;
            float now = Time.time;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                WorldItem w = Items[i];
                if (IsHost && now >= w.ExpireAt) { HostRemove(w); continue; }
                bool wasResting = w.Body.Resting;
                w.PreviousPosition = w.Body.Position;
                ItemPhysics.Step(ref w.Body, SimWorld.Current, w.HalfExtents, dt);
                if (IsHost && !wasResting && w.Body.Resting)
                    SendToReady(new WorldItemUpdateBroadcast { Id = w.Id, Position = w.Body.Position.ToUnity() });
            }
        }

        private static void OnSpawn(WorldItemSpawnBroadcast msg, Channel channel)
        {
            if (IsHost) return;
            WorldItem existing = Get(msg.Id);
            if (existing != null) return;
            var w = new WorldItem
            {
                Id = msg.Id,
                Item = new ItemStack { Kind = (ItemKind)msg.Kind, Content = msg.Content, Flags = msg.Flags, Count = msg.Count },
                Body = new ItemBody { Position = msg.Position.ToSim(), Velocity = msg.Velocity.ToSim(), Resting = msg.Resting },
            };
            w.HalfExtents = HalfExtentsFor(w.Item);
            // Catch up to now: the host started simulating at msg.Tick.
            int behind = Mathf.Clamp((int)(_manager.TimeManager.Tick - msg.Tick), 0, 256);
            float dt = (float)_manager.TimeManager.TickDelta;
            for (int i = 0; i < behind && !w.Body.Resting; i++) ItemPhysics.Step(ref w.Body, SimWorld.Current, w.HalfExtents, dt);
            w.PreviousPosition = w.Body.Position;
            Items.Add(w);
            WorldItemRenderer.Create(w);
        }

        private static void OnUpdate(WorldItemUpdateBroadcast msg, Channel channel)
        {
            if (IsHost) return;
            WorldItem w = Get(msg.Id);
            if (w == null) return;
            if (msg.Removed)
            {
                if (w.Visual != null) UnityEngine.Object.Destroy(w.Visual);
                Items.Remove(w);
                return;
            }
            w.Body.Position = msg.Position.ToSim();
            w.Body.Velocity = Vec3.Zero;
            w.Body.Resting = true;
        }

        /// <summary>The item the local player is looking at or standing next to, within reach (null if none).</summary>
        public static WorldItem Targeted(Vector3 eye, Vector3 forward, float reach)
        {
            WorldItem best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Items.Count; i++)
            {
                WorldItem w = Items[i];
                Vector3 p = w.Body.Position.ToUnity();
                Vector3 to = p - eye;
                float dist = to.magnitude;
                if (dist > reach + 0.5f) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle > 40f && dist > 1.4f) continue;
                float score = angle + dist * 10f;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            return best;
        }
    }
}
