using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// TEMPORARY (Step 1): spawns every prefab in all 7 colours on the lot to check scale and colours.
    /// Remove in Step 3.
    /// </summary>
    public sealed class TestPrefabSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject carPrefab;
        [SerializeField] private GameObject vanPrefab;
        [SerializeField] private GameObject busPrefab;
        [SerializeField] private GameObject passengerPrefab;

        private void Start()
        {
            GameConfig config = GameConfig.Instance;
            float cell = config != null ? config.cellSize : 1f;
            int height = config != null ? config.gridHeight : 10;

            // Lot is centred on this transform; rows are laid out from the top (+Z) edge downwards.
            float top = height * 0.5f * cell;
            float busZ = top - 1.5f * cell;           // rows 0-2
            float vanZ = busZ - 2.5f * cell;          // rows 3-4
            float carZ = vanZ - 2f * cell;            // rows 5-6
            float passengerZ = carZ - 2f * cell;      // row 8

            for (int i = 0; i < GameColors.Count; i++)
            {
                var color = (GameColor)i;
                float x = (i - (GameColors.Count - 1) * 0.5f) * cell;

                Spawn(busPrefab, color, x, busZ);
                Spawn(vanPrefab, color, x, vanZ);
                Spawn(carPrefab, color, x, carZ);

                GameObject passenger = Spawn(passengerPrefab, color, x, passengerZ);
                if (passenger != null)
                {
                    Transform cap = passenger.transform.Find("Cap");
                    if (cap != null && cap.TryGetComponent(out Renderer capRenderer))
                    {
                        GameColors.SetTint(capRenderer, GameColors.GetCapColor(color));
                    }
                }
            }
        }

        private GameObject Spawn(GameObject prefab, GameColor color, float x, float z)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject go = Instantiate(prefab, transform);
            go.name = $"{prefab.name}_{color}";
            go.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.identity);
            GameColors.Recolor(go.GetComponentsInChildren<Renderer>(), color);
            return go;
        }
    }
}
