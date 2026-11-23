using System.Collections.Generic;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Animated Road Map, a full-screen overlay in the Home scene. A vertical ScrollRect over map tiles (City, Snow, Beach
    /// repeating, 10 levels each, for every level in LevelPlan); only tiles near the view are alive (pooled with their path dots,
    /// nodes, chest, mist and world banner).
    ///
    /// Draw order inside the content, back to front: tiles, seam mist, path dots (white dashes + the gold progress), sequence fx,
    /// seam banner (above the dashes, so they never cross its text), nodes, chests, current-node marker. The card, skip catcher
    /// and top bar sit above the scroll view.
    ///
    /// Tapping an unlocked node opens the <see cref="LevelStartCard"/>; locked nodes shake and toast. Opening right after a win plays
    /// the ~2.3 s level-complete sequence: the camera scrolls to the completed node, it pops with a star burst and a white flash, the
    /// path to the next node fills from grey to gold dashes with a glowing dot, the next node's lock shakes, cracks, its halves fly off,
    /// it pops open to gold with a ring, then it becomes the current node. A tap skips it.
    /// </summary>
    public sealed class RoadMapScreen : MonoBehaviour
    {
        /// <summary>The open map (null when closed / not in the scene).</summary>
        public static RoadMapScreen Instance { get; private set; }

        /// <summary>Set before going to Home after a win: Home opens the map and plays this level's unlock.</summary>
        public static int PendingUnlockLevel;

        public static readonly string[] WorldNames = { "City", "Snow", "Beach" };

        [SerializeField] private TextAsset roadJson;
        [SerializeField] private Sprite[] tileSprites = new Sprite[3];

        [Header("Scroll")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [Tooltip("Back to front: tiles, mist, road (path dots), fx (sequence), banner, nodes, chests, marker (current node).")]
        [SerializeField] private RectTransform tilesLayer, mistLayer, bannerLayer, roadLayer, fxLayer, nodesLayer, chestLayer, markerLayer;
        [Tooltip("Where the focus level sits, as a fraction of the view height from the bottom.")]
        [SerializeField, Range(0f, 1f)] private float focusFraction = 0.38f;

        [Header("Templates (inactive)")]
        [SerializeField] private Image tileTemplate;
        [SerializeField] private UiPolyline roadTemplate;
        [SerializeField] private UiPolyline goldRoadTemplate;
        [SerializeField] private MapNodeView nodeTemplate;
        [SerializeField] private Button chestTemplate;
        [SerializeField] private MapSeamView seamTemplate;

        [Header("Level-complete sequence")]
        [SerializeField] private UiPolyline greyPath;
        [SerializeField] private UiPolyline goldGlow;
        [SerializeField] private UiPolyline goldPath;
        [SerializeField] private RectTransform dot;
        [SerializeField] private Image dotImage;
        [SerializeField] private Button skipCatcher;

        [Header("Level card")]
        [SerializeField] private LevelStartCard levelCard;

        [Header("Top bar")]
        [SerializeField] private Button homeButton;
        [SerializeField] private TMP_Text worldNumberText;
        [SerializeField] private TMP_Text worldNameText;
        [Tooltip("Coin icon of the map's coin pill (target of coin flies).")]
        [SerializeField] private RectTransform coinTarget;

        [Header("Reward screens (Home scene, wired by the builder)")]
        [SerializeField] private GiftChestScreen giftChest;
        [SerializeField] private NewWorldScreen newWorld;

        public float Clock { get; private set; }
        public LevelStartCard Card => levelCard;
        /// <summary>True while the level-complete sequence plays.</summary>
        public bool SequenceActive => seqLevel > 0;
        /// <summary>Seconds into the sequence (0..MapSequence.End), or -1 when none is playing.</summary>
        public float SequenceTime => seqLevel > 0 ? seqTime : -1f;
        /// <summary>Fires at the end of a sequence (also when skipped), with the level that was completed.</summary>
        public event System.Action<int> SequenceFinished;

        private RoadMapGeometry geo;
        private int levelCount, current;
        private readonly Dictionary<int, TileView> tiles = new Dictionary<int, TileView>();
        private readonly Stack<TileView> pool = new Stack<TileView>();
        private readonly List<TileView> drop = new List<TileView>(4);
        private int pendingWorld; // world whose banner/intro waits for its New World screen
        private Coroutine rewards;

        // sequence state
        private int seqLevel;
        private float seqTime, seqScrollFrom, seqScrollTo, seqArc, arcA, arcB;
        private MapNodeView seqA, seqB;
        private static AnimationCurve easeOut, easeInOut, dotScale;

        // A pin's box around its tip (the node position): 72 wide, from 8 px below the lip to the top of its crown, 88 px up.
        private const float NodeHalfW = 36f, NodeBottom = -8f, NodeTop = 88f;
        private const float BannerHalfW = MapSeamView.BannerWidth * 0.5f + 4f, BannerHalfH = MapSeamView.BannerHeight * 0.5f + 4f;

        private sealed class TileView
        {
            public int index;
            public Image image;
            public UiPolyline road;
            public UiPolyline goldRoad;
            public readonly MapNodeView[] nodes = new MapNodeView[RoadMapGeometry.LevelsPerTile];
            public Button chest;
            public MapSeamView mist;
            public MapSeamView banner;
        }

        // ------------------------------------------------------------------ open / close

        private void Awake()
        {
            homeButton.onClick.AddListener(Close);
            skipCatcher.onClick.AddListener(Skip);
            skipCatcher.gameObject.SetActive(false);
            if (easeOut == null)
            {
                Vector4 eo = new Vector4(0f, 0f, 0.58f, 1f);
                easeOut = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, eo);
                easeInOut = CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, CssCurve.EaseInOut);
                dotScale = CssCurve.Build(new[] { 0f, 0.5f, 1f }, new[] { 0.8f, 1.3f, 0.8f }, CssCurve.EaseInOut);
            }
        }

        private void OnEnable()
        {
            Instance = this;
            Localizer.Changed += OnLanguageChanged;
        }

        private void OnDisable()
        {
            Localizer.Changed -= OnLanguageChanged;
            if (Instance == this) Instance = null;
            ResetInteraction(); // closed or interrupted (scene / screen change): nothing may stay half-way
        }

        /// <summary>
        /// Always leaves the map interactive: no sequence running, tap-to-skip catcher and sequence fx off, card closed and reset.
        /// Called when the map opens, closes, is disabled and when a sequence ends, so the Play node works even after an interruption.
        /// </summary>
        private void ResetInteraction()
        {
            seqLevel = 0;
            seqA = seqB = null;
            if (skipCatcher != null) skipCatcher.gameObject.SetActive(false);
            if (greyPath != null) greyPath.gameObject.SetActive(false);
            if (goldGlow != null) goldGlow.gameObject.SetActive(false);
            if (goldPath != null) goldPath.gameObject.SetActive(false);
            if (dot != null) dot.gameObject.SetActive(false);
            if (levelCard != null) levelCard.HideImmediate();
        }

        /// <summary>Live language change: the top bar and every visible tile (world banners) re-read their strings.</summary>
        private void OnLanguageChanged()
        {
            if (geo == null || !gameObject.activeInHierarchy) return;
            int world = RoadMapGeometry.TileOf(current);
            worldNumberText.text = Localizer.F("WORLD {0}", world + 1);
            worldNameText.text = Localizer.T(WorldNames[world % WorldNames.Length]);
            RefreshTiles();
        }

        /// <summary>Opens the map; unlockedLevel > 0 plays that level's unlock sequence once (if it has not played yet).</summary>
        public void Open(int unlockedLevel = 0)
        {
            geo ??= new RoadMapGeometry(roadJson.text);
            levelCount = LevelPlan.PlannedLevels;
            current = Mathf.Clamp(SaveSystem.Data.level, 1, levelCount);
            gameObject.SetActive(true);
            Instance = this;
            ResetInteraction();

            content.sizeDelta = new Vector2(content.sizeDelta.x, RoadMapGeometry.TileCount(levelCount) * RoadMapGeometry.TileHeight);
            foreach (TileView tv in new List<TileView>(tiles.Values)) Release(tv);

            Clock = 0f;
            bool play = unlockedLevel > 0 && unlockedLevel < levelCount && unlockedLevel > SaveSystem.Data.mapUnlockShown && unlockedLevel + 1 == current;
            if (play)
            {
                seqLevel = unlockedLevel;
                seqTime = 0f;
                SaveSystem.Data.mapUnlockShown = unlockedLevel; // never replays, even if the map is closed mid-way
                SaveSystem.Commit();
                arcA = geo.LevelArc(unlockedLevel);
                arcB = geo.LevelArc(unlockedLevel + 1);
                seqArc = arcA;
                List<Vector2> path = geo.ContentPath(arcA, arcB);
                SetSequenceLine(greyPath, path, 1f);
                SetSequenceLine(goldGlow, path, 0f);
                SetSequenceLine(goldPath, path, 0f);
                dot.gameObject.SetActive(false);
                skipCatcher.gameObject.SetActive(true);
            }
            else
            {
                greyPath.gameObject.SetActive(false);
                goldGlow.gameObject.SetActive(false);
                goldPath.gameObject.SetActive(false);
                dot.gameObject.SetActive(false);
                skipCatcher.gameObject.SetActive(false);
            }

            if (rewards != null) StopCoroutine(rewards);
            rewards = null;
            pendingWorld = play && MapRewards.WorldDue(SaveSystem.Data, unlockedLevel + 1) ? MapRewards.WorldOf(unlockedLevel + 1) : 0;
            Canvas.ForceUpdateCanvases();
            if (play)
            {
                float nodeY = geo.ContentAt(arcA, out _).y;
                seqScrollTo = ClearOfTopBar(TargetScrollFor(nodeY));
                seqScrollFrom = TargetScrollFor(nodeY - 240f); // the camera starts below and scrolls up to the completed node
                SetScrollY(seqScrollFrom);
            }
            else
            {
                SetScrollY(ClearOfTopBar(TargetScrollFor(geo.ContentAt(geo.LevelArc(current), out _).y)));
            }

            int world = RoadMapGeometry.TileOf(current);
            worldNumberText.text = Localizer.F("WORLD {0}", world + 1);
            worldNameText.text = Localizer.T(WorldNames[world % WorldNames.Length]);
            UpdateTiles();
            ApplyGoldProgress();
        }

        private static void SetSequenceLine(UiPolyline line, List<Vector2> path, float fill)
        {
            line.SetPoints(path);
            line.Fill = fill;
            line.gameObject.SetActive(true);
        }

        public void Close()
        {
            ResetInteraction();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            float dt = Time.deltaTime;
            Clock += dt;
            if (seqLevel > 0) RunSequence(dt);

            UpdateTiles();
            foreach (TileView tv in tiles.Values)
            {
                foreach (MapNodeView n in tv.nodes)
                {
                    if (n != null && n.gameObject.activeSelf) n.Tick(Clock);
                }
            }
        }

        // ------------------------------------------------------------------ level-complete sequence

        private void RunSequence(float dt)
        {
            seqTime += dt;
            float t = seqTime;

            // 1. camera scrolls to the completed node
            float ck = Mathf.Clamp01(t / MapSequence.Scroll);
            SetScrollY(Mathf.Lerp(seqScrollFrom, seqScrollTo, easeOut.Evaluate(ck)));

            // 2. completed node: pop + star burst + white flash
            if (seqA != null) seqA.SeqCompleted(t);

            // 3. path fills grey -> gold with a glowing dot travelling along it
            float pk = Mathf.Clamp01((t - MapSequence.PathStart) / (MapSequence.PathEnd - MapSequence.PathStart));
            float fill = easeInOut.Evaluate(pk);
            goldGlow.Fill = fill;
            goldPath.Fill = fill;
            seqArc = Mathf.Lerp(arcA, arcB, fill);
            bool dotOn = t >= MapSequence.PathStart && t < MapSequence.PathEnd + 0.1f;
            dot.gameObject.SetActive(dotOn);
            if (dotOn)
            {
                dot.anchoredPosition = geo.ContentAt(seqArc, out _);
                dot.localScale = Vector3.one * dotScale.Evaluate(pk);
                Color c = dotImage.color;
                c.a = t < MapSequence.PathEnd ? 0.95f : Mathf.Clamp01(1f - (t - MapSequence.PathEnd) / 0.1f);
                dotImage.color = c;
            }

            // 4. next node: lock shakes, cracks, halves fly off, pops open to gold with a ring
            if (seqB != null) seqB.SeqNext(t);

            ApplyGoldProgress();
            if (t >= MapSequence.End) FinishSequence();
        }

        /// <summary>Tap anywhere: jump to the end state.</summary>
        public void Skip()
        {
            if (seqLevel > 0) seqTime = MapSequence.End;
        }

        private void FinishSequence()
        {
            int won = seqLevel;
            ResetInteraction(); // interaction enabled, overlays off, card reset: the new current node must be playable
            SetScrollY(seqScrollTo);

            RefreshTiles(); // node N+1 is now the current node (red Play disc in the marker layer)
            if (rewards != null) StopCoroutine(rewards);
            rewards = StartCoroutine(AfterUnlock(won));
            SequenceFinished?.Invoke(won);
        }

        /// <summary>The persistent gold dashes: from the first tile up to the current node (or the sequence's travelling front).</summary>
        private void ApplyGoldProgress()
        {
            float arc = seqLevel > 0 ? seqArc : (current > 1 ? geo.LevelArc(current) : 0f);
            foreach (TileView tv in tiles.Values) tv.goldRoad.Fill = Mathf.Clamp01((arc - tv.index * geo.TileArc) / geo.TileArc);
        }

        // ------------------------------------------------------------------ scrolling

        private float ViewHeight => scroll.viewport != null ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;

        /// <summary>content.anchoredPosition.y that shows content y at focusFraction of the view (content is bottom-anchored).</summary>
        private float TargetScrollFor(float contentY)
        {
            float max = Mathf.Max(0f, content.rect.height - ViewHeight);
            return -Mathf.Clamp(contentY - focusFraction * ViewHeight, 0f, max);
        }

        /// <summary>Top bar height (design px) plus the 80 px a chest label must stay clear of it.</summary>
        private const float TopBarClear = 92f + 80f;

        /// <summary>
        /// Adjusts the opening scroll so no chest (and its "Gift" tag) sits under the top bar or within 80 px of it: it scrolls the map
        /// so the chest moves down into the clear area, or - if that would push the focus node too low - up past the bar.
        /// </summary>
        private float ClearOfTopBar(float scrollY)
        {
            float h = ViewHeight;
            float minY = -(content.rect.height - h);
            float nodeY = geo.ContentAt(geo.LevelArc(current), out _).y;
            int tileCount = RoadMapGeometry.TileCount(levelCount);
            for (int tile = 0; tile < tileCount; tile++)
            {
                int chestLevel = tile * RoadMapGeometry.LevelsPerTile + 5;
                if (chestLevel > levelCount) break;
                float top = geo.NodeContent(chestLevel).y + 40f + scrollY; // chest column: 66 px tall below its top
                float bottom = top - 66f;
                if (bottom > h + 4f || top < 0f) continue;                  // not on screen at all
                if (top <= h - TopBarClear) continue;                       // already clear of the bar
                float down = top - (h - TopBarClear);
                float lowered = Mathf.Max(scrollY - down, minY);
                if (nodeY + lowered >= 0.15f * h)
                {
                    scrollY = lowered;                                       // chest moves down, the node stays comfortably on screen
                }
                else
                {
                    float up = h + 6f - bottom;                              // otherwise push the chest up and out above the bar
                    scrollY = Mathf.Min(scrollY + up, 0f);
                }
            }
            return scrollY;
        }

        private void SetScrollY(float y)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
        }

        // ------------------------------------------------------------------ pooling

        private void UpdateTiles()
        {
            float bottom = -content.anchoredPosition.y - 200f;
            float top = -content.anchoredPosition.y + ViewHeight + 200f;
            int tileCount = RoadMapGeometry.TileCount(levelCount);
            int first = Mathf.Max(0, Mathf.FloorToInt(bottom / RoadMapGeometry.TileHeight));
            int last = Mathf.Min(tileCount - 1, Mathf.FloorToInt(top / RoadMapGeometry.TileHeight));

            bool changed = false;
            drop.Clear();
            foreach (TileView tv in tiles.Values)
            {
                if (tv.index < first || tv.index > last) drop.Add(tv);
            }
            foreach (TileView tv in drop)
            {
                Release(tv);
                changed = true;
            }
            for (int i = first; i <= last; i++)
            {
                if (tiles.ContainsKey(i)) continue;
                tiles[i] = Acquire(i);
                changed = true;
            }
            if (changed) ApplyGoldProgress();
        }

        private TileView Acquire(int index)
        {
            TileView tv = pool.Count > 0 ? pool.Pop() : CreateView();
            tv.index = index;
            float y0 = index * RoadMapGeometry.TileHeight;

            tv.image.sprite = tileSprites[index % tileSprites.Length];
            tv.image.rectTransform.anchoredPosition = new Vector2(RoadMapGeometry.TileLeft, y0);
            tv.road.rectTransform.anchoredPosition = new Vector2(0f, y0);
            tv.goldRoad.rectTransform.anchoredPosition = new Vector2(0f, y0);
            tv.image.gameObject.SetActive(true);
            tv.road.gameObject.SetActive(true);
            tv.goldRoad.gameObject.SetActive(true);

            for (int i = 0; i < tv.nodes.Length; i++)
            {
                int level = index * RoadMapGeometry.LevelsPerTile + i + 1;
                MapNodeView n = tv.nodes[i];
                bool exists = level <= levelCount;
                n.gameObject.SetActive(exists);
                if (!exists) continue;
                MapNodeState state = level < current ? MapNodeState.Completed : level == current ? MapNodeState.Current : MapNodeState.Locked;
                if (seqLevel > 0 && level == seqLevel + 1) state = MapNodeState.Locked; // opens during the sequence
                Transform wantParent = state == MapNodeState.Current ? markerLayer : nodesLayer;
                if (n.transform.parent != wantParent) n.transform.SetParent(wantParent, false);
                Vector2 p = geo.NodeContent(level);
                ((RectTransform)n.transform).anchoredPosition = p + new Vector2(0f, 3f);
                n.Setup(level, state, SaveSystem.Data.GetStars(level), level % 10 == 0);
                if (seqLevel > 0 && level == seqLevel) seqA = n;
                if (seqLevel > 0 && level == seqLevel + 1) seqB = n;
            }

            int chestLevel = index * RoadMapGeometry.LevelsPerTile + 5;
            bool chest = chestLevel <= levelCount;
            tv.chest.gameObject.SetActive(chest);
            if (chest)
            {
                Vector2 p = geo.NodeContent(chestLevel);
                ((RectTransform)tv.chest.transform).anchoredPosition = p + new Vector2(-104f, 40f);
                tv.chest.GetComponent<MapChestView>().SetState(MapRewards.StateOf(SaveSystem.Data, chestLevel));
                tv.chest.onClick.RemoveAllListeners();
                tv.chest.onClick.AddListener(() => OnChestTapped(chestLevel));
            }

            bool seam = index > 0;
            tv.mist.gameObject.SetActive(seam);
            tv.banner.gameObject.SetActive(seam);
            if (seam)
            {
                ((RectTransform)tv.mist.transform).anchoredPosition = new Vector2(0f, y0);
                tv.mist.SetupMist();

                int firstLevel = index * RoadMapGeometry.LevelsPerTile + 1;
                string name = WorldNames[index % WorldNames.Length];
                bool locked = firstLevel > current || pendingWorld == index + 1;
                tv.banner.Setup(index + 1, name, firstLevel, locked);
                Vector2 bc = BannerCentre(index);
                ((RectTransform)tv.banner.transform).anchoredPosition = new Vector2(bc.x - MapSeamView.BannerCentreX, bc.y);
                tv.banner.Button.onClick.RemoveAllListeners();
                tv.banner.Button.onClick.AddListener(() =>
                {
                    // Unlocked: preview the New World screen; locked: just its name.
                    if (firstLevel <= current && pendingWorld != index + 1 && newWorld != null) newWorld.Show(index + 1, null);
                    else Toast.Show(Localizer.F("World {0}: {1}", index + 1, Localizer.T(name)));
                });
            }
            return tv;
        }

        /// <summary>
        /// The banner sits centred on the seam, on the road. If a pin would be under it, it slides along the road (nearest first, both ways)
        /// to the closest spot where no pin box touches the banner box; if there is no such spot it takes the one with the least overlap.
        /// </summary>
        public Vector2 BannerCentre(int seamIndex)
        {
            geo ??= new RoadMapGeometry(roadJson.text);
            float seamArc = seamIndex * geo.TileArc;
            int firstLevel = seamIndex * RoadMapGeometry.LevelsPerTile + 1;
            Vector2 best = geo.ContentAt(seamArc, out _);
            float bestOverlap = BannerOverlapAt(best, firstLevel);
            for (int step = 1; step <= 40 && bestOverlap > 0f; step++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    Vector2 p = geo.ContentAt(seamArc + sign * step * 4f, out _);
                    float o = BannerOverlapAt(p, firstLevel);
                    if (o < bestOverlap)
                    {
                        best = p;
                        bestOverlap = o;
                    }
                }
            }
            return best;
        }

        /// <summary>How far the banner box at <paramref name="p"/> sinks into the pin boxes next to the seam (0 = clear).</summary>
        private float BannerOverlapAt(Vector2 p, int firstLevel)
        {
            float total = 0f;
            for (int level = firstLevel - 2; level <= firstLevel + 1; level++)
            {
                if (level < 1 || level > levelCount) continue;
                Vector2 n = geo.NodeContent(level) + new Vector2(0f, 3f);
                float ox = NodeHalfW + BannerHalfW - Mathf.Abs(p.x - n.x);
                if (ox <= 0f) continue;
                float oy = Mathf.Min(p.y + BannerHalfH, n.y + NodeTop) - Mathf.Max(p.y - BannerHalfH, n.y + NodeBottom);
                if (oy <= 0f) continue;
                total += Mathf.Min(ox, oy);
            }
            return total;
        }

        private void Release(TileView tv)
        {
            tiles.Remove(tv.index);
            tv.image.gameObject.SetActive(false);
            tv.road.gameObject.SetActive(false);
            tv.goldRoad.gameObject.SetActive(false);
            foreach (MapNodeView n in tv.nodes)
            {
                if (n == seqA) seqA = null;
                if (n == seqB) seqB = null;
                n.gameObject.SetActive(false);
                if (n.transform.parent != nodesLayer) n.transform.SetParent(nodesLayer, false);
            }
            tv.chest.gameObject.SetActive(false);
            tv.mist.gameObject.SetActive(false);
            tv.banner.gameObject.SetActive(false);
            pool.Push(tv);
        }

        private TileView CreateView()
        {
            var tv = new TileView
            {
                image = Instantiate(tileTemplate, tilesLayer),
                mist = Instantiate(seamTemplate, mistLayer),
                banner = Instantiate(seamTemplate, bannerLayer),
                road = Instantiate(roadTemplate, roadLayer),
                goldRoad = Instantiate(goldRoadTemplate, roadLayer),
                chest = Instantiate(chestTemplate, chestLayer)
            };
            tv.road.SetPoints(geo.Points);
            tv.goldRoad.SetPoints(geo.Points);
            for (int i = 0; i < tv.nodes.Length; i++)
            {
                tv.nodes[i] = Instantiate(nodeTemplate, nodesLayer);
                tv.nodes[i].Tapped += OnNodeTapped;
            }
            return tv;
        }

        // ------------------------------------------------------------------ rewards after an unlock

        /// <summary>After the unlock of <paramref name="won"/>: its gift chest (bounce, then the screen), then a new world.</summary>
        private System.Collections.IEnumerator AfterUnlock(int won)
        {
            SaveData d = SaveSystem.Data;
            if (giftChest != null && MapRewards.ChestDue(d, won))
            {
                if (tiles.TryGetValue(RoadMapGeometry.TileOf(won), out TileView tv) && tv.chest.gameObject.activeSelf)
                {
                    yield return tv.chest.GetComponent<MapChestView>().Bounce3();
                }
                bool done = false;
                giftChest.Show(won, coinTarget, () => done = true);
                while (!done) yield return null;
                RefreshTiles();
            }
            if (newWorld != null && MapRewards.WorldDue(d, won + 1))
            {
                int world = MapRewards.WorldOf(won + 1);
                MapRewards.MarkWorldShown(d, world);
                SaveSystem.Commit();
                bool done = false;
                newWorld.Show(world, () => done = true);
                while (!done) yield return null;
            }
            pendingWorld = 0;
            RefreshTiles();
            rewards = null;
        }

        /// <summary>Rebuilds the visible tiles (chest / banner / node state changed).</summary>
        private void RefreshTiles()
        {
            foreach (TileView tv in new List<TileView>(tiles.Values)) Release(tv);
            UpdateTiles();
        }

        // ------------------------------------------------------------------ taps

        /// <summary>State comes from the save each time: LOCKED toast, READY opens the gift screen (pays out), CLAIMED does nothing.</summary>
        public void OnChestTapped(int chestLevel)
        {
            switch (MapRewards.StateOf(SaveSystem.Data, chestLevel))
            {
                case MapRewards.ChestState.Locked:
                    Toast.Show(Localizer.F("Gift after level {0}", chestLevel));
                    break;
                case MapRewards.ChestState.Ready:
                    if (giftChest == null || rewards != null) break; // the unlock sequence is already handling the chest
                    giftChest.Show(chestLevel, coinTarget, RefreshTiles);
                    break;
            }
        }

        /// <summary>The live node view of a level (null when its tile is not on screen).</summary>
        public MapNodeView NodeView(int level)
        {
            if (!tiles.TryGetValue(RoadMapGeometry.TileOf(level), out TileView tv)) return null;
            MapNodeView n = tv.nodes[(level - 1) % RoadMapGeometry.LevelsPerTile];
            return n != null && n.gameObject.activeSelf ? n : null;
        }

        /// <summary>Taps the node of a level exactly like a finger would (tests, tools). False when it is not on screen.</summary>
        public bool TapLevel(int level)
        {
            MapNodeView n = NodeView(level);
            if (n == null) return false;
            OnNodeTapped(n);
            return true;
        }

        private void OnNodeTapped(MapNodeView node)
        {
            if (seqLevel > 0) return;
            if (levelCard != null && levelCard.IsOpen)
            {
                if (levelCard.isActiveAndEnabled) return;   // really open on screen
                levelCard.HideImmediate();                  // stale "open" state of a card that is not on screen: never blocks a tap
            }
            if (node.State == MapNodeState.Locked)
            {
                node.Shake();
                Toast.Show(Localizer.F("Level {0} is locked", node.Level));
                return;
            }
            OpenCard(node);
        }

        /// <summary>Level Start card for an unlocked node (current = playable, completed = shown with its stars only).</summary>
        private void OpenCard(MapNodeView node)
        {
            int level = node.Level;
            int world = RoadMapGeometry.TileOf(level);
            bool playable = node.State == MapNodeState.Current;
            bool intro = playable && MapRewards.IntroDue(SaveSystem.Data, level);
            Sprite art = tileSprites[world % tileSprites.Length];
            levelCard.Show((RectTransform)node.transform, level, world, WorldNames[world % WorldNames.Length], SaveSystem.Data.GetStars(level),
                level % 10 == 0, GameSession.CoinsFor(3), intro, playable, art, () => StartLevel(level, intro));
        }

        private static void StartLevel(int level, bool intro)
        {
            if (intro)
            {
                MapRewards.MarkIntroShown(SaveSystem.Data, level);
                SaveSystem.Commit();
            }
            SceneLoader.LoadGame();
        }
    }
}
