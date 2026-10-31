using System;
using System.Reflection;
using System.Collections.Generic;
using Lighthouse.UI.SharedMap.Net;
using Lighthouse.UI.SharedMap.Rules;
using Lighthouse.UI.SharedMap.View;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.Sandbox
{
    public static class SharedMapReplayChecks
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Run in play mode; the fixture must not change scene assets.");
            }
            GameObject fixture = CreateFixture(out SharedMapRuntime runtime, out SharedMapScreen screen);
            try
            {
                MethodInfo receive = typeof(SharedMapRuntime).GetMethod("HandleUpdate", PrivateInstance);
                var first = new SharedMapOperation { Author = 1, Stroke = 1, From = new Vector2(0.1f, 0.1f), To = new Vector2(0.4f, 0.1f) };
                var second = new SharedMapOperation { Author = 2, Stroke = 2, From = new Vector2(0.6f, 0.8f), To = new Vector2(0.8f, 0.8f), Color = 2 };
                Apply(receive, runtime, 7, 0, 2, new[] { first });
                Require(runtime.Operations.Count == 1 && !runtime.IsCaughtUp, "partial replay");
                Apply(receive, runtime, 7, 0, 2, new[] { first });
                Require(runtime.Operations.Count == 1, "duplicate chunk ignored");
                Apply(receive, runtime, 7, 2, 3, new[] { second });
                Require(runtime.Operations.Count == 1, "gap ignored for subsequent pull");
                Apply(receive, runtime, 7, 1, 2, new[] { second });
                Require(runtime.IsCaughtUp && runtime.Operations.Count == 2, "catch-up");
                SharedMapGraphic graphic = fixture.GetComponentInChildren<SharedMapGraphic>(true);
                Require(graphic.GetComponent<CanvasRenderer>() != null, "drawing renderer");
                Require(graphic.TryFindStroke(new Vector2(0.2f, 0.1f), out SharedMapOperation hit) && hit.Author == 1, "eraser hit");
                first.IsErase = true;
                Apply(receive, runtime, 7, 2, 3, new[] { first });
                Require(!graphic.TryFindStroke(new Vector2(0.2f, 0.1f), out SharedMapOperation _), "erased stroke hidden");
                Require(graphic.TryFindStroke(new Vector2(0.7f, 0.8f), out hit) && hit.Author == 2, "other author's stroke preserved");
                Apply(receive, runtime, 8, 0, 0, Array.Empty<SharedMapOperation>());
                Require(runtime.IsCaughtUp && runtime.Operations.Count == 0, "new round clears ink");
                var timed = new SharedMapUpdate
                {
                    Epoch = 8, IsEditing = true, EditSession = 1,
                    Deadline = Mirror.NetworkTime.time + 60d, Operations = Array.Empty<SharedMapOperation>()
                };
                FieldInfo isOpen = typeof(SharedMapScreen).GetField("_open", PrivateInstance);
                receive.Invoke(runtime, new object[] { timed });
                Require((bool)isOpen.GetValue(screen), "session opens editor");
                timed.Deadline = Mirror.NetworkTime.time - 1d;
                receive.Invoke(runtime, new object[] { timed });
                Require(!(bool)isOpen.GetValue(screen), "deadline closes editor");
                receive.Invoke(runtime, new object[] { timed });
                Require(!(bool)isOpen.GetValue(screen), "late status cannot reopen expired editor");
                CursorLockMode savedLock = Cursor.lockState;
                bool savedVisible = Cursor.visible;
                MethodInfo open = typeof(SharedMapScreen).GetMethod("SetOpen", PrivateInstance);
                open.Invoke(screen, new object[] { true });
                Require(Cursor.visible && Cursor.lockState == CursorLockMode.None, "cursor released");
                open.Invoke(screen, new object[] { false });
                Require(Cursor.visible == savedVisible && Cursor.lockState == savedLock, "cursor restored");
                return "SharedMapReplayChecks PASS (14 checks): renderer, partial/duplicate/gapped replay, catch-up, erasure, other author, round reset, deadline close/stale status, cursor restore.";
            }
            finally
            {
                UnityEngine.Object.Destroy(fixture);
            }
        }

        public static string Preview()
        {
            GameObject fixture = CreateFixture(out SharedMapRuntime runtime, out SharedMapScreen screen);
            fixture.name = "SharedMapPreviewFixture";
            MethodInfo receive = typeof(SharedMapRuntime).GetMethod("HandleUpdate", PrivateInstance);
            var ink = new List<SharedMapOperation>();
            AddContour(ink, 1, 1, new Vector2(0.25f, 0.73f), new Vector2(0.12f, 0.08f));
            AddContour(ink, 2, 2, new Vector2(0.74f, 0.30f), new Vector2(0.10f, 0.12f));
            AddContour(ink, 3, 3, new Vector2(0.77f, 0.78f), new Vector2(0.07f, 0.06f));
            for (int i = 0; i < 80; i++)
            {
                float t = i / 80f;
                float next = (i + 1) / 80f;
                SharedMapOperation route = Segment(3, 4, 0.29f + t * 0.42f, 0.65f - t * 0.30f + Mathf.Sin(t * 10f) * 0.025f,
                    0.29f + next * 0.42f, 0.65f - next * 0.30f + Mathf.Sin(next * 10f) * 0.025f);
                route.BrushSize = 0;
                ink.Add(route);
            }
            Apply(receive, runtime, 11, 0, ink.Count, ink.ToArray());
            typeof(SharedMapScreen).GetMethod("SetOpen", PrivateInstance).Invoke(screen, new object[] { true });
            var title = (UnityEngine.UI.Text)typeof(SharedMapScreen).GetField("_title", PrivateInstance).GetValue(screen);
            title.text = "공동 작성  ·  남은 시간 60초";
            Canvas.ForceUpdateCanvases();
            return "Preview only: freehand outlines and route are fixture data, not actual island positions. Destroy SharedMapPreviewFixture after capture.";
        }

        private static void AddContour(List<SharedMapOperation> ink, uint author, uint stroke, Vector2 centre, Vector2 radius)
        {
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= 120; i++)
            {
                float angle = i / 120f * Mathf.PI * 2f;
                float variation = 1f + 0.18f * Mathf.Sin(angle * 3f) + 0.08f * Mathf.Sin(angle * 7f);
                Vector2 point = centre + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * variation, radius);
                if (i > 0)
                {
                    SharedMapOperation operation = Segment(author, stroke, previous.x, previous.y, point.x, point.y);
                    operation.BrushSize = 1;
                    ink.Add(operation);
                }
                previous = point;
            }
        }

        private static SharedMapOperation Segment(uint author, uint stroke, float x, float y, float u, float v)
        {
            return new SharedMapOperation { Author = author, Stroke = stroke, From = new Vector2(x, y), To = new Vector2(u, v), Color = (byte)(author - 1) };
        }

        private static GameObject CreateFixture(out SharedMapRuntime runtime, out SharedMapScreen screen)
        {
            var fixture = new GameObject("SharedMapReplayFixture");
            runtime = fixture.AddComponent<SharedMapRuntime>();
            runtime.enabled = false;
            screen = fixture.GetComponent<SharedMapScreen>();
            screen.enabled = false;
            return fixture;
        }

        private static void Apply(MethodInfo receive, SharedMapRuntime runtime, uint epoch, int offset, int total, SharedMapOperation[] operations)
        {
            receive.Invoke(runtime, new object[] { new SharedMapUpdate
            {
                Epoch = epoch, Offset = offset, Total = total, Operations = operations,
                Lighthouse = new Vector2(0.5f, 0.5f), Range = 3f
            }});
        }

        private static void Require(bool result, string name)
        {
            if (!result)
            {
                throw new InvalidOperationException("Shared map replay check failed: " + name);
            }
        }
    }
}
