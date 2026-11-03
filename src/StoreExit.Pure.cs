namespace WindowWorkspaces
{
    // When the store has been left: the decision behind StoreRelease, kept apart from Unity so tests/StoreExitTest.cs
    // can run it headless.
    public static class StoreExit
    {
        public const string StoreScene = "InventoryScene";

        // Follows scene loads. `inStore` remembers whether the store is up; returns true once, for the first scene
        // that replaces it. Additive loads change nothing, and a scene loaded while the store was not up (the main
        // menu at start, say) is not a departure.
        public static bool Left(ref bool inStore, string scene, bool single)
        {
            if (!single) return false;
            if (scene == StoreScene) { inStore = true; return false; }
            if (!inStore) return false;
            inStore = false;
            return true;
        }
    }
}
