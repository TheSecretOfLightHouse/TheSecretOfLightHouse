using System;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using Lighthouse.UI.SharedMap.View;
using Mirror;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Sandbox
{
    public static class SharedMapPaintChecks
    {
        public static string Run()
        {
            int checks = 0;
            using (var brush = new SharedMapBrush())
            {
                brush.Clear();
                brush.Paint(new Vector2(0.15f, 0.327f), new Vector2(0.85f, 0.327f), 3.8f, Color.black);
                brush.Upload();
                int row = Mathf.RoundToInt(0.327f * (SharedMapBrush.Resolution - 1));
                for (int x = 160; x < 860; x++)
                {
                    Check(brush.Texture.GetPixel(x, row).a > 0.95f, ref checks);
                }
                Check(brush.Texture.GetPixel(154, row + 2).a > 0.5f, ref checks);
                Check(brush.Texture.GetPixel(154, row + 7).a < 0.1f, ref checks);
                brush.Clear();
                brush.Paint(new Vector2(0.25f, 0.37f), new Vector2(0.25f, 0.37f), 6.8f, Color.red);
                brush.Upload();
                int dotX = Mathf.RoundToInt(0.25f * 1023f);
                int dotY = Mathf.RoundToInt(0.37f * 1023f);
                Check(brush.Texture.GetPixel(dotX, dotY).a > 0.95f, ref checks);
                Check(brush.Texture.GetPixel(dotX + 5, dotY).a > 0.95f, ref checks);
                Check(brush.Texture.GetPixel(dotX + 9, dotY).a < 0.1f, ref checks);
                brush.Clear();
                brush.Upload();
                Check(brush.Texture.GetPixel(dotX, dotY).a < 0.1f, ref checks);
                brush.Paint(Vector2.zero, Vector2.one, 6.8f, Color.blue);
                brush.Upload();
                Check(brush.Texture.GetPixel(0, 0).b > 0.95f && brush.Texture.GetPixel(1023, 1023).a > 0.95f, ref checks);
            }
            var batch = new SharedMapOperation[SharedMapRules.MaxBatchSize];
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i] = new SharedMapOperation { Author = 4, Writer = 4, Sample = (uint)(i + 1), Stroke = 8,
                    From = new Vector2(0.1f, 0.2f), To = new Vector2(0.2f, 0.3f), BrushSize = 2, Color = 3 };
            }
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.Write(new SharedMapRequest { Epoch = 7, Action = 2, Operations = batch });
                Check(writer.Position < 1024, ref checks);
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    SharedMapRequest restored = reader.Read<SharedMapRequest>();
                    Check(restored.Operations.Length == 16 && restored.Operations[15].Sample == 16, ref checks);
                    Check(restored.Operations[15].Writer == 4 && restored.Operations[15].BrushSize == 2, ref checks);
                }
            }
            return $"SharedMapPaintChecks PASS ({checks} checks): continuous raster, round cap, brush dot/size, clear, edge clipping, sampled batch roundtrip.";
        }

        private static void Check(bool result, ref int checks)
        {
            checks++;
            if (!result)
            {
                throw new InvalidOperationException($"Shared map paint check {checks} failed.");
            }
        }
    }
}
