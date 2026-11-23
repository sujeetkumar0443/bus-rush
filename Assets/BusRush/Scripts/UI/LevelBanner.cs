using System.Collections;
using TMPro;
using UnityEngine;

namespace BusRush.UI
{
    /// <summary>"Level N" banner that slides in from the left, holds, and slides out to the right at level start.</summary>
    public sealed class LevelBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform banner;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private float slideDistance = 520f;
        [SerializeField] private float hold = 0.9f;

        private void Awake()
        {
            banner.gameObject.SetActive(false);
        }

        public void Play(int level, LevelTier tier)
        {
            title.text = Localizer.F("Level {0}", level);
            bool boss = tier == LevelTier.Boss;
            subtitle.gameObject.SetActive(boss);
            if (boss) subtitle.text = Localizer.T("BOSS LEVEL");
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            banner.gameObject.SetActive(true);
            Vector2 center = Vector2.zero;
            Vector2 left = new Vector2(-slideDistance, 0f);
            Vector2 right = new Vector2(slideDistance, 0f);
            yield return Tween.Move(this, banner, left, center, 0.35f, Ease.OutBack); // waits for the slide
            yield return new WaitForSecondsRealtime(hold);
            yield return Tween.Move(this, banner, center, right, 0.3f, Ease.InQuad);
            banner.gameObject.SetActive(false);
        }
    }
}
