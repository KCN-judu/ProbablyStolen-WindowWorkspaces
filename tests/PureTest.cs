// Checks the mod's decisions (src/*.Pure.cs) outside the game: no game or Unity assembly is needed.
//   csc -out:PureTest.exe tests\PureTest.cs src\SnapRule.Pure.cs && PureTest.exe
using System;
using System.Collections.Generic;
using WindowWorkspaces;

static class PureTest
{
    const float Catch = 6f, Release = 10f;
    static readonly Box Screen = new Box { L = 0, R = 1920, B = 0, T = 1080 };
    static int failed;

    static void Check(string what, bool ok)
    {
        if (!ok) failed++;
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + what);
    }

    static Box B(float l, float b, float w, float h) { return new Box { L = l, R = l + w, B = b, T = b + h }; }

    static List<Box> With(params Box[] others)
    {
        var list = new List<Box>(others);
        list.Add(Screen);   // the screen is always the last target
        return list;
    }

    static bool Snap(Box me, List<Box> targets, bool x, float held, out float delta, out float line)
    {
        return SnapRule.Best(me, targets, x, held, Catch, Release, out delta, out line);
    }

    static int Main()
    {
        float d, line;
        var other = B(100, 100, 200, 200);   // L 100, R 300, B 100, T 300

        Console.WriteLine("-- edges");
        Check("left edge 4 px right of a window's right edge snaps onto it",
            Snap(B(304, 120, 100, 100), With(other), true, float.NaN, out d, out line) && d == -4 && line == 300);
        Check("7 px away does not catch", !Snap(B(307, 120, 100, 100), With(other), true, float.NaN, out d, out line));
        Check("7 px away holds the line grabbed last frame",
            Snap(B(307, 120, 100, 100), With(other), true, 300, out d, out line) && d == -7 && line == 300);
        Check("11 px away lets go even of the held line",
            !Snap(B(311, 120, 100, 100), With(other), true, 300, out d, out line));
        Check("matching edges line up: left to left, just above it",
            Snap(B(103, 305, 100, 50), With(other), true, float.NaN, out d, out line) && d == -3 && line == 100);
        Check("a window far above does not pull sideways",
            !Snap(B(304, 600, 100, 100), With(other), true, float.NaN, out d, out line));
        Check("screen edge: left edge 5 px from the screen's left",
            Snap(B(5, 500, 100, 100), With(), true, float.NaN, out d, out line) && d == -5 && line == 0);
        Check("screen edge: top edge 2 px under the screen's top",
            Snap(B(900, 878, 100, 200), With(), false, float.NaN, out d, out line) && d == 2 && line == 1080);
        Check("nearest of two edges wins",
            Snap(B(302, 120, 100, 100), With(other, B(405, 120, 50, 50)), true, float.NaN, out d, out line) && line == 300);
        Check("vertical: bottom edge 3 px above a window's top snaps down onto it",
            Snap(B(120, 303, 100, 100), With(other), false, float.NaN, out d, out line) && d == -3 && line == 300);

        Console.WriteLine("-- grid");
        Check("grid: 13 to the 8 px grid moves to 16", SnapRule.GridDelta(13, 8) == 3);
        Check("grid: 11 moves back to 8", SnapRule.GridDelta(11, 8) == -3);
        Check("grid: on a line stays", SnapRule.GridDelta(24, 8) == 0);
        Check("grid: halves round to even like Unity (12 -> 16, 20 -> 16)",
            SnapRule.GridDelta(12, 8) == 4 && SnapRule.GridDelta(20, 8) == -4);

        Console.WriteLine("-- cycling");
        Check("next after the last wraps to the first", SnapRule.Next(2, 3, false) == 0);
        Check("previous before the first wraps to the last", SnapRule.Next(0, 3, true) == 2);
        Check("next in the middle", SnapRule.Next(1, 4, false) == 2);
        Check("no workspaces: stays at 0", SnapRule.Next(0, 0, false) == 0);

        Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
