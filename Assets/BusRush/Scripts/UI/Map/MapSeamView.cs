using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>Tile seam on the Road Map: cloud band + blue world sign ("WORLD n", name, lock badge while locked).</summary>
    public sealed class MapSeamView : MonoBehaviour
    {
        [SerializeField] private Button signButton;
        [SerializeField] private TMP_Text worldNumber;
        [SerializeField] private TMP_Text worldName;
        [SerializeField] private GameObject lockBadge;
        [SerializeField] private TMP_Text lockText;

        public Button Button => signButton;

        public void Setup(int world, string name, int firstLevel, bool locked)
        {
            worldNumber.text = "WORLD " + world;
            worldName.text = name;
            lockBadge.SetActive(locked);
            lockText.text = "Level " + firstLevel;
        }
    }
}
