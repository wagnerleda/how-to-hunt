using System;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using HowToHunt.Core;
using HowToHunt.Economy;
using HowToHunt.Foundation;
using HowToHunt.Player;
using UnityEngine;

namespace HowToHunt.Travel
{
    /// <summary>Client → host: take me to this region (I am at a totem).</summary>
    public struct TravelRequestBroadcast : IBroadcast
    {
        public uint Region;
    }

    public enum TravelRefusal : byte { None, NotAtTotem, Undiscovered, NoTotemThere, Downed, AlreadyThere, Unknown }

    /// <summary>Host → client: the travel happened (you are there now) or why not.</summary>
    public struct TravelResultBroadcast : IBroadcast
    {
        public uint Region;
        public byte Refusal;
    }

    /// <summary>
    /// Totem travel, host-authoritative: the host checks you stand at a totem, have been to where you're going and
    /// are on your feet, then moves you to that region's totem (PlayerMotor.ServerTravel). It also notices hunters
    /// walking into a region for the first time (EconomyNet.HostDiscover, saved with their profile).
    /// </summary>
    public static class TravelNet
    {
        public static readonly FloatCvar Reach = new FloatCvar("sv_totem_reach", 2f,
            "Extra distance (m) beyond a totem's reach at which the host still accepts consulting it.", CvarFlags.Replicated | CvarFlags.Cheat, 0f, 50f);
        public static readonly FloatCvar DiscoverInterval = new FloatCvar("sv_discover_interval", 1f,
            "How often (s) the host checks which region each hunter is in.", CvarFlags.Replicated | CvarFlags.Cheat, 0.1f, 10f);

        private static NetworkManager _manager;
        private static float _nextDiscover;

        /// <summary>Client: the host answered a travel request.</summary>
        public static event Action<TravelResultBroadcast> Result;

        private static bool IsHost => _manager != null && _manager.IsServerStarted;

        public static void Attach(NetworkManager m)
        {
            _manager = m;
            m.ServerManager.RegisterBroadcast<TravelRequestBroadcast>(OnRequest);
            m.ClientManager.RegisterBroadcast<TravelResultBroadcast>(OnResult);
        }

        public static void Detach() => _manager = null;

        // --- client -------------------------------------------------------------------------------------------

        public static bool Request(RegionDefinition region)
        {
            if (_manager == null || !_manager.IsClientStarted || region == null) return false;
            _manager.ClientManager.Broadcast(new TravelRequestBroadcast { Region = region.Id.Value });
            return true;
        }

        private static void OnResult(TravelResultBroadcast msg, Channel channel) => Result?.Invoke(msg);

        /// <summary>Has this hunter (our own profile) been to that region? Regions known from the start always count.</summary>
        public static bool Known(EconomyProfile p, RegionDefinition r) => r != null && (r.KnownFromStart || p.Discovered.Contains(r.Id.Value));

        // --- host ---------------------------------------------------------------------------------------------

        private static void OnRequest(NetworkConnection conn, TravelRequestBroadcast req, Channel channel)
        {
            if (!IsHost) return;
            TravelRefusal why = HostTravel(conn.ClientId, req.Region);
            conn.Broadcast(new TravelResultBroadcast { Region = req.Region, Refusal = (byte)why });
        }

        private static TravelRefusal HostTravel(int clientId, uint regionId)
        {
            RegionDefinition region = ContentRegistry.Get<RegionDefinition>(new ContentId(regionId));
            PlayerMotor motor = MotorOf(clientId);
            if (region == null || motor == null) return TravelRefusal.Unknown;
            PlayerHealth health = motor.GetComponent<PlayerHealth>();
            if (health != null && health.Downed) return TravelRefusal.Downed;
            Totem here = Totem.Near(motor.transform.position, Reach.Value);
            if (here == null) return TravelRefusal.NotAtTotem;
            if (here.Region == region) return TravelRefusal.AlreadyThere;
            if (!region.KnownFromStart && !EconomyNet.HostHasDiscovered(clientId, regionId)) return TravelRefusal.Undiscovered;
            Totem there = Totem.Of(region);
            if (there == null) return TravelRefusal.NoTotemThere;
            there.Arrival(out Vector3 feet, out float yaw);
            motor.ServerTravel(feet, yaw);
            return TravelRefusal.None;
        }

        /// <summary>Host, every frame: a few times a second, note the region each hunter stands in.</summary>
        public static void HostTick()
        {
            if (!IsHost || Time.unscaledTime < _nextDiscover) return;
            _nextDiscover = Time.unscaledTime + DiscoverInterval.Value;
            var all = PlayerMotor.All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerMotor m = all[i];
                if (m.Owner == null || !m.Owner.IsValid) continue;
                RegionDefinition r = Totem.RegionAt(m.transform.position);
                if (r != null && !r.KnownFromStart) EconomyNet.HostDiscover(m.Owner.ClientId, r);
            }
        }

        private static PlayerMotor MotorOf(int clientId)
        {
            var all = PlayerMotor.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Owner != null && all[i].Owner.ClientId == clientId) return all[i];
            return null;
        }

        public static string Explain(TravelRefusal why)
        {
            switch (why)
            {
                case TravelRefusal.NotAtTotem: return "Stand by the totem to travel.";
                case TravelRefusal.Undiscovered: return "You haven't been there yet. Find the way first.";
                case TravelRefusal.NoTotemThere: return "No totem stands there.";
                case TravelRefusal.Downed: return "You can't travel while down.";
                case TravelRefusal.AlreadyThere: return "You are already here.";
                default: return "The totem stays silent.";
            }
        }
    }
}
