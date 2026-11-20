using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Loads a scene through <see cref="SceneLoader"/> when the attached Button is clicked.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SceneLoadButton : MonoBehaviour
    {
        [SerializeField] private string sceneName = SceneLoader.HomeScene;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            Haptics.Short();
            SceneLoader.Load(sceneName);
        }
    }
}
