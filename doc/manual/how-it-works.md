# How it works

## Snapping

The game moves a dragged window from its own input loop: each move sets the window to where the drag started plus how far the pointer has travelled since. The mod snaps from `Canvas.willRenderCanvases`, which Unity calls after all of the frame's game logic and right before drawing, so the snapped position is the one on screen.

Because the game recomputes the position from the drag origin, snapping never accumulates: the pointer's free position is always known, and pulling away from an edge releases the window exactly under the pointer. An edge catches within 6 UI pixels and lets go beyond 10, so a snap holds steady without feeling glued.

Nothing runs while the mouse button is up. On a press, the visible windows' positions are noted; the first one that moves while the button is held is the dragged window. Edges are gathered once at that point, so each frame of a drag costs a few comparisons per window and allocates nothing.

The grid is the game's own inventory cell sprite (`Window/machinery`, `cell`), tiled across the screen under an opacity filter, one tile per cell at the window's scale. Grid snapping uses the same cell. The guide lines are the game's white sprite under a colour and opacity filter. A texture pack that restyles these sprites restyles the grid and guides with them.

## Finding windows

The game's own list of window roots drops a window as soon as it gets a parent item, so it never holds container windows. The mod keeps its own list from `PixelWindow.Show`, `Hide` and `Destroy`, which costs nothing between those events, and checks `IsVisible` when it reads it. Performance Fixes answers `IsVisible` for windows it hid by switching off their canvas, so a hidden window is never a snap target or part of a workspace.

## Switching

Windows open through the game's `ItemMouseDoubleClickHandler.OpenContentAction`, which shows a container's window and then places it from `PlayerStore.contentWindowPositions`. The mod writes the workspace's positions there first and lets the game do the rest, then restores the tab selection with `ApplyContentTabsSelection`. Windows close with `GameItem.CloseContentWindow`. Anything else that hooks those calls, such as Performance Fixes' per-window canvases and fast hiding, applies as usual.

Opening a full storage window is real work for Unity, so a switch is queued and run over several frames: each frame runs queued steps until 3 ms are spent, always at least one, closing first.

## Storage

Workspaces are stored per save slot (`PlayerStore.saveSlotId`) in `PlayerPrefs`, by item id, which is stable within a save. The mod reads the store from its static field rather than `PlayerStore.Instance`, which would create a store at the main menu.

## The manager and the notice

The manager is a window of the game's `CustomUIManager`, laid out like the storage-preset manager, with the game's own item chips. The switch notice is a machine `PixelWindow` with the game's radio buttons. Both are drawn entirely with the game's assets.
