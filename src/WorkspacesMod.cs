using System;
using UnityEngine;

namespace WindowWorkspaces
{
    // Window snapping and workspaces of container windows.
    //
    // Nothing in the game is patched. Snapping runs right before the frame is drawn, after the game's own input
    // loop has moved a dragged window, and only while the mouse button is held (see Snap).
    // Workspaces open and close windows through the game's own calls, so whatever else hooks those (Performance
    // Fixes' canvases and fast hiding among them) applies unchanged (see Workspaces).
    public sealed class WorkspacesMod : IMod
    {
        public const string ModId = "window_workspaces";
        internal const string Tag = "[WindowWorkspaces] ";
        const string KeyPref = "WindowWorkspaces.key.";

        static string modId = ModId;
        static GameObject ticker;
        static bool optionsAdded, warned;
        static ConfigEntry<bool> snapOn, gridSnapOn, gridShown;

        // Two keys cover everything: cycle through the workspaces (Shift for back) and open the manager. No
        // defaults: both are bound in the options menu.
        const int CycleKey = 0, ManagerKey = 1;
        static readonly string[] keyNames = { "cycle", "manager" };
        static readonly KeyCode[] keys = new KeyCode[2];
        static bool lateWarned;

        public void Init(ModManifest manifest)
        {
            if (manifest != null && !string.IsNullOrEmpty(manifest.ID)) modId = manifest.ID;
        }

        public void OnEnable()
        {
            ReadConfig();
            try { Windows.Patch(); }
            catch (Exception e) { Debug.LogWarning(Tag + "could not follow windows: " + e); }
            Canvas.willRenderCanvases -= BeforeRender;
            Canvas.willRenderCanvases += BeforeRender;
            if (ticker == null)
            {
                ticker = new GameObject("WindowWorkspaces.Ticker");
                UnityEngine.Object.DontDestroyOnLoad(ticker);
                ticker.hideFlags = HideFlags.HideAndDontSave;
                ticker.AddComponent<Ticker>();
            }
        }

        public void OnDisable()
        {
            Canvas.willRenderCanvases -= BeforeRender;
            Windows.Unpatch();
            if (ticker != null) { UnityEngine.Object.Destroy(ticker); ticker = null; }
            Snap.Destroy();
            Manager.Close();
            Toast.Destroy();
            Workspaces.Reset();
        }

        internal static string L(string key) { return ModHelper.GetLocalized(modId, key); }

        static KeyCode LoadKey(int i)
        {
            try
            {
                int k = PlayerPrefs.GetInt(KeyPref + keyNames[i], (int)KeyCode.None);
                return Enum.IsDefined(typeof(KeyCode), k) ? (KeyCode)k : KeyCode.None;
            }
            catch (Exception) { return KeyCode.None; }
        }

        static void SaveKey(int i, KeyCode k)
        {
            keys[i] = k;
            try { PlayerPrefs.SetInt(KeyPref + keyNames[i], (int)k); PlayerPrefs.Save(); } catch (Exception) { }
        }

        static void ReadConfig()
        {
            for (int i = 0; i < keys.Length; i++) keys[i] = LoadKey(i);
            if (optionsAdded) { ApplySnap(); return; }
            optionsAdded = true;
            try
            {
                var config = ModHelper.GetConfig(modId);
                snapOn = config.Define("snap", true, "Snapping", () => L("opt_snap"), () => L("opt_snap_tip"), false);
                gridSnapOn = config.Define("gridSnap", true, "Snapping", () => L("opt_grid_snap"), () => L("opt_grid_snap_tip"), false);
                gridShown = config.Define("gridShown", true, "Snapping", () => L("opt_grid_shown"), () => L("opt_grid_shown_tip"), false);
                snapOn.OnChanged += _ => ApplySnap();
                gridSnapOn.OnChanged += _ => ApplySnap();
                gridShown.OnChanged += _ => ApplySnap();
            }
            catch (Exception e) { Debug.LogWarning(Tag + "settings unavailable, using defaults: " + e.Message); }
            try
            {
                ModHelper.AddGameOptionKeyBind(modId, "keybind", "cycle", () => L("opt_key_cycle"),
                    () => keys[CycleKey], k => SaveKey(CycleKey, k), () => L("opt_key_cycle_tip"));
                ModHelper.AddGameOptionKeyBind(modId, "keybind", "manager", () => L("opt_key_manager"),
                    () => keys[ManagerKey], k => SaveKey(ManagerKey, k), () => L("opt_key_manager_tip"));
            }
            catch (Exception e) { Debug.LogWarning(Tag + "key bindings unavailable: " + e.Message); }
            ApplySnap();
        }

        static void ApplySnap()
        {
            Snap.Enabled = snapOn == null || snapOn.Value;
            Snap.GridSnap = gridSnapOn == null || gridSnapOn.Value;
            Snap.GridShown = gridShown == null || gridShown.Value;
        }

        static int announced;
        static bool Announce(string what)
        {
            if (announced < 5) { announced++; Debug.Log(Tag + what + (Workspaces.Ready ? "" : " (no save loaded yet)")); }
            return true;
        }

        internal static void Tick()
        {
            try
            {
                Workspaces.Track();
                if (!KeyBindHelper.SuppressHotkeys)
                {
                    var cy = keys[CycleKey];
                    if (cy != KeyCode.None && Input.GetKeyDown(cy) && Announce("cycle key " + cy))
                        Workspaces.Cycle(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                    var mg = keys[ManagerKey];
                    if (mg != KeyCode.None && Input.GetKeyDown(mg) && Announce("manager key " + mg)) Manager.Toggle();
                }
                Workspaces.Step();
                Toast.Tick();
            }
            catch (Exception e)
            {
                if (!warned) { warned = true; Debug.LogWarning(Tag + "update failed: " + e); }
            }
        }

        // Snapping runs right before canvases are drawn: after every Update and LateUpdate, whatever order the
        // game's own window dragging runs in, so the snapped position is always the one on screen.
        static int lastFrame = -1;
        static void BeforeRender()
        {
            int f = Time.frameCount;
            if (f == lastFrame) return;
            lastFrame = f;
            try { Snap.Frame(); }
            catch (Exception e)
            {
                Snap.Abort();
                if (!lateWarned) { lateWarned = true; Debug.LogWarning(Tag + "snapping failed: " + e); }
            }
        }
    }

    public sealed class Ticker : MonoBehaviour
    {
        void Update() { WorkspacesMod.Tick(); }
    }
}
