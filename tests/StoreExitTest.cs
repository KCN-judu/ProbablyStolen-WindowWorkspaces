// Checks when the store counts as left (src/StoreExit.Pure.cs), outside the game.
//   csc -out:StoreExitTest.exe tests\StoreExitTest.cs src\StoreExit.Pure.cs && StoreExitTest.exe
using System;
using WindowWorkspaces;

static class StoreExitTest
{
    static int failed;

    static void Check(string what, bool ok)
    {
        if (!ok) failed++;
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + what);
    }

    static int Main()
    {
        bool inStore = false;
        Check("the main menu at start is not leaving the store", !StoreExit.Left(ref inStore, "EmporiumMenu", true) && !inStore);
        Check("loading the store is not leaving it", !StoreExit.Left(ref inStore, "InventoryScene", true) && inStore);
        Check("an additive scene over the store is not leaving it", !StoreExit.Left(ref inStore, "Overlay", false) && inStore);
        Check("reloading the store is not leaving it", !StoreExit.Left(ref inStore, "InventoryScene", true) && inStore);
        Check("the main menu after the store is leaving it", StoreExit.Left(ref inStore, "EmporiumMenu", true) && !inStore);
        Check("only once: another scene after that is not", !StoreExit.Left(ref inStore, "Credits", true));
        Check("back in the store and out again counts again",
            !StoreExit.Left(ref inStore, "InventoryScene", true) && StoreExit.Left(ref inStore, "EmporiumMenu", true));

        Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
