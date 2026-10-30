using System;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Sandbox
{
    public static class SharedMapChecks
    {
        public static string Run()
        {
            int checks = 0;
            for (int players = 0; players <= 5; players++)
            {
                for (int ready = 0; ready <= players; ready++)
                {
                    Check(SharedMapRules.CanStart(true, ready, players, 2, 1) == (players == 4 && ready == 4), ref checks);
                    Check(!SharedMapRules.CanStart(false, ready, players, 2, 1), ref checks);
                    Check(!SharedMapRules.CanStart(true, ready, players, 2, 2), ref checks);
                }
            }
            Check(SharedMapRules.CanEdit(true, true, 59.999d, 60d), ref checks);
            Check(!SharedMapRules.CanEdit(true, true, 60d, 60d), ref checks);
            Check(!SharedMapRules.CanEdit(true, true, 61d, 60d), ref checks);
            Check(!SharedMapRules.CanEdit(false, true, 20d, 60d), ref checks);
            Check(!SharedMapRules.CanEdit(true, false, 20d, 60d), ref checks);
            Check(SharedMapRules.IsValidPoint(Vector2.zero) && SharedMapRules.IsValidPoint(Vector2.one), ref checks);
            Check(!SharedMapRules.IsValidPoint(new Vector2(float.NaN, 0.5f)), ref checks);
            Check(!SharedMapRules.IsValidPoint(new Vector2(0.5f, float.PositiveInfinity)), ref checks);
            Check(!SharedMapRules.IsValidPoint(new Vector2(-0.01f, 0.5f)), ref checks);
            Check(!SharedMapRules.IsValidPoint(new Vector2(0.5f, 1.01f)), ref checks);
            Check(Mathf.Abs(SharedMapRules.DistanceToSegment(new Vector2(0.5f, 0.2f), Vector2.zero, Vector2.right) - 0.2f) < 0.001f, ref checks);
            Check(Mathf.Abs(SharedMapRules.DistanceToSegment(Vector2.one, Vector2.zero, Vector2.zero) - Mathf.Sqrt(2f)) < 0.001f, ref checks);

            var operations = new SharedMapOperation[SharedMapRules.ChunkSize];
            for (int i = 0; i < operations.Length; i++)
            {
                operations[i] = new SharedMapOperation
                {
                    Author = (uint)(i % 4 + 1), Stroke = (uint)(i + 1), From = Vector2.zero,
                    To = Vector2.one, Color = (byte)(i % 4), IsErase = i == operations.Length - 1
                };
            }
            var update = new SharedMapUpdate
            {
                Epoch = 7, Offset = 48, Total = 96, Operations = operations, Lighthouse = Vector2.one * 0.5f,
                IsEvening = true, IsEditing = true, Deadline = 1234.5d, EditSession = 3, ReadyCount = 4, Range = 3f
            };
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.Write(update);
                Check(writer.Position < 4096, ref checks);
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    SharedMapUpdate restored = reader.Read<SharedMapUpdate>();
                    Check(restored.Epoch == 7 && restored.Offset == 48 && restored.Total == 96, ref checks);
                    Check(restored.IsEditing && restored.Deadline == 1234.5d && restored.EditSession == 3, ref checks);
                    Check(restored.Operations.Length == SharedMapRules.ChunkSize && restored.Operations[SharedMapRules.ChunkSize - 1].IsErase, ref checks);
                    Check(restored.Operations[3].Author == 4 && restored.Operations[3].To == Vector2.one, ref checks);
                }
            }
            return $"SharedMapChecks PASS ({checks} checks): four-player gate, evening/day gate, deadline, invalid coordinates, eraser geometry, Mirror chunk roundtrip.";
        }

        private static void Check(bool result, ref int checks)
        {
            checks++;
            if (!result)
            {
                throw new InvalidOperationException($"Shared map check {checks} failed.");
            }
        }
    }
}
