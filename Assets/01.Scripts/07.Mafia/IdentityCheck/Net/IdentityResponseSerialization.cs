using Lighthouse.Mafia.IdentityCheck.Rules;
using Mirror;

namespace Lighthouse.Mafia.IdentityCheck.Net
{
    public static class IdentityResponseSerialization
    {
        public static void WriteIdentityResponse(this NetworkWriter writer, IdentityResponse response)
        {
            writer.WriteInt((int)response.Result);
            writer.WriteBool(response.IsNewDecision);
            writer.WriteBool(response.HasPrivateState);
            writer.WriteInt(response.RemainingCheckCount);
            writer.WriteDouble(response.SummonExpiresAt);
            writer.WriteBool(response.HasSummonRight);
            writer.WriteBool(response.CanCheck);
            writer.WriteBool(response.CanSummon);
            writer.WriteULong(response.LastCheckRequestId);
            writer.WriteULong(response.LastSummonRequestId);
        }

        public static IdentityResponse ReadIdentityResponse(this NetworkReader reader)
        {
            return new IdentityResponse
            {
                Result = (IdentityActionResult)reader.ReadInt(),
                IsNewDecision = reader.ReadBool(),
                HasPrivateState = reader.ReadBool(),
                RemainingCheckCount = reader.ReadInt(),
                SummonExpiresAt = reader.ReadDouble(),
                HasSummonRight = reader.ReadBool(),
                CanCheck = reader.ReadBool(),
                CanSummon = reader.ReadBool(),
                LastCheckRequestId = reader.ReadULong(),
                LastSummonRequestId = reader.ReadULong()
            };
        }
    }
}
