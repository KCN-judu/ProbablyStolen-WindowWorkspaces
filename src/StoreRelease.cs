using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WindowWorkspaces
{
    // Leaving the store (back to the main menu, say) lets go of everything this mod still pointed at in it.
    //
    // A store's items, inventories and windows all point at each other, so one of them kept from a static field keeps
    // the whole store in memory. The windows destroyed with the store's scene never go through PixelWindow.Destroy,
    // so without this they stayed in these fields until the next store replaced them:
    //   - the list of open windows (Windows): only destroyed windows are taken out;
    //   - the switch notice's window (Toast), which is built again on the next switch;
    //   - the manager's window (Manager), which closes; it remembers where it was, as on any close;
    //   - queued switch steps and their items, and the last list of visible windows (Workspaces);
    //   - the window being dragged and the windows noted on the last press (Snap).
    // Nothing changes inside the store: this runs only when another scene has replaced it.
    static class StoreRelease
    {
        static bool inStore;

        internal static void Patch()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        internal static void Unpatch() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!StoreExit.Left(ref inStore, scene.name, mode == LoadSceneMode.Single)) return;
            int windows = 0;
            Try(() => windows = Windows.Forget());
            Try(Toast.Forget);
            Try(Manager.Close);
            Try(Workspaces.Forget);
            Try(Snap.Abort);
            Debug.Log(WorkspacesMod.Tag + "left the store for '" + scene.name + "': released " + windows + " window(s)");
        }

        static void Try(Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogWarning(WorkspacesMod.Tag + "releasing the store failed: " + e.Message); }
        }
    }
}
