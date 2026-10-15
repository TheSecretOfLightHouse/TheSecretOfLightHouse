using System;
using System.Security.Cryptography;

namespace Lighthouse.World.WorldState.Server
{
    public static class ForecastSeedSource
    {
        private const int SeedByteCount = sizeof(ulong);

        public static ulong Next()
        {
            byte[] bytes = new byte[SeedByteCount];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
