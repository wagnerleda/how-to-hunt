using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Foundation;
using HowToHunt.Hunting;
using HowToHunt.Player;
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

    /// <summary>
    /// Host → everyone: an item came to rest here (corrects any drift), was removed (picked up, rotted), or was
    /// shoved by a hit (Moving: starts from Position with Velocity at Tick).
    /// </summary>
    public struct WorldItemUpdateBroadcast : IBroadcast
    {
        public ushort Id;
        public bool Removed;
        public Vector3 Position;
        public bool Moving;
        public Vector3 Velocity;
        public uint Tick;
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
        /// <summary>Host: client id of the thrower while a throw is in flight (-1 = not a throw, or already landed/hit).</summary>
        public int ThrownBy = -1;
        /// <summary>Client: the rotation drawn last frame (eases into the rest pose instead of snapping).</summary>
        public Quaternion Shown;
        public bool HasShown;
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
        private const float MinHitSpeed = 3f; // m/s: a throw slower than this just lands on them
        private const float ThrowSellSlack = 1f; // m beyond the trader's radius: hitting the counter counts

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
            PlayerTools.HostToolReached -= OnToolReached;
            PlayerTools.HostToolReached += OnToolReached;
            PlayerTools.HostExplosion -= OnExplosion;
            PlayerTools.HostExplosion += OnExplosion;
        }

        public static void Detach()
        {
            PlayerTools.HostToolReached -= OnToolReached;
            PlayerTools.HostExplosion -= OnExplosion;
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

        private static Vec3 HalfExtentsFor(in ItemStack item, ushort id) => ItemShapes.HalfExtents(item, id);

        // --- host -------------------------------------------------------------------------------------------

        /// <summary>Host: put an item into the world.</summary>
        public static void HostSpawn(in ItemStack item, Vector3 position, Vector3 velocity, int thrownBy = -1)
        {
            if (!IsHost || item.IsEmpty) return;
            if (Items.Count >= MaxItems) HostRemove(Items[0]); // oldest goes first
            var w = new WorldItem
            {
                Id = _nextId++,
                Item = item,
                Body = new ItemBody { Position = position.ToSim(), Velocity = velocity.ToSim() },
                ExpireAt = Time.time + Lifetime.Value,
                ThrownBy = thrownBy,
            };
            if (_nextId == 0) _nextId = 1;
            w.HalfExtents = HalfExtentsFor(item, w.Id);
            w.PreviousPosition = w.Body.Position;
            Items.Add(w);
            WorldItemRenderer.Create(w);
            SendToReady(SpawnMessage(w));
        }

        /// <summary>
        /// Host: a throw in flight. Prey thrown into the trader are sold. A throw that strikes a prey or another player
        /// slows them a little for a moment (never a stun); the item flies on. Returns true if the item was removed.
        /// </summary>
        private static bool HostThrownContact(WorldItem w)
        {
            Vec3 pos = w.Body.Position;
            Vector3 upos = pos.ToUnity();
            if (w.Item.Kind == ItemKind.Prey && CampStation.Nearest(upos, CampStationKind.Sell, ThrowSellSlack) != null
                && EconomyNet.HostSellThrown(w.ThrownBy, w.Item))
            {
                HostRemove(w);
                return true;
            }
            if (w.Body.Velocity.LengthSquared() < MinHitSpeed * MinHitSpeed) return false;
            float itemR = MathF.Max(w.HalfExtents.X, MathF.Max(w.HalfExtents.Y, w.HalfExtents.Z));
            float factor = MovementCvars.Config.HitSlowFactor, seconds = EconomyNet.ThrowSlowTime.Value;

            var prey = PreyWorld.All;
            for (int i = 0; i < prey.Count; i++)
            {
                PreyInstance p = prey[i];
                if (!p.Alive || p.IsFlyer) continue;
                Vec3 ext = p.Def.Behavior.HalfExtents;
                Vec3 center = p.State.Position + new Vec3(0f, ext.Y, 0f);
                float r = MathF.Max(ext.X, MathF.Max(ext.Y, ext.Z)) + itemR + 0.05f;
                if (Vec3.DistanceSquared(center, pos) > r * r) continue;
                PreyBrain.Slow(ref p.State, factor, seconds);
                w.ThrownBy = -1;
                return false;
            }

            var players = PlayerMotor.All;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerMotor m = players[i];
                if (m.Owner != null && m.Owner.ClientId == w.ThrownBy) continue;
                Vec3 half = MovementCvars.Config.Hull(m.State.Ducked);
                Vec3 d = Vec3.Abs(m.State.Position - pos);
                if (d.X > half.X + itemR || d.Z > half.Z + itemR || d.Y > half.Y + itemR) continue;
                m.HostSlow(seconds);
                w.ThrownBy = -1;
                return false;
            }
            return false;
        }

        /// <summary>Host: shove an item (a hit, a blast). Everyone re-simulates it from the new motion.</summary>
        public static void HostPush(WorldItem w, Vector3 velocity)
        {
            if (!IsHost || velocity.sqrMagnitude < 1e-4f) return;
            w.Body.Velocity += velocity.ToSim();
            w.Body.Resting = false;
            w.ExpireAt = Mathf.Max(w.ExpireAt, Time.time + 10f);
            SendToReady(new WorldItemUpdateBroadcast
            {
                Id = w.Id, Position = w.Body.Position.ToUnity(), Moving = true, Velocity = w.Body.Velocity.ToUnity(), Tick = _manager.TimeManager.Tick,
            });
        }

        /// <summary>Host: a blast throws nearby items outward and up, harder near the centre.</summary>
        private static void OnExplosion(Vector3 point, float radius, float force)
        {
            if (!IsHost) return;
            for (int i = 0; i < Items.Count; i++)
            {
                WorldItem w = Items[i];
                Vector3 to = w.Body.Position.ToUnity() - point;
                float share = Sim.Combat.GunMath.Blast(to.magnitude, radius * 1.5f);
                if (share <= 0f) continue;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                HostPush(w, (dir + Vector3.up * 0.8f) * (force * share));
            }
        }

        /// <summary>Host: a tool reached this far. A shot shoves the first item on its line; a swing shoves items in its arc.</summary>
        private static void OnToolReached(ToolDefinition tool, Vector3 origin, Vector3 dir, float reach)
        {
            if (!IsHost || tool.Knockback <= 0f) return;
            // Items hit get a little lift so they hop rather than slide.
            Vector3 lift = Vector3.up * (tool.Knockback * 0.35f);
            if (tool.Archetype == ToolArchetype.Firearm || tool.ArcDegrees <= 0f)
            {
                WorldItem first = null;
                float best = reach;
                for (int i = 0; i < Items.Count; i++)
                {
                    WorldItem w = Items[i];
                    var bounds = new Bounds(w.Body.Position.ToUnity(), w.HalfExtents.ToUnity() * 2f + Vector3.one * 0.1f);
                    if (bounds.IntersectRay(new Ray(origin, dir), out float d) && d < best) { best = d; first = w; }
                }
                if (first != null) HostPush(first, dir * tool.Knockback + lift);
                return;
            }
            float half = tool.ArcDegrees * 0.5f;
            for (int i = 0; i < Items.Count; i++)
            {
                WorldItem w = Items[i];
                Vector3 to = w.Body.Position.ToUnity() - origin;
                float dist = to.magnitude;
                if (dist > reach + 0.3f || dist < 1e-3f || Vector3.Angle(dir, to) > half) continue;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 push = flat.sqrMagnitude > 1e-4f ? flat.normalized : new Vector3(dir.x, 0f, dir.z).normalized;
                HostPush(w, push * tool.Knockback + lift);
            }
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
                if (IsHost && w.ThrownBy >= 0)
                {
                    if (w.Body.Resting) w.ThrownBy = -1;
                    else if (HostThrownContact(w)) continue; // sold: removed
                }
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
            w.HalfExtents = HalfExtentsFor(w.Item, w.Id);
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
            if (msg.Moving)
            {
                // Shoved: re-simulate from the host's motion at that tick, catching up to now.
                w.Body.Velocity = msg.Velocity.ToSim();
                w.Body.Resting = false;
                int behind = Mathf.Clamp((int)(_manager.TimeManager.Tick - msg.Tick), 0, 256);
                float dt = (float)_manager.TimeManager.TickDelta;
                for (int i = 0; i < behind && !w.Body.Resting; i++) ItemPhysics.Step(ref w.Body, SimWorld.Current, w.HalfExtents, dt);
                w.PreviousPosition = w.Body.Position;
                return;
            }
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
