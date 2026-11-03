using System;
using UnityEngine;

namespace WindowWorkspaces
{
    // The switch notice: a small machine window of the game's own, so it is drawn with the game's sprites and
    // follows any texture pack.
    //
    //   WORKSPACE
    //   ( 1 Warehouse )  (o 2 Counter )  ( 3 )
    //
    // One radio button per workspace, the current one lit; clicking one switches. It opens near the top of the
    // screen on each switch and closes after a moment, unless the pointer is over it.
    public static class Toast
    {
        const float Hold = 1.4f;
        const int NamedUpTo = 5, NameLength = 12;

        static PixelWindow window;
        static MachineryPanel panel;
        static MachineryPanelRow row;
        static float hideAt;

        static bool IsOpen { get { return window != null && window.handler != null && window.IsVisible(); } }

        public static void Show(int current, int count)
        {
            if (count <= 0) return;
            if (window == null || window.handler == null)
            {
                panel = MachineHelper.Panel(WorkspacesMod.L("toast_panel"), new MachineryStack(false, 4), "control", false);
                row = new MachineryPanelRow(new[] { panel }).SetColumns(1);
                window = new PixelWindow(false, WorkspacesMod.L("toast_title"), WindowStyle.Machinery);
                window.Attach(row);
            }
            var stack = new MachineryStack(false, 6).SetAlign(MachineryStack.Align.Center);
            for (int i = 0; i < count; i++)
            {
                int n = i;
                string label = (i + 1).ToString();
                if (count <= NamedUpTo && i < Workspaces.Count && !string.IsNullOrEmpty(Workspaces.Get(i).Name))
                {
                    var name = Workspaces.Get(i).Name;
                    label += " " + (name.Length > NameLength ? name.Substring(0, NameLength - 1) + "…" : name);
                }
                MachineryRadioButton b = null;
                b = new MachineryRadioButton(label, i == current, on =>
                {
                    if (n != Workspaces.Current) Workspaces.SwitchTo(n);
                    else if (b != null) b.SetOn(true, false);
                });
                stack.Add(b);
            }
            panel.SetContent(stack);
            row.Validate();
            window.Validate();
            bool wasOpen = IsOpen;
            if (!wasOpen) window.Show();
            Place();
            window.ToFront(true);
            hideAt = Time.unscaledTime + Hold;
        }

        // Centred near the top of the screen, on whole pixels.
        static void Place()
        {
            var rt = window.rectTransform;
            if (rt == null) return;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float cx = (c[0].x + c[2].x) * 0.5f, top = c[1].y;
            var p = rt.position;
            rt.position = new Vector3(Mathf.Round(p.x + Screen.width * 0.5f - cx), Mathf.Round(p.y + Screen.height * 0.95f - top), p.z);
        }

        internal static void Tick()
        {
            if (!IsOpen || Time.unscaledTime < hideAt) return;
            var rt = window.rectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null))
            {
                hideAt = Time.unscaledTime + 0.3f;
                return;
            }
            window.Hide();
        }

        internal static void Destroy()
        {
            if (IsOpen) window.Hide();
            window = null; panel = null; row = null;
        }

        // After leaving the store: its window went with the scene, so it is only let go of.
        internal static void Forget()
        {
            window = null; panel = null; row = null;
        }
    }
}
