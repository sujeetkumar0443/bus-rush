using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Gameplay HUD (UI v2, Docs/UI_v2_Design/HUD.dc.html): pause, level badge with a live passengers progress bar
    /// (boarded / total, smooth fill), coin pill, booster bar and locked-bay tags (<see cref="BayLockOverlay"/>).
    /// </summary>
    public sealed class GameHUD : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text progressText;
        [Tooltip("Smallest visible fill width (the rounded ends), UI units.")]
        [SerializeField] private float minFillWidth = 14f;
        [Tooltip("Fill animation speed (fraction of the bar per second at most).")]
        [SerializeField] private float fillSpeed = 1.5f;
        [SerializeField] private LevelBanner banner;

        private float shownFraction = -1f;
        private int shownBoarded = -1, shownTotal = -1;

        private void Awake()
        {
            pauseButton.onClick.AddListener(OpenPause);
        }

        private void OnEnable()
        {
            SaveSystem.Changed += Refresh;
            Refresh();
        }

        private System.Collections.IEnumerator Start()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup += OpenPause;

            // Level intro banner, once the session has picked its level.
            yield return null;
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            if (banner != null && session != null && session.Level > 0)
            {
                Refresh();
                banner.Play(session.Level, LevelPlan.Get(session.Level).tier);
            }
        }

        private void OnDisable()
        {
            SaveSystem.Changed -= Refresh;
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null) UIManager.Instance.BackPressedWithNoPopup -= OpenPause;
        }

        private void Update()
        {
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            int total = session != null ? session.TotalPassengers : 0;
            int boarded = session != null ? Mathf.Min(session.BoardedCount, total) : 0;
            if (boarded != shownBoarded || total != shownTotal)
            {
                shownBoarded = boarded;
                shownTotal = total;
                progressText.text = $"{boarded} / {total}";
            }

            float target = total > 0 ? boarded / (float)total : 0f;
            shownFraction = shownFraction < 0f ? target : Mathf.MoveTowards(shownFraction, target, fillSpeed * Time.unscaledDeltaTime);
            var area = (RectTransform)progressFill.parent;
            float w = shownFraction <= 0f ? 0f : Mathf.Max(minFillWidth, shownFraction * area.rect.width);
            progressFill.sizeDelta = new Vector2(w, progressFill.sizeDelta.y);
            progressFill.gameObject.SetActive(w > 0f);
        }

        private void Refresh()
        {
            // The session's level, not the save: the save already points at the next level once the board is cleared.
            Gameplay.GameSession session = Gameplay.GameSession.Current;
            int level = session != null && session.Level > 0 ? session.Level : SaveSystem.Data.level;
            levelText.text = "Level " + level;
        }

        private static void OpenPause()
        {
            if (DebugOnly.Swallowing) return; // the long-press that toggles the debug panel ends on this button
            if (UIManager.Instance != null && !UIManager.Instance.IsOpen<PausePopup>()) UIManager.Instance.Open<PausePopup>();
        }
    }
}
