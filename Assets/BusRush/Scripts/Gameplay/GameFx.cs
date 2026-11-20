using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>Pooled one-shot effects: win confetti (7 palette colours) and the small boarding sparkle.</summary>
    public sealed class GameFx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem confetti;
        [SerializeField] private ParticleSystem sparkle;
        [SerializeField] private int confettiCount = 140;
        [SerializeField] private int sparkleCount = 8;

        /// <summary>Confetti burst from just above the lot, across the screen width.</summary>
        public void Confetti()
        {
            if (confetti == null) return;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < confettiCount; i++)
            {
                Vector3 pos = ScreenRef.RefToWorld(Random.Range(20f, ScreenRef.Width - 20f), Random.Range(380f, 440f));
                pos.y = Random.Range(2f, 4f);
                p.position = pos;
                p.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 8f), Random.Range(-1.5f, 3f));
                p.startColor = GameColors.GetColor((GameColor)(i % GameColors.Count));
                p.applyShapeToPosition = false;
                confetti.Emit(p, 1);
            }
        }

        public void Sparkle(Vector3 position, Color color)
        {
            if (sparkle == null) return;
            var p = new ParticleSystem.EmitParams { position = position, startColor = color, applyShapeToPosition = true };
            sparkle.Emit(p, sparkleCount);
        }
    }
}
