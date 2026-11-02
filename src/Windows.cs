using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace WindowWorkspaces
{
    // The windows that are open, kept from the game's own Show, Hide and Destroy.
    //
    // The game's list of window roots (GraphHandler.windowGraphNodeRoots) drops a window as soon as it gets a parent
    // item, so it never holds container windows. This list is updated only when a window opens or closes, which
    // costs nothing in between; readers still check IsVisible, which is also what Performance Fixes answers for a
    // window it hid by switching off its canvas.
    public static class Windows
    {
        const string HarmonyId = "mod." + WorkspacesMod.ModId;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static Harmony harmony;
        static readonly List<PixelWindow> open = new List<PixelWindow>();

        internal static void Patch()
        {
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            var shown = new HarmonyMethod(typeof(Windows).GetMethod("Shown", AnyStatic));
            var gone = new HarmonyMethod(typeof(Windows).GetMethod("Gone", AnyStatic));
            foreach (var m in typeof(PixelWindow).GetMethods(AnyInstance))
            {
                if (m.DeclaringType != typeof(PixelWindow)) continue;
                if (m.Name == "Show") harmony.Patch(m, null, shown);
                else if (m.Name == "Hide") harmony.Patch(m, null, gone);
            }
            harmony.Patch(typeof(PixelWindow).GetMethod("Destroy", AnyInstance, null, Type.EmptyTypes, null), new HarmonyMethod(typeof(Windows).GetMethod("Gone", AnyStatic)));
        }

        internal static void Unpatch()
        {
            if (harmony != null) { harmony.UnpatchAll(HarmonyId); harmony = null; }
            open.Clear();
        }

        static void Shown(PixelWindow __instance)
        {
            if (__instance != null && !open.Contains(__instance)) open.Add(__instance);
        }

        static void Gone(PixelWindow __instance)
        {
            open.Remove(__instance);
        }

        // Visible windows, oldest first. Stale entries (destroyed without Destroy, or hidden by other means) are
        // dropped on the way.
        public static void Visible(List<PixelWindow> into)
        {
            into.Clear();
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var w = open[i];
                bool alive;
                try { alive = w != null && w.handler != null && w.rectTransform != null; }
                catch (Exception) { alive = false; }
                if (!alive) { open.RemoveAt(i); continue; }
                if (w.IsVisible()) into.Add(w);
            }
            into.Reverse();
        }
    }
}
