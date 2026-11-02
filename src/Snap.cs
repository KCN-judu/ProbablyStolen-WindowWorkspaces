using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WindowWorkspaces
{
    // Snaps a dragged window to the edges of other windows, the screen edges and a faint grid.
    //
    // The game moves a dragged window from its own input loop: each move sets the position to where the drag
    // started plus how far the pointer has travelled since. This runs from Canvas.willRenderCanvases, after all of
    // the frame's game logic and right before drawing, so the snapped position is the one on screen. Because the
    // game recomputes from the drag origin, snapping never accumulates: the pointer's free position is always
    // known, and pulling away from an edge releases the window exactly under the pointer.
    //
    // The grid is the game's own inventory cell (Window/machinery, cell) tiled across the screen under an
    // opacity filter, one cell per tile at the window's scale, and grid snapping uses that same cell, so windows
    // line up with the item grid. Guides are the game's white sprite under a colour and opacity filter. Both
    // follow a texture pack that restyles them.
    //
    // Nothing runs while the mouse button is up. On press, the visible windows' positions are noted; the first one
    // that moves while the button is held is the dragged window. Edges are gathered once at that point, so each
    // frame of a drag is a few comparisons per window and no allocation.
    //
    // Feel: an edge catches within 6 UI pixels and lets go beyond 10, so a snap holds steady but never feels glued.
    // Edge snapping wins over the grid; holding Alt drags freely.
    public static class Snap
    {
        public static bool Enabled = true, GridSnap = true, GridShown = true;

        const float Catch = 6f, Release = 10f, FadeTime = 0.12f, GridAlpha = 0.12f, FallbackCell = 8f;
        static readonly Color GuideColor = new Color(0.30f, 0.76f, 0.71f, 0.9f);


        static bool pressed;
        static readonly List<PixelWindow> watched = new List<PixelWindow>();
        static readonly List<Vector2> watchedAt = new List<Vector2>();
        static readonly List<Box> targets = new List<Box>();
        static PixelWindow dragged;
        static RectTransform rt;
        static Vector2 lastSet, lastRaw;
        static float stuckX = float.NaN, stuckY = float.NaN;
        static readonly Vector3[] corners = new Vector3[4];

        // Overlay: one tiled grid image and two guide bars on their own canvases. The grid canvas never changes
        // while shown, so it costs one batch and no rebuild per frame.
        static GameObject gridRoot, guideRoot;
        static Canvas gridCanvas, guideCanvas;
        static CanvasGroup gridGroup;
        static Image grid;
        static Sprite cellSprite;
        static bool cellLoaded;
        static float cellPx;
        static RectTransform guideV, guideH;
        static float fade, fadeTarget;

        static bool announced;

        internal static void Frame()
        {
            if (Input.GetMouseButtonDown(0)) Press();
            if (pressed && !Input.GetMouseButton(0)) { pressed = false; EndDrag(); }
            if (pressed)
            {
                if (dragged == null) Detect();
                if (dragged != null) Drag();
            }
            if (fade != fadeTarget) Fade();
        }

        // Notes where every visible window is, to tell which one the game starts moving.
        static void Press()
        {
            pressed = true;
            dragged = null;
            watched.Clear(); watchedAt.Clear();
            if (!Enabled) return;
            Windows.Visible(watched);
            for (int i = 0; i < watched.Count; i++) watchedAt.Add(watched[i].rectTransform.anchoredPosition);
            if (presses < 3) { presses++; Debug.Log(WorkspacesMod.Tag + "press: " + watched.Count + " visible window(s)"); }
        }

        static int presses;

        static void Detect()
        {
            for (int i = 0; i < watched.Count; i++)
            {
                var w = watched[i];
                if (w.rectTransform == null || w.rectTransform.anchoredPosition == watchedAt[i]) continue;
                BeginDrag(w);
                return;
            }
        }

        static void BeginDrag(PixelWindow w)
        {
            if (!announced) { announced = true; Debug.Log(WorkspacesMod.Tag + "snapping a dragged window (first this session)"); }
            dragged = w;
            rt = w.rectTransform;
            lastSet = lastRaw = rt.anchoredPosition;
            stuckX = stuckY = float.NaN;
            targets.Clear();
            for (int i = 0; i < watched.Count; i++)
            {
                var o = watched[i];
                if (o == w || o.rectTransform == null || !o.IsVisible()) continue;
                targets.Add(BoxOf(o.rectTransform));
            }
            // Screen edges, as a box the window sits inside.
            targets.Add(new Box { L = 0f, R = Screen.width, B = 0f, T = Screen.height });
            ShowOverlay();
        }

        static Box BoxOf(RectTransform t)
        {
            t.GetWorldCorners(corners);
            return new Box { L = corners[0].x, B = corners[0].y, R = corners[2].x, T = corners[1].y };
        }

        static void Drag()
        {
            if (rt == null || !dragged.IsVisible()) { EndDrag(); return; }
            var now = rt.anchoredPosition;
            // The game set a new free position this frame, or the pointer did not move and ours still stands.
            var raw = now != lastSet ? now : lastRaw;
            lastRaw = raw;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                if (now != raw) rt.anchoredPosition = raw;
                lastSet = raw;
                stuckX = stuckY = float.NaN;
                Guides(false, 0f, false, 0f, 1f);
                return;
            }

            if (now != raw) rt.anchoredPosition = raw;
            var me = BoxOf(rt);
            float s = Mathf.Max(0.01f, rt.lossyScale.x);
            float dx, dy, lineX, lineY;
            bool hitX = SnapRule.Best(me, targets, true, stuckX, Catch * s, Release * s, out dx, out lineX);
            bool hitY = SnapRule.Best(me, targets, false, stuckY, Catch * s, Release * s, out dy, out lineY);
            stuckX = hitX ? lineX : float.NaN;
            stuckY = hitY ? lineY : float.NaN;
            if (GridSnap)
            {
                float cell = cellPx > 0f ? cellPx : FallbackCell * s;
                if (!hitX) dx = SnapRule.GridDelta(me.L, cell);
                if (!hitY) dy = SnapRule.GridDelta(me.T, cell);
            }

            if (dx != 0f || dy != 0f)
            {
                var p = rt.position;
                // Whole screen pixels, as the game itself leaves a dragged window.
                rt.position = new Vector3(Mathf.Round(p.x + dx), Mathf.Round(p.y + dy), p.z);
            }
            lastSet = rt.anchoredPosition;
            Guides(hitX, lineX, hitY, lineY, s);
        }

        static void EndDrag()
        {
            dragged = null;
            rt = null;
            watched.Clear(); watchedAt.Clear(); targets.Clear();
            fadeTarget = 0f;
            Guides(false, 0f, false, 0f, 1f);
        }

        internal static void Abort()
        {
            pressed = false;
            EndDrag();
            fade = 0f;
            if (gridRoot != null) gridRoot.SetActive(false);
        }

        // ---- Overlay ---------------------------------------------------------------------------------------------

        static void ShowOverlay()
        {
            EnsureOverlay();
            // The grid sits just under the window canvas and the guides just above it.
            var root = rt.GetComponentInParent<Canvas>();
            int order = root != null ? root.rootCanvas.sortingOrder : 0;
            gridCanvas.sortingOrder = order - 1;
            guideCanvas.sortingOrder = order + 1;

            LayoutGrid(Mathf.Max(0.01f, rt.lossyScale.x));
            grid.enabled = GridShown && grid.sprite != null;
            gridRoot.SetActive(true);
            guideRoot.SetActive(true);
            fadeTarget = 1f;
        }

        static void Fade()
        {
            float step = Time.unscaledDeltaTime / FadeTime;
            fade = Mathf.MoveTowards(fade, fadeTarget, step);
            if (gridGroup != null) gridGroup.alpha = fade;
            if (fade <= 0f && gridRoot != null) { gridRoot.SetActive(false); guideRoot.SetActive(false); }
        }

        static void Guides(bool showV, float x, bool showH, float y, float s)
        {
            if (guideV == null) return;
            float w = Mathf.Max(1f, Mathf.Round(s));
            guideV.gameObject.SetActive(showV);
            guideH.gameObject.SetActive(showH);
            if (showV) { guideV.anchoredPosition = new Vector2(x, 0f); guideV.sizeDelta = new Vector2(w, Screen.height); }
            if (showH) { guideH.anchoredPosition = new Vector2(0f, y); guideH.sizeDelta = new Vector2(Screen.width, w); }
        }

        static void EnsureOverlay()
        {
            if (gridRoot != null) return;
            gridRoot = NewCanvas("WindowWorkspaces.Grid", out gridCanvas);
            gridGroup = gridRoot.AddComponent<CanvasGroup>();
            gridGroup.blocksRaycasts = false;
            gridGroup.interactable = false;
            var g = new GameObject("Grid", typeof(RectTransform));
            g.transform.SetParent(gridRoot.transform, false);
            grid = g.AddComponent<Image>();
            grid.raycastTarget = false;
            grid.type = Image.Type.Tiled;
            grid.color = new Color(1f, 1f, 1f, GridAlpha);
            var gr = g.GetComponent<RectTransform>();
            gr.anchorMin = gr.anchorMax = gr.pivot = Vector2.zero;

            guideRoot = NewCanvas("WindowWorkspaces.Guides", out guideCanvas);
            guideV = GuideBar(guideRoot.transform, new Vector2(0.5f, 0f));
            guideH = GuideBar(guideRoot.transform, new Vector2(0f, 0.5f));
            gridRoot.SetActive(false);
            guideRoot.SetActive(false);
        }

        static GameObject NewCanvas(string name, out Canvas canvas)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            return go;
        }

        static RectTransform GuideBar(Transform parent, Vector2 pivot)
        {
            var go = new GameObject("Guide", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Line(go, GuideColor);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = pivot;
            go.SetActive(false);
            return r;
        }

        // An image of the game's white sprite, tinted.
        static void Line(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var rh = RenderHandler.current;
            if (rh != null && rh.white != null) img.sprite = rh.white;
            img.color = color;
        }

        // The tiled cell at one tile per inventory cell: a sprite texel is one UI unit in a window, so a tile is the
        // cell's width in texels times the window scale, in screen pixels. The image starts at the screen origin,
        // which is where grid snapping counts from.
        static void LayoutGrid(float s)
        {
            if (!cellLoaded)
            {
                cellLoaded = true;
                try { cellSprite = RenderHandler.LoadFromAtlas("Window/machinery", "cell"); } catch (System.Exception) { cellSprite = null; }
            }
            grid.sprite = cellSprite;
            cellPx = 0f;
            if (cellSprite == null) return;
            float texels = cellSprite.rect.width;
            cellPx = Mathf.Max(2f, Mathf.Round(texels * s));
            // A tile is rect / (pixelsPerUnit / referencePixelsPerUnit * multiplier) canvas units.
            grid.pixelsPerUnitMultiplier = texels * 100f / (cellPx * cellSprite.pixelsPerUnit);
            var r = grid.rectTransform;
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(Screen.width, Screen.height);
        }

        internal static void Destroy()
        {
            Abort();
            if (gridRoot != null) Object.Destroy(gridRoot);
            if (guideRoot != null) Object.Destroy(guideRoot);
            gridRoot = guideRoot = null; grid = null; guideV = guideH = null;
        }
    }
}
