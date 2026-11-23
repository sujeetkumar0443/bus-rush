using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Tile seam on the Road Map. The template holds the cloud band (mist) and the blue world banner; the screen instantiates
    /// it twice, once as the mist (behind the path dots and nodes) and once as the banner (also behind them, above the mist),
    /// so neither can cover a node. The old "Level N" lock badge is gone: locked nodes carry their own small lock.
    /// </summary>
    public sealed class MapSeamView : MonoBehaviour
    {
        [SerializeField] private Button signButton;
        [SerializeField] private TMP_Text worldNumber;
        [SerializeField] private TMP_Text worldName;
        [SerializeField] private GameObject lockBadge;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private GameObject[] clouds = new GameObject[0];
        [SerializeField] private RectTransform sign;
        [SerializeField] private CanvasGroup signGroup;

        private Vector2 signHome;
        private bool homeSet;
        private static AnimationCurve dropY, dropS;

        /// <summary>Banner size in design px (a node box must stay clear of it).</summary>
        public const float BannerWidth = 220f, BannerHeight = 60f;

        /// <summary>The banner's centre inside the seam rect (x from its left edge).</summary>
        public const float BannerCentreX = 195f;

        public Button Button => signButton;

        /// <summary>The world sign (for the New World card to zoom out of it).</summary>
        public RectTransform SignRect => sign;

        /// <summary>
        /// The sign drops in with a bounce (translateY 40 -> 0, scale 0.8 -> 1, cubic-bezier(.3,1.5,.5,1)): k &lt; 0 = not there yet,
        /// 0..1 = dropping, &gt;= 1 = settled.
        /// </summary>
        public void SetDrop(float k)
        {
            CacheHome();
            if (k < 0f)
            {
                signGroup.alpha = 0f;
                return;
            }
            k = Mathf.Clamp01(k);
            signGroup.alpha = Mathf.Clamp01(k * 3f);
            sign.anchoredPosition = signHome + new Vector2(0f, dropY.Evaluate(k));
            sign.localScale = Vector3.one * dropS.Evaluate(k);
        }

        /// <summary>Back to the settled look.</summary>
        public void ClearDrop()
        {
            CacheHome();
            signGroup.alpha = 1f;
            sign.anchoredPosition = signHome;
            sign.localScale = Vector3.one;
        }

        private void CacheHome()
        {
            if (homeSet) return;
            homeSet = true;
            signHome = sign.anchoredPosition;
            if (dropY != null) return;
            Vector4 pop = new Vector4(0.3f, 1.5f, 0.5f, 1f);
            float[] ends = { 0f, 1f };
            dropY = CssCurve.Build(ends, new[] { 40f, 0f }, pop);
            dropS = CssCurve.Build(ends, new[] { 0.8f, 1f }, pop);
        }

        /// <summary>Mist instance: only the clouds, not clickable.</summary>
        public void SetupMist()
        {
            foreach (GameObject c in clouds) c.SetActive(true);
            sign.gameObject.SetActive(false);
        }

        /// <summary>Banner instance: only the world sign.</summary>
        public void Setup(int world, string name, int firstLevel, bool locked)
        {
            foreach (GameObject c in clouds) c.SetActive(false);
            sign.gameObject.SetActive(true);
            ClearDrop();
            worldNumber.text = Localizer.F("WORLD {0}", world);
            worldName.text = Localizer.T(name);
            lockBadge.SetActive(false); // locked worlds are shown by their nodes' own locks
        }
    }
}
