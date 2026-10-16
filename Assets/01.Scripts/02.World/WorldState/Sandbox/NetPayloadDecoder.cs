using System.Text;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Net;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Weather.Net;
using Lighthouse.World.Weather.Rules;
using Lighthouse.World.WorldState.Net;
using Lighthouse.World.WorldState.Rules;
using Mirror;

namespace Lighthouse.World.WorldState.Sandbox
{
    public static class NetPayloadDecoder
    {
        public static bool TryDecode(NetworkBehaviour behaviour, NetworkReader reader, out string text)
        {
            if (behaviour is WorldStateNet)
            {
                MatchStartInfo match = reader.Read<MatchStartInfo>();
                text = $"TotalDays={match.TotalDays} StartTime={match.StartTime:F3}";
                return true;
            }

            if (behaviour is WeatherStateNet)
            {
                WeatherTransitionInfo transition = reader.Read<WeatherTransitionInfo>();
                text = $"From={transition.From} To={transition.To} StartTime={transition.StartTime:F3} Duration={transition.Duration} IsSet={transition.IsSet}";
                return true;
            }

            if (behaviour is WeatherScheduleDriver)
            {
                text = "no serialized fields";
                return true;
            }

            if (behaviour is MapStateNet)
            {
                MapSeedInfo map = reader.Read<MapSeedInfo>();
                text = $"Seed={map.Seed} Layout=0x{map.LayoutHash:X8} Content=0x{map.ContentHash:X8} IsSet={map.IsSet}";
                return true;
            }

            if (behaviour is IslandStateNet)
            {
                text = DecodeIsland(reader);
                return true;
            }

            text = "undecoded";
            return false;
        }

        private static string DecodeIsland(NetworkReader reader)
        {
            int count = (int)reader.ReadUInt();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                NodeState node = reader.Read<NodeState>();
                builder.Append(i).Append(':').Append(node.ResourceId).Append('/').Append(node.Remaining).Append(' ');
            }

            uint pendingChanges = reader.ReadUInt();
            short islandIndex = reader.ReadShort();
            return $"island={islandIndex} count={count} pendingChanges={pendingChanges} nodes=[{builder}]";
        }
    }
}
