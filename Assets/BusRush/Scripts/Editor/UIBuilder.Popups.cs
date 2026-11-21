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
    }
}
