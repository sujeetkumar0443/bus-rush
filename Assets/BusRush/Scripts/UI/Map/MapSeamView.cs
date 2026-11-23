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

        /// <summary>Banner size in design px (a node box must stay clear of it).</summary>
        public const float BannerWidth = 220f, BannerHeight = 60f;

        /// <summary>The banner's centre inside the seam rect (x from its left edge).</summary>
        public const float BannerCentreX = 195f;

        public Button Button => signButton;

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
            worldNumber.text = Localizer.F("WORLD {0}", world);
            worldName.text = Localizer.T(name);
            lockBadge.SetActive(false); // locked worlds are shown by their nodes' own locks
        }
    }
}
