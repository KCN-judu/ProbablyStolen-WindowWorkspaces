using System;
using System.Collections.Generic;

namespace WindowWorkspaces
{
    // A rectangle in screen pixels: left, right, bottom, top.
    public struct Box { public float L, R, B, T; }

    // The mod's decisions, kept apart from Unity and the game so tests/PureTest.cs can run them headless.
    public static class SnapRule
    {
        // The nearest edge line on one axis for the dragged box `me`: its own edge to another box's facing or
        // matching edge, or to an edge of the screen box, which is the last target. Only boxes that overlap `me` on
        // the other axis (within the release distance) count, so a box far above does not pull sideways. An edge
        // catches within `catchD`; the line held last frame (`held`, NaN for none) keeps its grip up to `releaseD`.
        // Returns false when nothing is in reach; `delta` moves `me` onto `line`.
        public static bool Best(Box me, IList<Box> targets, bool xAxis, float held, float catchD, float releaseD,
            out float delta, out float line)
        {
            float bestAbs = float.MaxValue;
            delta = 0f; line = 0f;
            float lo = xAxis ? me.L : me.B, hi = xAxis ? me.R : me.T;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                bool screen = i == targets.Count - 1;
                if (!screen)
                {
                    bool overlap = xAxis ? (me.B < t.T + releaseD && me.T > t.B - releaseD)
                                         : (me.L < t.R + releaseD && me.R > t.L - releaseD);
                    if (!overlap) continue;
                }
                float tlo = xAxis ? t.L : t.B, thi = xAxis ? t.R : t.T;
                Try(lo, screen ? tlo : thi, held, catchD, releaseD, ref bestAbs, ref delta, ref line);
                Try(hi, screen ? thi : tlo, held, catchD, releaseD, ref bestAbs, ref delta, ref line);
                if (!screen)
                {
                    Try(lo, tlo, held, catchD, releaseD, ref bestAbs, ref delta, ref line);
                    Try(hi, thi, held, catchD, releaseD, ref bestAbs, ref delta, ref line);
                }
            }
            return bestAbs < float.MaxValue;
        }

        static void Try(float edge, float target, float held, float catchD, float releaseD,
            ref float bestAbs, ref float delta, ref float line)
        {
            float d = target - edge, a = Math.Abs(d);
            float limit = !float.IsNaN(held) && Math.Abs(target - held) < 0.5f ? releaseD : catchD;
            if (a > limit || a >= bestAbs) return;
            bestAbs = a; delta = d; line = target;
        }

        // How far an edge moves to the nearest grid line, rounding as Unity's Mathf.Round does (halves to even).
        public static float GridDelta(float edge, float cell)
        {
            return (float)Math.Round(edge / cell) * cell - edge;
        }

        // The workspace after `current` among `count`, or before it when going back.
        public static int Next(int current, int count, bool back)
        {
            if (count < 1) return 0;
            return ((current + (back ? count - 1 : 1)) % count + count) % count;
        }
    }
}
