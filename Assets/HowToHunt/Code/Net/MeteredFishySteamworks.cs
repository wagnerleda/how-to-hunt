using System;
using FishNet.Transporting;

namespace HowToHunt.Net
{
    /// <summary>Steam P2P transport with byte counting for the bandwidth overlay and budgets.</summary>
    public sealed class MeteredFishySteamworks : FishySteamworks.FishySteamworks
    {
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            NetMeter.CountOut(segment.Count);
            base.SendToServer(channelId, segment);
        }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            NetMeter.CountOut(segment.Count);
            base.SendToClient(channelId, segment, connectionId);
        }

        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs args)
        {
            NetMeter.CountIn(args.Data.Count);
            base.HandleClientReceivedDataArgs(args);
        }

        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs args)
        {
            NetMeter.CountIn(args.Data.Count);
            base.HandleServerReceivedDataArgs(args);
        }
    }
}
