using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace WindowWorkspaces
{
    // Workspaces of container windows, like virtual desktops.
    //
    // A workspace has a name and the container windows that were open in it, each with its position and tab
    // selection. Leaving a workspace records what is open; entering one closes the windows it does not have and
    // opens the ones it has, where they were. Windows open with the game's own OpenContentAction, which shows the
    // window and then places it from PlayerStore.contentWindowPositions, so the workspace writes its positions
    // there first and the game does the rest. Closing is the game's CloseContentWindow. Anything else that hooks
    // those calls, such as Performance Fixes' per-window canvases and fast hiding, applies as usual; with it, a
    // window hidden by a switch stays built and comes back almost at once.
    //
    // Opening a full storage window is real work for Unity, so a switch is spread over frames: each frame runs
    // queued steps until a 3 ms budget is spent (always at least one), closes first, so animations keep moving.
    //
    // Workspaces are stored per save slot in PlayerPrefs, by item id, which is stable within a save.
    public static class Workspaces
    {
        // At most nine workspaces.
        public const int Max = 9;
        const string Pref = "WindowWorkspaces.v2.slot.";
        const double BudgetMs = 3.0;

        // One container in a workspace: its item id and the window's position and tab selection when last seen.
        public sealed class Entry
        {
            public int Id;
            public Vector2 Position;
            public string Tabs;
        }

        public sealed class Space
        {
            public string Name;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        // One queued step of a switch: close a window, or open it at Position with the Tabs selection.
        sealed class Job
        {
            public GameItem Item;
            public bool Close;
            public Vector2 Position;
            public string Tabs;
        }

        static readonly List<Space> spaces = new List<Space>();
        static int current;
        static PlayerStore store;
        static int slot = int.MinValue;
        static bool dirty;
        static readonly Queue<Job> queue = new Queue<Job>();
        static readonly Stopwatch timer = new Stopwatch();
        static readonly List<PixelWindow> visible = new List<PixelWindow>();

        // PlayerStore.Instance creates a store when there is none, which must never happen from here (at the main
        // menu it even throws), so the store is read from its static field.
        static readonly System.Reflection.FieldInfo instanceField = typeof(PlayerStore).GetField("instance",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        public static int Current { get { return current; } }
        public static int Count { get { return spaces.Count; } }
        public static bool Ready { get { return store != null; } }
        public static Space Get(int i) { return spaces[i]; }

        // A workspace's name, or "Workspace N" when it has none.
        public static string NameOf(int i)
        {
            var n = spaces[i].Name;
            return string.IsNullOrEmpty(n) ? WorkspacesMod.L("workspace").Replace("#", (i + 1).ToString()) : n;
        }

        // On disable: forgets the save and every workspace (they stay in PlayerPrefs).
        internal static void Reset()
        {
            Flush();
            queue.Clear();
            store = null;
            slot = int.MinValue;
            spaces.Clear();
            current = 0;
        }

        // After leaving the store: queued steps and the last visible windows point at its items and windows.
        internal static void Forget()
        {
            Flush();
            queue.Clear();
            visible.Clear();
        }

        // Follows the loaded save: a new store or slot means other items and other workspaces.
        internal static void Track()
        {
            var ps = instanceField != null ? instanceField.GetValue(null) as PlayerStore : null;
            if (ps == store && (ps == null || ps.saveSlotId == slot)) return;
            Flush();
            queue.Clear();
            store = ps;
            slot = ps != null ? ps.saveSlotId : int.MinValue;
            spaces.Clear();
            current = 0;
            if (ps != null)
            {
                Load();
                if (spaces.Count == 0) spaces.Add(new Space());
                if (current >= spaces.Count) current = 0;
            }
            Manager.Close();
            UnityEngine.Debug.Log(WorkspacesMod.Tag + (ps != null
                ? "save slot " + slot + ": " + spaces.Count + " workspace(s), in " + (current + 1) : "no save loaded"));
        }

        // ---- Changing the list -----------------------------------------------------------------------------------

        // A new, empty workspace at the end. Returns its index, or -1 at the limit.
        public static int Add()
        {
            if (store == null || spaces.Count >= Max) return -1;
            spaces.Add(new Space());
            Save();
            return spaces.Count - 1;
        }

        // Deleting the workspace you are in first moves to a neighbour, so its windows close with it.
        public static void Remove(int i)
        {
            if (store == null || spaces.Count <= 1 || i < 0 || i >= spaces.Count) return;
            if (i == current) SwitchTo(i > 0 ? i - 1 : 1, false);
            spaces.RemoveAt(i);
            if (current > i) current--;
            Save();
            Toast.Show(current, spaces.Count);
        }

        public static void Rename(int i, string name)
        {
            if (i < 0 || i >= spaces.Count) return;
            spaces[i].Name = string.IsNullOrEmpty(name) || name.Trim().Length == 0 ? null : name.Trim();
            dirty = true;
        }

        // Takes one container out of a workspace; in the current one its window closes too.
        public static void Forget(int i, int id)
        {
            if (i < 0 || i >= spaces.Count) return;
            if (i == current)
            {
                foreach (var item in OpenContainers())
                    if (item.GetUniqueID() == id) { item.CloseContentWindow(false); break; }
                Capture(current);
            }
            spaces[i].Entries.RemoveAll(e => e.Id == id);
            Save();
        }

        // ---- Switching -------------------------------------------------------------------------------------------

        // The next-workspace key. With a single workspace it only shows the notice.
        public static void Cycle(bool back)
        {
            if (store == null) return;
            int n = spaces.Count;
            if (n < 2) { Toast.Show(current, n); return; }
            SwitchTo(SnapRule.Next(current, n, back));
        }

        public static void SwitchTo(int target) { SwitchTo(target, true); }

        // Records the open windows into the workspace being left, queues closes for windows the target does not have
        // and opens for those it has. A window of the target that is already open is only moved. A switch
        // started during a switch drops the queue and starts from what is on screen.
        static void SwitchTo(int target, bool notify)
        {
            if (store == null || target < 0 || target >= spaces.Count) return;
            if (target == current && queue.Count == 0) { if (notify) Toast.Show(current, spaces.Count); return; }
            // A switch during a switch starts from what is on screen now.
            queue.Clear();
            if (target != current) Capture(current);

            var wanted = spaces[target].Entries;
            var ids = new HashSet<int>();
            foreach (var e in wanted) ids.Add(e.Id);

            foreach (var item in OpenContainers())
                if (!ids.Contains(item.GetUniqueID())) queue.Enqueue(new Job { Item = item, Close = true });

            if (wanted.Count > 0)
            {
                var items = ItemsById(ids);
                foreach (var e in wanted)
                {
                    GameItem item;
                    if (!items.TryGetValue(e.Id, out item) || !CanOpen(item)) continue;
                    var w = item.contentWindow;
                    if (w.IsVisible()) { Place(w, e.Position); continue; }
                    queue.Enqueue(new Job { Item = item, Position = e.Position, Tabs = e.Tabs });
                }
            }
            current = target;
            Save();
            if (notify) Toast.Show(current, spaces.Count);
            Manager.Refresh();
            UnityEngine.Debug.Log(WorkspacesMod.Tag + "workspace " + (target + 1) + ": " + wanted.Count + " window(s)");
        }

        // Once per frame: runs queued steps within BudgetMs, closes first (they were queued first).
        internal static void Step()
        {
            if (queue.Count == 0) return;
            timer.Reset();
            timer.Start();
            do
            {
                var s = queue.Dequeue();
                try { Run(s); }
                catch (Exception e) { UnityEngine.Debug.LogWarning(WorkspacesMod.Tag + "window step failed: " + e.Message); }
            } while (queue.Count > 0 && timer.Elapsed.TotalMilliseconds < BudgetMs);
            timer.Stop();
        }

        // Runs one step. Opening writes the stored position to PlayerStore.contentWindowPositions, which
        // OpenContentAction reads, and then restores the tabs; locked or no longer owned containers are skipped.
        static void Run(Job s)
        {
            var item = s.Item;
            if (item == null || item.contentWindow == null) return;
            if (s.Close)
            {
                if (item.contentWindow.IsVisible()) item.CloseContentWindow(false);
                return;
            }
            if (item.contentWindow.IsVisible() || !CanOpen(item)) return;
            var opener = ItemMouseDoubleClickHandler.current;
            if (opener == null) return;
            var positions = store.contentWindowPositions;
            if (positions == null) store.contentWindowPositions = positions = new Dictionary<int, Vector2>();
            positions[item.GetUniqueID()] = s.Position;
            opener.OpenContentAction(item, Input.mousePosition);
            if (!string.IsNullOrEmpty(s.Tabs) && item.contentWindow.IsVisible()) item.ApplyContentTabsSelection(s.Tabs);
        }

        static void Place(PixelWindow w, Vector2 position)
        {
            w.rectTransform.anchoredPosition = position;
            w.FixBounds();
        }

        // ---- What is open ----------------------------------------------------------------------------------------

        // Replaces workspace i's entries with the windows open now.
        static void Capture(int i)
        {
            if (i < 0 || i >= spaces.Count) return;
            var list = spaces[i].Entries;
            list.Clear();
            foreach (var item in OpenContainers())
            {
                var w = item.contentWindow;
                string tabs = null;
                try { tabs = item.GetContentTabsSelection(); } catch (Exception) { }
                list.Add(new Entry { Id = item.GetUniqueID(), Position = w.rectTransform.anchoredPosition, Tabs = tabs });
            }
        }

        // The manager shows the live state of the workspace you are in.
        internal static void CaptureCurrent() { if (store != null) { Capture(current); Save(); } }

        // Items whose own content window is visible, in opening order.
        static List<GameItem> OpenContainers()
        {
            var result = new List<GameItem>();
            Windows.Visible(visible);
            foreach (var w in visible)
            {
                var owners = w.parentItems;
                if (owners == null) continue;
                foreach (var item in owners)
                    if (item != null && item.contentWindow == w) { result.Add(item); break; }
            }
            return result;
        }

        // Finds the store's items with the given ids; ids that no longer exist are missing from the result.
        internal static Dictionary<int, GameItem> ItemsById(HashSet<int> ids)
        {
            var found = new Dictionary<int, GameItem>();
            if (store == null || ids.Count == 0) return found;
            foreach (var item in store.FindAllItem(true))
            {
                if (item == null) continue;
                int id = item.GetUniqueID();
                if (ids.Contains(id)) found[id] = item;
            }
            return found;
        }

        // A container whose window exists, is not locked and whose item the player owns.
        static bool CanOpen(GameItem item)
        {
            var w = item.contentWindow;
            return w != null && !w.isLocked && GeneralHelper.IsItemOwned(item);
        }

        // ---- Storage ---------------------------------------------------------------------------------------------
        //
        // One PlayerPrefs string per save slot:
        //   current
        //   name|id,x,y,tabs;id,x,y,tabs;...     (one line per workspace; name and tabs escaped)

        static string Key() { return Pref + slot; }

        internal static void Flush() { if (dirty) Save(); }

        // Writes all workspaces and the current index to PlayerPrefs. Renames only set `dirty`;
        // the write happens when the manager closes (Flush) or on the next change.
        static void Save()
        {
            dirty = false;
            if (store == null) return;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(current).Append('\n');
            foreach (var s in spaces)
            {
                sb.Append(Uri.EscapeDataString(s.Name ?? "")).Append('|');
                foreach (var e in s.Entries)
                {
                    sb.Append(e.Id).Append(',')
                      .Append(e.Position.x.ToString("R", inv)).Append(',')
                      .Append(e.Position.y.ToString("R", inv)).Append(',')
                      .Append(Uri.EscapeDataString(e.Tabs ?? "")).Append(';');
                }
                sb.Append('\n');
            }
            try { PlayerPrefs.SetString(Key(), sb.ToString()); PlayerPrefs.Save(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning(WorkspacesMod.Tag + "could not save workspaces: " + e.Message); }
        }

        // Reads what Save wrote; malformed lines or entries are skipped, at most Max workspaces are taken.
        static void Load()
        {
            string text;
            try { text = PlayerPrefs.GetString(Key(), ""); } catch (Exception) { return; }
            if (string.IsNullOrEmpty(text)) return;
            var inv = CultureInfo.InvariantCulture;
            var lines = text.Split('\n');
            int c;
            if (int.TryParse(lines[0], out c)) current = Math.Max(0, c);
            for (int i = 1; i < lines.Length && spaces.Count < Max; i++)
            {
                var line = lines[i];
                int bar = line.IndexOf('|');
                if (bar < 0) continue;
                var space = new Space();
                try { space.Name = Uri.UnescapeDataString(line.Substring(0, bar)); } catch (Exception) { }
                if (string.IsNullOrEmpty(space.Name)) space.Name = null;
                foreach (var part in line.Substring(bar + 1).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = part.Split(',');
                    int id; float x, y;
                    if (f.Length < 4 || !int.TryParse(f[0], out id) ||
                        !float.TryParse(f[1], NumberStyles.Float, inv, out x) ||
                        !float.TryParse(f[2], NumberStyles.Float, inv, out y)) continue;
                    string tabs = null;
                    try { tabs = Uri.UnescapeDataString(f[3]); } catch (Exception) { }
                    space.Entries.Add(new Entry { Id = id, Position = new Vector2(x, y), Tabs = string.IsNullOrEmpty(tabs) ? null : tabs });
                }
                spaces.Add(space);
            }
        }
    }
}
