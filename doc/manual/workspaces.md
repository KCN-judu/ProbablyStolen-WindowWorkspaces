# Workspaces

## The manager

Press the key bound to **Workspace manager** to open the manager. It is built like the game's own storage-preset manager.

Each workspace has a row with:

- its name, which you can type over (an unnamed workspace shows as "Workspace 2" and so on);
- **Switch**, or **Current** for the workspace you are in;
- the number of windows it holds;
- a cross to delete it.

Below the row, its containers are shown as item chips. Click a chip to take that container out of the workspace; in the workspace you are in, its window closes too.

**New workspace** adds an empty workspace at the end, up to nine. Deleting the workspace you are in first moves you to its neighbour, so its windows close with it. The last workspace cannot be deleted.

## Switching

Press **Next workspace** to move to the next workspace, and Shift with it to go back. A small window near the top of the screen shows which workspace you are in, with one button per workspace; click one to switch to it.

A switch closes the windows the new workspace does not have and opens the ones it has. Windows open one after another within a few milliseconds per frame, so the game keeps moving while a large set opens.

## What is remembered

For every container: which item it is, where its window was and which tab it showed. Workspaces are kept per save slot, so each save has its own.

A container that is sold or thrown away stays in the list as "(gone)" and is skipped when you switch; click its chip to forget it.
