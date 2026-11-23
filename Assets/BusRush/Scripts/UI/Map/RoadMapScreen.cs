using System.Collections.Generic;
using BusRush.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Animated Road Map (RoadMap.dc.html), a full-screen overlay in the Home scene. A vertical ScrollRect over map
    /// tiles (City, Snow, Beach repeating, 10 levels each, for every level in LevelPlan); only tiles near the view are
    /// alive (pooled with their road, nodes, chest and world sign). Opening right after a win plays the unlock: trail,
    /// bus drive along the road, grey disc burst, green disc pop. Design px everywhere (canvas matches width 390).
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
        [SerializeField] private Sprite[] busFrames = new Sprite[0];

        [Header("Scroll")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform tilesLayer, roadLayer, nodesLayer, chestLayer, seamLayer, fxLayer;
        [Tooltip("Where the focus level sits, as a fraction of the view height from the bottom.")]
        [SerializeField, Range(0f, 1f)] private float focusFraction = 0.38f;

        [Header("Templates (inactive)")]
        [SerializeField] private Image tileTemplate;
        [SerializeField] private UiPolyline roadTemplate;
        [SerializeField] private MapNodeView nodeTemplate;
        [SerializeField] private Button chestTemplate;
        [SerializeField] private MapSeamView seamTemplate;

        [Header("Bus + unlock trail")]
        [SerializeField] private RectTransform bus;
        [SerializeField] private RectTransform busRumble;
        [SerializeField] private Image busImage;
        [SerializeField] private Image[] busPuffs = new Image[3];
        [SerializeField] private UiPolyline trailGlow;
        [SerializeField] private UiPolyline trailMarch;

        [Header("Top bar")]
        [SerializeField] private Button homeButton;
        [SerializeField] private TMP_Text worldNumberText;
        [SerializeField] private TMP_Text worldNameText;
        [SerializeField] private TMP_Text toastFontProbe;
        [Tooltip("Coin icon of the map's coin pill (target of coin flies).")]
        [SerializeField] private RectTransform coinTarget;

        [Header("Reward screens (Home scene, wired by the builder)")]
        [SerializeField] private GiftChestScreen giftChest;
        [SerializeField] private NewWorldScreen newWorld;

        public float Clock { get; private set; }
        /// <summary>Seconds into the unlock timeline (design times), or negative when no unlock is playing.</summary>
        public float UnlockTime => unlockLevel > 0 ? Clock - unlockStart : -1f;

        private RoadMapGeometry geo;
        private int levelCount, current;
        private readonly Dictionary<int, TileView> tiles = new Dictionary<int, TileView>();
        private readonly Stack<TileView> pool = new Stack<TileView>();
        private int unlockLevel;
        private float unlockStart;
        private float busArc;
        private float scrollVelocity;
        private bool following;

        private const float UnlockEnd = 5f;
        private const float StopBeforeNext = 30f;
        private static AnimationCurve puffEase;

        private sealed class TileView
        {
            public int index;
            public Image image;
            public UiPolyline road;
            public readonly MapNodeView[] nodes = new MapNodeView[RoadMapGeometry.LevelsPerTile];
            public Button chest;
            public MapSeamView seam;
        }

        // ------------------------------------------------------------------ open / close

        private bool dragged;
        private int pendingWorld; // world whose sign stays locked until its New World screen has shown
        private Coroutine rewards;

        private void Awake()
        {
            homeButton.onClick.AddListener(Close);
            MapDragWatcher watcher = scroll.GetComponent<MapDragWatcher>();
            if (watcher == null) watcher = scroll.gameObject.AddComponent<MapDragWatcher>();
            watcher.BeganDrag += () => dragged = true;
            puffEase ??= CssCurve.Build(new[] { 0f, 1f }, new[] { 0f, 1f }, new Vector4(0f, 0f, 0.58f, 1f));
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Opens the map; unlockedLevel > 0 plays that level's unlock once (if it has not played yet).</summary>
        public void Open(int unlockedLevel = 0)
        {
            geo ??= new RoadMapGeometry(roadJson.text);
            levelCount = LevelPlan.PlannedLevels;
            current = Mathf.Clamp(SaveSystem.Data.level, 1, levelCount);
            gameObject.SetActive(true);
            Instance = this;

            content.sizeDelta = new Vector2(content.sizeDelta.x, RoadMapGeometry.TileCount(levelCount) * RoadMapGeometry.TileHeight);
            foreach (TileView tv in new List<TileView>(tiles.Values)) Release(tv);

            unlockLevel = 0;
            Clock = 0f;
            if (unlockedLevel > 0 && unlockedLevel < levelCount && unlockedLevel > SaveSystem.Data.mapUnlockShown)
            {
                unlockLevel = unlockedLevel;
                unlockStart = 0f;
                SaveSystem.Data.mapUnlockShown = unlockedLevel; // never replays, even if the map is closed mid-way
                SaveSystem.Commit();
                busArc = geo.LevelArc(unlockedLevel);
                // Trail graphics stretch over the content with pivot (0,0): content space = their local space.
                List<Vector2> path = geo.ContentPath(geo.LevelArc(unlockedLevel), geo.LevelArc(unlockedLevel + 1));
                trailGlow.SetPoints(path);
                trailMarch.SetPoints(path);
            }
            else
            {
                busArc = ParkArc(current); // where the last unlock left it, facing along the road
            }
            trailGlow.gameObject.SetActive(unlockLevel > 0);
            trailMarch.gameObject.SetActive(unlockLevel > 0);

            if (rewards != null) StopCoroutine(rewards);
            rewards = null;
            pendingWorld = unlockLevel > 0 && MapRewards.WorldDue(SaveSystem.Data, unlockLevel + 1) ? MapRewards.WorldOf(unlockLevel + 1) : 0;
            int focus = unlockLevel > 0 ? unlockLevel : current;
            Canvas.ForceUpdateCanvases();
            SetScrollY(TargetScrollFor(geo.ContentAt(unlockLevel > 0 ? busArc : geo.LevelArc(focus), out _).y));
            following = unlockLevel > 0;
            dragged = false;

            int world = RoadMapGeometry.TileOf(current);
            worldNumberText.text = "WORLD " + (world + 1);
            worldNameText.text = WorldNames[world % WorldNames.Length];
            UpdateTiles();
            PlaceBus(0f);
        }

        public void Close() => gameObject.SetActive(false);

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            Clock += Time.deltaTime;
            float ut = UnlockTime;
            if (ut >= UnlockEnd)
            {
                int won = unlockLevel;
                unlockLevel = 0;
                rewards = StartCoroutine(AfterUnlock(won));
            }

            if (unlockLevel > 0)
            {
                // Bus: drives N -> just before N+1 in 2.4 s from 0.6 s (smoothstep), trail marches and fades out.
                float k = Mathf.Clamp01((ut - 0.6f) / 2.4f);
                float s = k * k * (3f - 2f * k);
                busArc = Mathf.Lerp(geo.LevelArc(unlockLevel), ParkArc(unlockLevel + 1), s);
                // The trail stays until the bus arrives (3.0 s), then fades out over 0.3 s.
                float trailAlpha = ut < 3f ? 1f : Mathf.Clamp01(1f - (ut - 3f) / 0.3f);
                SetAlpha(trailGlow, 0.35f * trailAlpha);
                SetAlpha(trailMarch, trailAlpha);
                trailMarch.DashOffset = -38f * (Clock / 0.6f % 1f);
                if (trailAlpha <= 0f)
                {
                    trailGlow.gameObject.SetActive(false);
                    trailMarch.gameObject.SetActive(false);
                }
            }
            else if (trailGlow.gameObject.activeSelf)
            {
                trailGlow.gameObject.SetActive(false);
                trailMarch.gameObject.SetActive(false);
            }

            bool driving = unlockLevel > 0 && ut > 0.6f && ut < 3f;
            PlaceBus(driving ? ut : -1f);

            if (following)
            {
                if (dragged) following = false; // the player took over
                else
                {
                    float target = TargetScrollFor(bus.anchoredPosition.y);
                    SetScrollY(Mathf.SmoothDamp(content.anchoredPosition.y, target, ref scrollVelocity, 0.25f));
                    if (unlockLevel == 0) following = false;
                }
            }

            UpdateTiles();
            foreach (TileView tv in tiles.Values)
            {
                foreach (MapNodeView n in tv.nodes)
                {
                    if (n != null && n.gameObject.activeSelf) n.Tick(Clock, n.Level == unlockLevel + 1 ? ut : -1f);
                }
            }
        }

        /// <summary>The bus waits just before the level to play (the same spot an unlock drive ends on).</summary>
        private float ParkArc(int level) => Mathf.Max(0f, geo.LevelArc(level) - StopBeforeNext);

        private void PlaceBus(float driveTime)
        {
            Vector2 p = geo.ContentAt(busArc, out Vector2 dir);
            bus.anchoredPosition = p;
            int frame = RoadMapGeometry.FrameFor(dir.x, -dir.y);
            if (busFrames.Length == RoadMapGeometry.BusFrames) busImage.sprite = busFrames[frame];

            bool driving = driveTime >= 0f;
            float r = driving ? Clock / 0.22f % 1f : 0f;
            busRumble.anchoredPosition = new Vector2(0f, driving ? 2f * (1f - Mathf.Abs(2f * r - 1f)) : 0f);

            // Smoke puffs behind the bus (opposite its heading), 0.7 s ease-out, staggered 0.23 s.
            for (int i = 0; i < busPuffs.Length; i++)
            {
                Image puff = busPuffs[i];
                if (!driving)
                {
                    SetAlpha(puff, 0f);
                    continue;
                }
                float e = puffEase.Evaluate(Mathf.Repeat((Clock - i * 0.23f) / 0.7f, 1f));
                puff.rectTransform.anchoredPosition = new Vector2(0f, 9.4f) - dir * (38f + 44f * e);
                puff.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.8f, e);
                SetAlpha(puff, Mathf.Lerp(0.9f, 0f, e));
            }
        }

        // ------------------------------------------------------------------ scrolling

        private float ViewHeight => scroll.viewport != null ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;

        /// <summary>content.anchoredPosition.y that shows content y at focusFraction of the view (content is bottom-anchored).</summary>
        private float TargetScrollFor(float contentY)
        {
            float max = Mathf.Max(0f, content.rect.height - ViewHeight);
            return -Mathf.Clamp(contentY - focusFraction * ViewHeight, 0f, max);
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

            var drop = new List<TileView>();
            foreach (TileView tv in tiles.Values)
            {
                if (tv.index < first || tv.index > last) drop.Add(tv);
            }
            foreach (TileView tv in drop) Release(tv);
            for (int i = first; i <= last; i++)
            {
                if (!tiles.ContainsKey(i)) tiles[i] = Acquire(i);
            }
        }

        private TileView Acquire(int index)
        {
            TileView tv = pool.Count > 0 ? pool.Pop() : CreateView();
            tv.index = index;
            float y0 = index * RoadMapGeometry.TileHeight;

            tv.image.sprite = tileSprites[index % tileSprites.Length];
            tv.image.rectTransform.anchoredPosition = new Vector2(RoadMapGeometry.TileLeft, y0);
            tv.road.rectTransform.anchoredPosition = new Vector2(0f, y0);
            tv.image.gameObject.SetActive(true);
            tv.road.gameObject.SetActive(true);

            for (int i = 0; i < tv.nodes.Length; i++)
            {
                int level = index * RoadMapGeometry.LevelsPerTile + i + 1;
                MapNodeView n = tv.nodes[i];
                bool exists = level <= levelCount;
                n.gameObject.SetActive(exists);
                if (!exists) continue;
                Vector2 p = geo.NodeContent(level);
                ((RectTransform)n.transform).anchoredPosition = p + new Vector2(0f, 3f);
                MapNodeState state = level < current ? MapNodeState.Completed : level == current ? MapNodeState.Current : MapNodeState.Locked;
                n.Setup(level, state, SaveSystem.Data.GetStars(level), level % 10 == 0);
            }

            int chestLevel = index * RoadMapGeometry.LevelsPerTile + 5;
            bool chest = chestLevel <= levelCount;
            tv.chest.gameObject.SetActive(chest);
            if (chest)
            {
                Vector2 p = geo.NodeContent(chestLevel);
                ((RectTransform)tv.chest.transform).anchoredPosition = p + new Vector2(-104f, 40f);
                bool claimed = MapRewards.ChestClaimed(SaveSystem.Data, chestLevel);
                tv.chest.GetComponent<MapChestView>().SetClaimed(claimed);
                tv.chest.onClick.RemoveAllListeners();
                tv.chest.onClick.AddListener(() => Toast.Show(MapRewards.ChestClaimed(SaveSystem.Data, chestLevel)
                    ? "Gift collected" : $"Gift after level {chestLevel}"));
            }

            bool seam = index > 0;
            tv.seam.gameObject.SetActive(seam);
            if (seam)
            {
                ((RectTransform)tv.seam.transform).anchoredPosition = new Vector2(0f, y0);
                int firstLevel = index * RoadMapGeometry.LevelsPerTile + 1;
                string name = WorldNames[index % WorldNames.Length];
                bool locked = firstLevel > current || pendingWorld == index + 1;
                tv.seam.Setup(index + 1, name, firstLevel, locked);
                tv.seam.Button.onClick.RemoveAllListeners();
                tv.seam.Button.onClick.AddListener(() =>
                {
                    // Unlocked: preview the New World screen; locked: just its name.
                    if (firstLevel <= current && pendingWorld != index + 1 && newWorld != null) newWorld.Show(index + 1, null);
                    else Toast.Show($"World {index + 1}: {name}");
                });
            }
            return tv;
        }

        private void Release(TileView tv)
        {
            tiles.Remove(tv.index);
            tv.image.gameObject.SetActive(false);
            tv.road.gameObject.SetActive(false);
            foreach (MapNodeView n in tv.nodes) n.gameObject.SetActive(false);
            tv.chest.gameObject.SetActive(false);
            tv.seam.gameObject.SetActive(false);
            pool.Push(tv);
        }

        private TileView CreateView()
        {
            var tv = new TileView
            {
                image = Instantiate(tileTemplate, tilesLayer),
                road = Instantiate(roadTemplate, roadLayer),
                chest = Instantiate(chestTemplate, chestLayer),
                seam = Instantiate(seamTemplate, seamLayer)
            };
            tv.road.SetPoints(geo.Points);
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

        /// <summary>Rebuilds the visible tiles (chest / sign state changed).</summary>
        private void RefreshTiles()
        {
            foreach (TileView tv in new List<TileView>(tiles.Values)) Release(tv);
            UpdateTiles();
        }

        // ------------------------------------------------------------------ taps

        private void OnNodeTapped(MapNodeView node)
        {
            switch (node.State)
            {
                case MapNodeState.Current:
                    SceneLoader.LoadGame();
                    break;
                case MapNodeState.Completed:
                    string stars = $"{node.Stars}/3 stars"; // Latin only: the UI fonts have no star glyphs
                    Toast.Show($"Level {node.Level}  {stars}");
                    break;
                default:
                    Toast.Show($"Level {node.Level} is locked");
                    break;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
