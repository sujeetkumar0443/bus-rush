using BusRush.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BusRush.EditorTools.UIKit;

namespace BusRush.EditorTools
{
    public static partial class UIBuilder
    {
        private struct Shell
        {
            public GameObject Root;
            public RectTransform Panel;   // sized panel (scaled by Popup to fit)
            public RectTransform Face;    // content area of the panel
            public TextMeshProUGUI Title;
            public Chunk Close;
            public Image Dim;
        }

        /// <summary>Full-screen popup root: dim + safe area + chunky panel with header ribbon and optional close button.</summary>
        private static Shell PopupShell<T>(string name, float w, float h, string title, Color headerFace, Color headerLip,
                                           bool closeButton = true) where T : Popup
        {
            var s = new Shell { Root = new GameObject(name, typeof(RectTransform)) };
            s.Root.layer = 5;
            Stretch((RectTransform)s.Root.transform);
            s.Root.AddComponent<CanvasGroup>();
            T popup = s.Root.AddComponent<T>();

            s.Dim = Img(s.Root.transform, "Dim", White, new Color(0.06f, 0.09f, 0.18f, 0.65f));
            s.Dim.raycastTarget = true;
            Stretch(s.Dim.rectTransform);
            Button dimButton = s.Dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            RectTransform safe = SafeArea(s.Root.transform);
            s.Panel = Rect(safe, "Panel");
            Place(s.Panel, 0.5f, 0.5f, 0f, -10f, w, h);

            Chunk body = Chunky(s.Panel, "Card", PanelWhite, PanelLip, w, h, 30f, 9f, false, true, false);
            Stretch(body.Root);
            s.Face = body.Face;

            Chunk header = Chunky(s.Panel, "Header", headerFace, headerLip, w - 56f, 62f, 22f, 7f);
            Top(header.Root, -26f, w - 56f, 62f);
            s.Title = Label(header.Face, "Title", title, 34f, TextStyle.Heading);
            Stretch(s.Title.rectTransform, 16f, 2f, 16f, 0f);

            if (closeButton)
            {
                s.Close = CloseButton(s.Panel);
                Place(s.Close.Root, 1f, 1f, -6f, -4f, 50f, 54f);
            }

            Wire(popup, ("dim", s.Dim), ("panel", s.Panel), ("closeButton", closeButton ? s.Close.Button : null));
            return s;
        }

        /// <summary>Soft grey row card inside a panel.</summary>
        private static RectTransform Row(Transform parent, string name, float y, float w, float h)
        {
            Image row = Img(parent, name, Round, PanelSoft, 16f);
            Top(row.rectTransform, y, w, h);
            return row.rectTransform;
        }

        // ------------------------------------------------------------------ Settings / Pause

        private static GameObject BuildSettings()
        {
            const float w = 340f, homeH = 300f, gameH = 390f;
            Shell s = PopupShell<SettingsPopup>("SettingsPopup", w, gameH, "SETTINGS", Blue, BlueLip);

            SwitchToggle MakeRow(string name, string label, string icon, float y)
            {
                RectTransform row = Row(s.Face, name + "Row", y, w - 44f, 60f);
                Image badge = Img(row, "IconBg", Round, Blue, 14f);
                Place(badge.rectTransform, 0f, 0.5f, 12f, 0f, 42f, 42f, 0f, 0.5f);
                Image i = IconImg(badge.transform, "Icon", icon, 34f);
                Place(i.rectTransform, 0.5f, 0.5f, 0f, 0f, 34f, 34f);
                TextMeshProUGUI t = Label(row, "Label", label, 24f, TextStyle.BodyPlain, TextGrey, TextAlignmentOptions.Left);
                Stretch(t.rectTransform, 66f, 0f, 90f, 0f);
                return Switch(row, name + "Switch");
            }

            SwitchToggle sound = MakeRow("Sound", "Sound", "sound", 56f);
            SwitchToggle music = MakeRow("Music", "Music", "music", 126f);
            SwitchToggle vib = MakeRow("Vibration", "Vibration", "vibrate", 196f);

            RectTransform gameButtons = Rect(s.Face, "GameButtons");
            Bottom(gameButtons, 22f, w - 44f, 66f);
            Chunk home = IconButton(gameButtons, "HomeButton", "home", Blue, BlueLip, 66f, 66f, 38f);
            Place(home.Root, 0f, 0.5f, 0f, 0f, 66f, 66f, 0f, 0.5f);
            Chunk restart = IconButton(gameButtons, "RestartButton", "restart", Blue, BlueLip, 66f, 66f, 38f);
            Place(restart.Root, 0f, 0.5f, 78f, 0f, 66f, 66f, 0f, 0.5f);
            Chunk resume = TextButton(gameButtons, "ResumeButton", "RESUME", Green, GreenLip, 140f, 66f, 28f);
            Place(resume.Root, 1f, 0.5f, 0f, 0f, 140f, 66f, 1f, 0.5f);

            Wire(s.Root.GetComponent<SettingsPopup>(),
                ("title", s.Title), ("sound", sound), ("music", music), ("vibration", vib),
                ("gameButtons", gameButtons.gameObject), ("homeButton", home.Button), ("restartButton", restart.Button),
                ("resumeButton", resume.Button), ("panelForResize", s.Panel),
                ("homePanelHeight", homeH), ("gamePanelHeight", gameH));
            return s.Root;
        }

        private static SwitchToggle Switch(RectTransform row, string name)
        {
            RectTransform root = Rect(row, name);
            Place(root, 1f, 0.5f, -12f, 0f, 70f, 40f, 1f, 0.5f);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Round;
            hit.color = new Color(1f, 1f, 1f, 0f);
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = hit;
            toggle.isOn = true;

            Chunk track = Chunky(root, "Track", Green, GreenLip, 70f, 40f, -1f, 5f, false, false, false);
            Stretch(track.Root);
            Image knob = Img(track.Face, "Knob", Circle, Color.white);
            knob.preserveAspect = true;
            Place(knob.rectTransform, 0.5f, 0.5f, 15f, 0f, 29f, 29f);

            SwitchToggle sw = root.gameObject.AddComponent<SwitchToggle>();
            Wire(sw, ("track", track.FaceImage), ("trackLip", track.LipImage), ("knob", knob.rectTransform), ("knobTravel", 15f),
                ("onColor", Green), ("onLipColor", GreenLip), ("offColor", Grey), ("offLipColor", GreyLip));
            return sw;
        }

        // ------------------------------------------------------------------ Shop

        private static GameObject BuildShop()
        {
            const float w = 350f, h = 548f, rowW = 306f, rowH = 70f, step = 78f;
            Shell s = PopupShell<ShopPopup>("ShopPopup", w, h, "SHOP", Gold, GoldLip);

            (Button button, TextMeshProUGUI price, RectTransform row) Product(string name, float y, string icon, string title, string sub,
                                                                            Color face, Color lip, string buttonText = null)
            {
                RectTransform row = Row(s.Face, name, y, rowW, rowH);
                Image ic = IconImg(row, "Icon", icon, 52f);
                Place(ic.rectTransform, 0f, 0.5f, 10f, 0f, 52f, 52f, 0f, 0.5f);
                TextMeshProUGUI t = Label(row, "Title", title, 21f, TextStyle.BodyPlain, DarkNavy, TextAlignmentOptions.BottomLeft);
                Stretch(t.rectTransform, 72f, 34f, 112f, 8f);
                TextMeshProUGUI st = Label(row, "Subtitle", sub, 14f, TextStyle.BodyBoldPlain, TextGrey, TextAlignmentOptions.TopLeft);
                Stretch(st.rectTransform, 72f, 8f, 112f, 38f);

                Chunk b = Chunky(row, "BuyButton", face, lip, 98f, 48f, 14f, 6f, true);
                Place(b.Root, 1f, 0.5f, -10f, 1f, 98f, 48f, 1f, 0.5f);
                TextMeshProUGUI label = Label(b.Face, "Price", buttonText ?? MonetizationPlaceholder.PriceLabel, 19f, TextStyle.Body);
                Stretch(label.rectTransform, 6f, 0f, 6f, 0f);
                return (b.Button, label, row);
            }

            var removeAds = Product("RemoveAdsRow", 56f, "noads", "Remove Ads", "No more banner ads", Green, GreenLip);
            TextMeshProUGUI owned = Label(removeAds.row, "OwnedLabel", "OWNED", 18f, TextStyle.BodyPlain, GreenLip, TextAlignmentOptions.Right);
            Stretch(owned.rectTransform, 200f, 0f, 16f, 0f);
            owned.gameObject.SetActive(false);

            var pack = Product("BoosterPackRow", 56f + step, "gift", "Booster Pack", "2x VIP, Arrange, Shuffle", Green, GreenLip);
            var c1 = Product("Coins500Row", 56f + step * 2, "coin", "500 Coins", "A handy top-up", Green, GreenLip);
            var c2 = Product("Coins1200Row", 56f + step * 3, "coins_pile", "1,200 Coins", "Popular", Green, GreenLip);
            var c3 = Product("Coins3000Row", 56f + step * 4, "coins_heap", "3,000 Coins", "Best value!", Green, GreenLip);
            var free = Product("FreeCoinsRow", 56f + step * 5, "video", "Free Coins", "Watch a short video", Blue, BlueLip, "");

            // Free coins button: video icon + amount.
            Transform freeFace = free.button.transform.Find("Body/Face");
            free.price.text = "+50";
            Stretch(free.price.rectTransform, 30f, 0f, 6f, 0f);
            Image vid = IconImg(freeFace, "Icon", "coin", 26f);
            Place(vid.rectTransform, 0f, 0.5f, 8f, 0f, 26f, 26f, 0f, 0.5f);

            Wire(s.Root.GetComponent<ShopPopup>(),
                ("removeAds.productId", "remove_ads"), ("removeAds.button", removeAds.button), ("removeAds.priceText", removeAds.price),
                ("removeAdsOwnedLabel", owned.gameObject),
                ("boosterPack.productId", "booster_pack"), ("boosterPack.button", pack.button), ("boosterPack.priceText", pack.price),
                ("coinPacks", new object[3]),
                ("freeCoinsButton", free.button), ("freeCoinsAmount", free.price));
            Wire(s.Root.GetComponent<ShopPopup>(),
                ("coinPacks.Array.data[0].productId", "coins_500"), ("coinPacks.Array.data[0].button", c1.button), ("coinPacks.Array.data[0].priceText", c1.price),
                ("coinPacks.Array.data[1].productId", "coins_1200"), ("coinPacks.Array.data[1].button", c2.button), ("coinPacks.Array.data[1].priceText", c2.price),
                ("coinPacks.Array.data[2].productId", "coins_3000"), ("coinPacks.Array.data[2].button", c3.button), ("coinPacks.Array.data[2].priceText", c3.price));
            return s.Root;
        }

        // ------------------------------------------------------------------ Daily reward

        private static GameObject BuildDaily()
        {
            const float w = 350f, h = 476f, cardW = 70f, cardH = 94f, gap = 8f;
            Shell s = PopupShell<DailyRewardPopup>("DailyRewardPopup", w, h, "DAILY GIFT", Red, RedLip);

            TextMeshProUGUI timer = Label(s.Face, "TimerText", "Your daily gift is ready!", 17f, TextStyle.BodyPlain, TextGrey);
            Top(timer.rectTransform, 50f, w - 40f, 26f);

            var cards = new DayCardView[DailyRewards.Days];
            float rowW = cardW * 4f + gap * 3f;
            for (int i = 0; i < DailyRewards.Days; i++)
            {
                bool top = i < 4;
                int col = top ? i : i - 4;
                float cw = i == 6 ? cardW * 2f + gap : cardW;
                float x = -rowW * 0.5f + col * (cardW + gap) + cw * 0.5f;
                float y = top ? 84f : 84f + cardH + 12f;
                cards[i] = DayCard(s.Face, i, x, y, cw, cardH);
            }

            Chunk claim = TextButton(s.Face, "ClaimButton", "CLAIM", Green, GreenLip, w - 50f, 64f, 32f);
            Top(claim.Root, 306f, w - 50f, 64f);
            TextMeshProUGUI claimLabel = claim.Face.Find("Label").GetComponent<TextMeshProUGUI>();

            Chunk x2 = Chunky(s.Face, "DoubleButton", Gold, GoldLip, w - 50f, 58f, 18f, 7f, true);
            Top(x2.Root, 382f, w - 50f, 58f);
            Image vid = IconImg(x2.Face, "Icon", "video", 36f);
            Place(vid.rectTransform, 0.5f, 0.5f, -86f, 0f, 36f, 36f);
            TextMeshProUGUI x2Label = Label(x2.Face, "Label", "x2 CLAIM", 28f, TextStyle.Heading);
            Stretch(x2Label.rectTransform, 118f, 0f, 30f, 0f);
            x2Label.alignment = TextAlignmentOptions.Left;

            Wire(s.Root.GetComponent<DailyRewardPopup>(),
                ("cards", cards), ("claimButton", claim.UIButton), ("claimLabel", claimLabel), ("doubleButton", x2.UIButton),
                ("timerText", timer), ("coinSprite", Icon("coin")), ("packSprite", Icon("gift")));
            return s.Root;
        }

        private static DayCardView DayCard(Transform parent, int index, float x, float y, float w, float h)
        {
            RectTransform root = Rect(parent, "Day" + (index + 1));
            Place(root, 0.5f, 1f, x, -y, w, h, 0.5f, 1f);

            Image glow = Img(root, "Highlight", Glow, Gold);
            Stretch(glow.rectTransform, -22f, -22f, -22f, -22f);

            RectTransform content = Stretch(Rect(root, "Content"));
            Chunk card = Chunky(content, "Card", Color.white, PanelLip, w, h, 14f, 5f, false, true, false);
            Stretch(card.Root);

            TextMeshProUGUI day = Label(card.Face, "DayLabel", "DAY " + (index + 1), 13f, TextStyle.BodyPlain, TextGrey);
            Top(day.rectTransform, 4f, w - 8f, 18f);
            Image icon = IconImg(card.Face, "Icon", index == 6 ? "gift" : "coin", 42f);
            Place(icon.rectTransform, 0.5f, 0.5f, 0f, 2f, 42f, 42f);
            TextMeshProUGUI amount = Label(card.Face, "Amount", "50", 19f, TextStyle.HeadingPlain, Navy);
            Place(amount.rectTransform, 0.5f, 0f, 0f, 3f, w - 6f, 22f, 0.5f, 0f);

            Image check = Badge(card.Face, "Check", Green, 26f, out RectTransform checkContent);
            Place(check.rectTransform, 1f, 1f, 10f, 10f, 26f, 26f, 1f, 1f);
            Image tick = IconImg(checkContent, "Tick", "check", 18f);
            Place(tick.rectTransform, 0.5f, 0.5f, 0f, 0f, 18f, 18f);

            DayCardView view = root.gameObject.AddComponent<DayCardView>();
            Wire(view, ("face", card.FaceImage), ("lip", card.LipImage), ("dayLabel", day), ("icon", icon), ("amount", amount),
                ("check", check.gameObject), ("highlight", glow.gameObject), ("content", content));
            return view;
        }

        // ------------------------------------------------------------------ Boosters

        private static GameObject BuildBoosters()
        {
            const float w = 350f, h = 516f;
            Shell s = PopupShell<BoosterPopup>("BoosterPopup", w, h, "BOOSTERS", Blue, BlueLip);

            var types = new[] { BoosterType.Vip, BoosterType.Arrange, BoosterType.Shuffle };
            string[] labels = { "VIP", "ARRANGE", "SHUFFLE" };
            string[] icons = { "crown", "arrange", "shuffle" };
            string[] titles = { "VIP", "ARRANGE", "SHUFFLE" };
            string[] desc =
            {
                "Send any vehicle straight to a free bay, even when it is blocked.",
                "Sort the waiting crowd so passengers of the same colour stand together.",
                "Mix up the colours of the vehicles still parked on the lot."
            };

            var tabParts = new Chunk[3];
            var tabLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                Chunk t = Chunky(s.Face, labels[i] + "Tab", PanelSoft, Grey, 96f, 46f, 14f, 5f, true, false);
                Top(t.Root, 52f, 96f, 46f, (i - 1) * 104f);
                tabLabels[i] = Label(t.Face, "Label", labels[i], 19f, TextStyle.HeadingPlain, TextGrey);
                Stretch(tabLabels[i].rectTransform, 4f, 0f, 4f, 0f);
                tabParts[i] = t;
            }

            RectTransform iconRoot = Rect(s.Face, "IconRoot");
            Top(iconRoot, 112f, 140f, 140f);
            Image glow = Img(iconRoot, "Glow", Glow, new Color(1f, 0.85f, 0.35f, 0.9f));
            Stretch(glow.rectTransform, -30f, -30f, -30f, -30f);
            Image disc = Img(iconRoot, "Disc", Circle, PanelSoft);
            disc.preserveAspect = true;
            Stretch(disc.rectTransform);
            Image icon2D = IconImg(iconRoot, "Icon2D", "crown", 96f);
            Place(icon2D.rectTransform, 0.5f, 0.5f, 0f, 0f, 96f, 96f);
            Image icon3D = Img(iconRoot, "Icon3D", null, new Color(1f, 1f, 1f, 0f));
            Place(icon3D.rectTransform, 0.5f, 0.5f, 0f, 0f, 124f, 124f);

            TextMeshProUGUI name = Label(s.Face, "Name", "VIP", 34f, TextStyle.HeadingPlain, Navy);
            Top(name.rectTransform, 262f, w - 40f, 38f);
            TextMeshProUGUI description = Label(s.Face, "Description", desc[0], 17f, TextStyle.BodyBoldPlain, TextGrey);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = false;
            Top(description.rectTransform, 302f, w - 56f, 48f);
            TextMeshProUGUI ownedText = Label(s.Face, "Owned", "Owned: 2", 19f, TextStyle.BodyPlain, Navy);
            Top(ownedText.rectTransform, 354f, w - 40f, 24f);
            TextMeshProUGUI lockText = Label(s.Face, "LockText", "Unlocks at level 13", 15f, TextStyle.BodyPlain, Red);
            Top(lockText.rectTransform, 376f, w - 40f, 20f);

            Chunk buy = Chunky(s.Face, "BuyButton", Green, GreenLip, 230f, 64f, 18f, 7f, true);
            Top(buy.Root, 400f, 230f, 64f);
            TextMeshProUGUI buyLabel = Label(buy.Face, "Label", "BUY", 28f, TextStyle.Heading, null, TextAlignmentOptions.Left);
            Stretch(buyLabel.rectTransform, 26f, 0f, 120f, 0f);
            Image coin = IconImg(buy.Face, "Coin", "coin", 36f);
            Place(coin.rectTransform, 0.5f, 0.5f, 6f, 0f, 36f, 36f);
            TextMeshProUGUI price = Label(buy.Face, "Price", "300", 28f, TextStyle.Heading, null, TextAlignmentOptions.Left);
            Stretch(price.rectTransform, 140f, 0f, 14f, 0f);

            TextMeshProUGUI feedback = Label(s.Face, "Feedback", "", 17f, TextStyle.BodyPlain, Red);
            Top(feedback.rectTransform, 474f, w - 40f, 22f);

            BoosterPopup popup = s.Root.GetComponent<BoosterPopup>();
            Wire(popup, ("tabs", new object[3]));
            for (int i = 0; i < 3; i++)
            {
                string p = $"tabs.Array.data[{i}].";
                Wire(popup, (p + "type", types[i]), (p + "button", tabParts[i].Button), (p + "face", tabParts[i].FaceImage),
                    (p + "lip", tabParts[i].LipImage), (p + "label", tabLabels[i]), (p + "icon", Icon(icons[i])),
                    (p + "title", titles[i]), (p + "description", desc[i]));
            }
            Wire(popup, ("icon", icon2D), ("iconRoot", iconRoot), ("nameText", name), ("descriptionText", description),
                ("ownedText", ownedText), ("lockText", lockText), ("buyButton", buy.UIButton), ("buyPrice", price),
                ("feedbackText", feedback),
                ("selectedFace", Blue), ("selectedLip", BlueLip), ("idleFace", PanelSoft), ("idleLip", Grey),
                ("selectedText", Color.white), ("idleText", TextGrey));
            return s.Root;
        }

        // ------------------------------------------------------------------ Level complete

        private static GameObject BuildLevelComplete()
        {
            const float w = 350f, h = 690f;
            Shell s = PopupShell<LevelCompletePopup>("LevelCompletePopup", w, h, "LEVEL COMPLETE!", Green, GreenLip, false);
            s.Title.fontSize = 32f;

            Image rays = Img(s.Panel, "Rays", Rays, new Color(1f, 0.93f, 0.6f, 0.75f));
            Place(rays.rectTransform, 0.5f, 1f, 0f, -70f, 560f, 560f);
            rays.transform.SetAsFirstSibling();
            rays.gameObject.AddComponent<Rotator>();

            RectTransform starsRoot = Rect(s.Face, "Stars");
            Top(starsRoot, 46f, 300f, 110f);
            var stars = new RectTransform[3];
            float[] xs = { -88f, 0f, 88f };
            float[] ys = { -12f, 4f, -12f };
            float[] sizes = { 78f, 98f, 78f };
            float[] rot = { 14f, 0f, -14f };
            for (int i = 0; i < 3; i++)
            {
                Image slot = IconImg(starsRoot, "StarSlot" + (i + 1), "star", sizes[i]);
                slot.color = new Color(0.12f, 0.16f, 0.27f, 0.22f);
                Place(slot.rectTransform, 0.5f, 0.5f, xs[i], ys[i], sizes[i], sizes[i]);
                slot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
                Image star = IconImg(slot.transform, "Star", "star", sizes[i]);
                Stretch(star.rectTransform);
                stars[i] = star.rectTransform;
            }

            TextMeshProUGUI level = Label(s.Face, "LevelText", "LEVEL 1", 22f, TextStyle.BodyPlain, TextGrey);
            Top(level.rectTransform, 160f, w - 40f, 26f);

            RectTransform reward = Rect(s.Face, "Reward");
            Top(reward, 190f, 220f, 54f);
            Image coin = IconImg(reward, "Coin", "coin", 48f);
            Place(coin.rectTransform, 0.5f, 0.5f, -48f, 0f, 48f, 48f);
            TextMeshProUGUI rewardText = Label(reward, "Amount", "+50", 42f, TextStyle.Heading, Gold, TextAlignmentOptions.Left);
            Stretch(rewardText.rectTransform, 92f, 0f, 0f, 0f);

            Chunk claim = TextButton(s.Face, "ClaimButton", "CLAIM", Green, GreenLip, w - 60f, 64f, 32f);
            Top(claim.Root, 256f, w - 60f, 64f);
            Chunk next = Chunky(s.Face, "NextButton", Blue, BlueLip, w - 60f, 64f, 18f, 7f, true);
            Top(next.Root, 332f, w - 60f, 64f);
            TextMeshProUGUI nextLabel = Label(next.Face, "Label", "NEXT LEVEL", 30f, TextStyle.Heading);
            Stretch(nextLabel.rectTransform, 10f, 0f, 46f, 0f);
            Image play = IconImg(next.Face, "Icon", "play", 30f);
            Place(play.rectTransform, 1f, 0.5f, -22f, 0f, 30f, 30f, 1f, 0.5f);

            RectTransform mrec = Rect(s.Face, "MrecSlot");
            Top(mrec, 416f, 300f, 250f);

            Wire(s.Root.GetComponent<LevelCompletePopup>(),
                ("stars", stars), ("levelText", level), ("rewardText", rewardText), ("rewardRoot", reward),
                ("claimButton", claim.UIButton), ("claimLabel", claim.Face.Find("Label").GetComponent<TextMeshProUGUI>()),
                ("nextButton", next.UIButton), ("closeOnDimClick", false), ("closeOnBack", false));
            return s.Root;
        }

        // ------------------------------------------------------------------ Out of space

        private static GameObject BuildOutOfSpace()
        {
            const float w = 350f, h = 440f;
            Shell s = PopupShell<OutOfSpacePopup>("OutOfSpacePopup", w, h, "OUT OF SPACE!", Red, RedLip, false);

            TextMeshProUGUI sub = Label(s.Face, "Subtitle", "All parking bays are full!", 19f, TextStyle.BodyPlain, TextGrey);
            Top(sub.rectTransform, 50f, w - 40f, 26f);

            const float bw = 34f, bh = 54f, gap = 4f;
            float total = bw * 8f + gap * 7f;
            var bays = new object[8]; // reserves the 8 struct slots; filled below
            var bayParts = new (Image face, Image car, GameObject lockIcon, GameObject plus)[8];
            for (int i = 0; i < 8; i++)
            {
                float x = -total * 0.5f + bw * 0.5f + i * (bw + gap);
                Chunk bay = Chunky(s.Face, "Bay" + (i + 1), Color.white, PanelLip, bw, bh, 9f, 4f, false, false, false);
                Top(bay.Root, 100f, bw, bh, x);
                Image car = IconImg(bay.Face, "Car", "car", 48f);
                Place(car.rectTransform, 0.5f, 0.5f, 0f, 0f, 34f, 48f);
                Image lockIcon = IconImg(bay.Face, "Lock", "lock", 26f);
                Place(lockIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 26f, 26f);
                Image plus = Badge(bay.Root, "PlusBadge", Green, 28f, out RectTransform plusContent);
                Place(plus.rectTransform, 0.5f, 1f, 0f, 6f, 28f, 28f);
                Image plusIcon = IconImg(plusContent, "Plus", "plus", 18f);
                Place(plusIcon.rectTransform, 0.5f, 0.5f, 0f, 0f, 18f, 18f);
                bayParts[i] = (bay.FaceImage, car, lockIcon.gameObject, plus.gameObject);
            }

            TextMeshProUGUI title = Label(s.Face, "ExtraBayTitle", "+1 BAY", 32f, TextStyle.Heading, Gold);
            Top(title.rectTransform, 168f, w - 40f, 40f);

            Chunk coins = Chunky(s.Face, "CoinsButton", Green, GreenLip, w - 60f, 64f, 18f, 7f, true);
            Top(coins.Root, 210f, w - 60f, 64f);
            Image coinIcon = IconImg(coins.Face, "Coin", "coin", 38f);
            Place(coinIcon.rectTransform, 0.5f, 0.5f, -40f, 0f, 38f, 38f);
            TextMeshProUGUI price = Label(coins.Face, "Price", "300", 30f, TextStyle.Heading, null, TextAlignmentOptions.Left);
            Stretch(price.rectTransform, 160f, 0f, 10f, 0f);

            Chunk ad = Chunky(s.Face, "AdButton", Blue, BlueLip, w - 60f, 64f, 18f, 7f, true);
            Top(ad.Root, 286f, w - 60f, 64f);
            Image vid = IconImg(ad.Face, "Icon", "video", 38f);
            Place(vid.rectTransform, 0.5f, 0.5f, -46f, 0f, 38f, 38f);
            TextMeshProUGUI free = Label(ad.Face, "Label", "FREE", 30f, TextStyle.Heading, null, TextAlignmentOptions.Left);
            Stretch(free.rectTransform, 158f, 0f, 10f, 0f);

            TextMeshProUGUI feedback = Label(s.Face, "Feedback", "", 16f, TextStyle.BodyPlain, Red);
            Top(feedback.rectTransform, 356f, w - 40f, 20f);

            RectTransform giveUpRt = Rect(s.Face, "GiveUpButton");
            Top(giveUpRt, 378f, 160f, 36f);
            Image hit = giveUpRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            Button giveUp = giveUpRt.gameObject.AddComponent<Button>();
            giveUp.transition = Selectable.Transition.None;
            giveUp.targetGraphic = hit;
            giveUpRt.gameObject.AddComponent<UIButton>();
            TextMeshProUGUI giveUpLabel = Label(giveUpRt, "Label", "<u>Give up</u>", 20f, TextStyle.BodyPlain, Red);
            Stretch(giveUpLabel.rectTransform);

            OutOfSpacePopup popup = s.Root.GetComponent<OutOfSpacePopup>();
            Wire(popup, ("bays", bays));
            for (int i = 0; i < 8; i++)
            {
                string p = $"bays.Array.data[{i}].";
                Wire(popup, (p + "face", bayParts[i].face), (p + "car", bayParts[i].car), (p + "lockIcon", bayParts[i].lockIcon),
                    (p + "plusBadge", bayParts[i].plus));
            }
            Wire(popup, ("coinsButton", coins.UIButton), ("coinsPrice", price), ("adButton", ad.UIButton), ("giveUpButton", giveUp),
                ("feedbackText", feedback), ("openFace", Color.white), ("lockedFace", Grey),
                ("closeOnDimClick", false), ("closeOnBack", false));
            return s.Root;
        }
    }
}
