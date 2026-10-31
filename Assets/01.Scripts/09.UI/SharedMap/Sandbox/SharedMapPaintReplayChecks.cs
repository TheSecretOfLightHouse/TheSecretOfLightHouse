using System;
using System.Collections.Generic;
using System.Reflection;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using Lighthouse.UI.SharedMap.View;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Sandbox
{
    public static class SharedMapPaintReplayChecks
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Run the paint replay fixture in play mode.");
            }
            var root = new GameObject("SharedMapPaintReplayFixture");
            SharedMapRuntime runtime = root.AddComponent<SharedMapRuntime>();
            runtime.enabled = false;
            root.GetComponent<SharedMapScreen>().enabled = false;
            SharedMapGraphic graphic = root.GetComponentInChildren<SharedMapGraphic>(true);
            MethodInfo receive = typeof(SharedMapRuntime).GetMethod("HandleUpdate", PrivateInstance);
            var pending = (List<SharedMapOperation>)typeof(SharedMapRuntime).GetField("_pendingOperations", PrivateInstance).GetValue(runtime);
            var first = new SharedMapOperation { Author = 1, Writer = 1, Stroke = 1, Sample = 1,
                From = new Vector2(0.2f, 0.27f), To = new Vector2(0.3f, 0.27f), BrushSize = 1 };
            try
            {
                Receive(receive, runtime, 21, 0, 0, Array.Empty<SharedMapOperation>());
                pending.Add(first);
                Render(graphic);
                Check(Pixel(graphic, 0.25f, 0.27f).a > 0.95f, "local preview without acknowledgement");
                Color edgeBefore = Pixel(graphic, 0.25f, 280f / 1023f);
                Receive(receive, runtime, 21, 0, 1, new[] { first });
                Render(graphic);
                Check(pending.Count == 0, "acknowledged preview removed");
                Check(Pixel(graphic, 0.25f, 280f / 1023f) == edgeBefore, "ack does not double-paint edges");
                SharedMapOperation erase = first;
                erase.Writer = 2;
                erase.IsErase = true;
                Receive(receive, runtime, 21, 1, 2, new[] { erase });
                Render(graphic);
                Check(Pixel(graphic, 0.25f, 0.27f).a < 0.1f, "different writer can erase with same sample number");
                first.Stroke = 2;
                first.Sample = 2;
                first.From = new Vector2(0.4f, 0.37f);
                first.To = new Vector2(0.5f, 0.37f);
                pending.Add(first);
                Render(graphic);
                Check(Pixel(graphic, 0.45f, 0.37f).a > 0.95f, "next local stroke visible");
                typeof(SharedMapRuntime).GetMethod("ClearPreview", PrivateInstance).Invoke(runtime, null);
                Render(graphic);
                Check(Pixel(graphic, 0.45f, 0.37f).a < 0.1f, "rejected/expired preview rolls back");
                Receive(receive, runtime, 22, 0, 0, Array.Empty<SharedMapOperation>());
                Check(Render(graphic) == 4, "constant canvas mesh after epoch reset");
                Check(runtime.Operations.Count == 0 && pending.Count == 0, "epoch resets confirmed and pending ink");
                SharedMapSettings settings = Resources.Load<SharedMapSettings>("SharedMapSettings");
                Check(settings && settings.PanelSprite && settings.ButtonSprite && settings.SelectedButtonSprite && settings.LighthouseSprite,
                    "existing UI skin references");
                return "SharedMapPaintReplayChecks PASS (9 checks): immediate preview, ack reconciliation, writer identity, erase, rollback, epoch reset, constant mesh, UI skin.";
            }
            finally
            {
                typeof(SharedMapScreen).GetMethod("SetOpen", PrivateInstance)
                    .Invoke(root.GetComponent<SharedMapScreen>(), new object[] { false });
                UnityEngine.Object.Destroy(root);
            }
        }

        private static int Render(SharedMapGraphic graphic)
        {
            using (var vertices = new UnityEngine.UI.VertexHelper())
            {
                typeof(SharedMapGraphic).GetMethod("OnPopulateMesh", PrivateInstance | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { vertices });
                return vertices.currentVertCount;
            }
        }

        private static Color Pixel(SharedMapGraphic graphic, float x, float y)
        {
            return ((Texture2D)graphic.mainTexture).GetPixel(Mathf.RoundToInt(x * 1023f), Mathf.RoundToInt(y * 1023f));
        }

        private static void Receive(MethodInfo method, SharedMapRuntime runtime, uint epoch, int offset, int total, SharedMapOperation[] operations)
        {
            method.Invoke(runtime, new object[] { new SharedMapUpdate
            {
                Epoch = epoch, Offset = offset, Total = total, Operations = operations,
                IsEditing = true, EditSession = 1, Deadline = Mirror.NetworkTime.time + 60d,
                Lighthouse = Vector2.one * 0.5f, Range = 3f
            }});
        }

        private static void Check(bool result, string name)
        {
            if (!result)
            {
                throw new InvalidOperationException("Shared map paint replay failed: " + name);
            }
        }
    }
}
