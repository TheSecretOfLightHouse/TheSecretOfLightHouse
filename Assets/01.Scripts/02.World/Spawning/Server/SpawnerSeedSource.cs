using System;
using System.Security.Cryptography;

namespace Lighthouse.World.Spawning.Server
{
    public static class SpawnerSeedSource
    {
        public static ulong Next()
        {
            byte[] bytes = new byte[sizeof(ulong)];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
