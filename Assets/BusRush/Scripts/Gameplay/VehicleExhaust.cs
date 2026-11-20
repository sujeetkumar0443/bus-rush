using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Speed-driven diesel smoke on the exhaust particle system (emission is fully script-controlled):
    /// off on the board, a stream while driving, a burst when pulling away, rare puffs while idling in a bay.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class VehicleExhaust : MonoBehaviour
    {
        public enum Mode
        {
            Off,
            Idle,
            Driving
        }

        [SerializeField] private float drivingRateMin = 6f;
        [SerializeField] private float drivingRateMax = 13f;
        [SerializeField] private Vector2 idleInterval = new Vector2(1.5f, 3f);

        private ParticleSystem ps;
        private Mode mode = Mode.Off;
        private float speed01;
        private float carry;
        private float nextIdlePuff;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            ParticleSystem.EmissionModule e = ps.emission;
            e.enabled = false; // we emit manually
            if (!ps.isPlaying) ps.Play();
        }

        public void SetMode(Mode m)
        {
            if (m == Mode.Idle && mode != Mode.Idle) nextIdlePuff = Time.time + Random.Range(idleInterval.x, idleInterval.y);
            mode = m;
        }

        public void SetSpeed(float normalized) => speed01 = Mathf.Clamp01(normalized);

        public void Burst(int count)
        {
            if (ps != null && count > 0) ps.Emit(count);
        }

        private void Update()
        {
            switch (mode)
            {
                case Mode.Driving:
                    carry += Mathf.Lerp(drivingRateMin, drivingRateMax, speed01) * Time.deltaTime;
                    if (carry >= 1f)
                    {
                        int n = Mathf.FloorToInt(carry);
                        carry -= n;
                        ps.Emit(n);
                    }
                    break;
                case Mode.Idle:
                    if (Time.time >= nextIdlePuff)
                    {
                        ps.Emit(1);
                        nextIdlePuff = Time.time + Random.Range(idleInterval.x, idleInterval.y);
                    }
                    break;
            }
        }
    }
}
