using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowWorkspaces
{
    // The workspace manager, built like the game's own storage-preset manager (same window kind, rows and chips):
    //
    //   WORKSPACES
    //   Switch between sets of open windows...           [ New workspace ]
    //   +----------------------------------------------------------------+
    //   | [ Warehouse____ ]  CURRENT  4 windows                       X |
    //   | [icon] Fridge  [icon] Shelf  [icon] Crate  [icon] Toolbox     |
    //   |                                                                |
    //   | [ Workspace 2__ ]  [ Switch ]  2 windows                     X |
    //   | [icon] Safe  [icon] Locker                                    |
    //   +----------------------------------------------------------------+
    //
    // A name field per workspace, Switch (or CURRENT), a delete cross when there is more than one, and the
    // containers as item chips: clicking a chip takes that container out of the workspace. The list is rebuilt
    // whenever it changes; the window remembers where it was.
    public static class Manager
    {
        const string WindowId = "window_workspaces";
        const string DimColor = "#8E887C", CurrentColor = "#4CC3B5";

        static CustomUIWindow window;
        static RectTransform listHost;
        static Vector2? lastPosition;
        static bool warned;

        static bool IsOpen { get { return window != null && window.IsAlive; } }

        // The manager key.
        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Close()
        {
            if (IsOpen) window.Close();
            window = null;
            listHost = null;
        }

        public static void Refresh()
        {
            if (IsOpen) Fill();
        }

        // Opens at the last position (centred the first time). The current workspace is captured first so its row
        // shows the windows that are open now.
        static void Open()
        {
            if (!Workspaces.Ready) return;
            var ui = CustomUIManager.Instance;
            if (ui == null)
            {
                if (!warned) { warned = true; Debug.LogWarning(WorkspacesMod.Tag + "the game's window manager is not ready"); }
                return;
            }
            var old = ui.GetWindow(WindowId);
            if (old != null && old.IsAlive) old.Close();

            Workspaces.CaptureCurrent();
            var b = ui.CreateWindow(WindowId, WorkspacesMod.L("window_title"), "paper", null, CustomUIManager.Style.Auto)
                .SetSize(760f, 440f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .OnClose(Closed);
            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddLabel(WorkspacesMod.L("manager_desc"), null);
            b.AddFlexibleSpace();
            b.AddButton(WorkspacesMod.L("button_new"), New, "ww_new").WithTooltip(WorkspacesMod.L("tip_new"));
            Widen(b.Last, 1.4f);
            Size(b.Last, -1f, 32f);
            b.End();
            b.BeginScroll(300f);
            listHost = b.CurrentContainer;
            b.End();
            if (lastPosition.HasValue) b.SetPosition(lastPosition.Value); else b.Center();
            window = b.Show();
            Fill();
        }

        // Close callback: remembers the position and writes any pending rename.
        static void Closed()
        {
            if (window != null && window.Rect != null) lastPosition = window.Rect.anchoredPosition;
            window = null;
            listHost = null;
            Workspaces.Flush();
        }

        static void New()
        {
            if (Workspaces.Add() < 0) return;
            Fill();
        }

        // Rebuilds every row from the workspaces; items are looked up once for all rows.
        static void Fill()
        {
            if (listHost == null || !IsOpen) return;
            CustomUIBuilder.ClearContainer(listHost);
            var b = new CustomUIBuilder(CustomUIManager.Instance, window, listHost);

            var ids = new HashSet<int>();
            for (int i = 0; i < Workspaces.Count; i++)
                foreach (var e in Workspaces.Get(i).Entries) ids.Add(e.Id);
            var items = Workspaces.ItemsById(ids);

            for (int i = 0; i < Workspaces.Count; i++)
            {
                int n = i;
                var space = Workspaces.Get(i);
                bool here = i == Workspaces.Current;
                if (i > 0) b.AddSpace(14f);
                b.BeginColumn(4f, TextAnchor.UpperLeft);

                b.BeginRow(6f, TextAnchor.MiddleLeft);
                b.AddInput(Workspaces.NameOf(i), space.Name ?? "", s => Workspaces.Rename(n, s), null, null);
                Size(b.Last, 180f, 32f);
                if (here) b.AddLabel("<color=" + CurrentColor + ">" + WorkspacesMod.L("current") + "</color>", null);
                else
                {
                    b.AddButton(WorkspacesMod.L("button_switch"), () => Workspaces.SwitchTo(n), null)
                        .WithTooltip(WorkspacesMod.L("tip_switch"));
                    Widen(b.Last, 1.5f);
                    Size(b.Last, -1f, 32f);
                }
                string count = space.Entries.Count == 0 ? WorkspacesMod.L("empty")
                    : WorkspacesMod.L("windows").Replace("#", space.Entries.Count.ToString());
                b.AddLabel("<color=" + DimColor + ">" + count + "</color>", null);
                b.AddFlexibleSpace();
                if (Workspaces.Count > 1)
                    b.AddCloseButton(() => { Workspaces.Remove(n); Fill(); }, null).WithTooltip(WorkspacesMod.L("tip_delete"));
                b.End();

                if (space.Entries.Count == 0)
                    b.AddLabel("<color=" + DimColor + ">" + WorkspacesMod.L(here ? "hint_here" : "hint_empty") + "</color>", null);
                else
                {
                    b.BeginGrid(4, 170f, 124f, 4f);
                    foreach (var e in space.Entries)
                    {
                        int id = e.Id;
                        GameItem item;
                        Action remove = () => { Workspaces.Forget(n, id); Fill(); };
                        if (items.TryGetValue(id, out item))
                        {
                            var el = item as GameItemElement;
                            Sprite sprite = null;
                            try { if (el != null) sprite = el.ResolveSpriteByName(item.spritePath); } catch (Exception) { }
                            MachineStorageCoordinator.AddChip(b, sprite, item.GetDisplayName(true), WorkspacesMod.L("tip_chip"), remove);
                        }
                        else MachineStorageCoordinator.AddChip(b, null, WorkspacesMod.L("missing"), WorkspacesMod.L("tip_missing"), remove);
                    }
                    b.End();
                }
                b.End();
            }
            CustomUIBuilder.RebuildLayout(listHost);
        }

        static void Size(CustomUIElement e, float w, float h)
        {
            if (e != null) e.SetPreferredSize(w, h);
        }

        // A button as wide as its text needs, times a margin, pinned so the row cannot squeeze it (as the game's
        // own preset manager does); otherwise a long label is cut off.
        static void Widen(CustomUIElement e, float factor)
        {
            var r = e != null ? e.Rect : null;
            if (r == null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(r);
            float w = UnityEngine.UI.LayoutUtility.GetPreferredWidth(r);
            if (w <= 0f) w = UnityEngine.UI.LayoutUtility.GetMinWidth(r);
            if (w <= 0f) return;
            var le = r.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.minWidth = le.preferredWidth = Mathf.Ceil(w * factor);
            le.flexibleWidth = 0f;
        }
    }
}
