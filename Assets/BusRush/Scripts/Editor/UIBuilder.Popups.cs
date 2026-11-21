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
    }
}
