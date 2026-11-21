using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>A vehicle on the board: placement from <see cref="VehicleData"/>, colours, roof arrow and feedback animations.</summary>
    public sealed class VehicleView : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform model;
        [SerializeField] private Transform arrow;
        [SerializeField] private Transform contactShadow;
        [SerializeField] private BoxCollider hitBox;
        [Tooltip("Visual scale so neighbouring vehicles show a small gap.")]
        [SerializeField, Range(0.7f, 1f)] private float visualScale = 0.92f;
        [Tooltip("Roof arrow scale (bolder than the modelled one, kept square even when the model is narrowed).")]
        [SerializeField, Range(1f, 2f)] private float arrowScale = 1.35f;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Vector3 modelBaseScale;
        private Coroutine feedback;
        private Coroutine flash;

        public int Id { get; private set; }
        public VehicleData Data { get; private set; }
        public bool IsExiting { get; set; }
        public BoxCollider HitBox => hitBox;
        public VehicleMotion Motion { get; private set; }

        private void Awake()
        {
            Motion = GetComponent<VehicleMotion>();
            renderers = model != null ? model.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
            modelBaseScale = model != null ? model.localScale : Vector3.one;
        }

        /// <summary>Shuffle booster: a quick spin, recoloured halfway (the data's colour changes at once).</summary>
        public void SwirlTo(GameColor color, float duration)
        {
            Data.color = color;
            name = $"{Data.type}_{color}_{Id}";
            StartCoroutine(Swirl(color, duration));
        }

        private System.Collections.IEnumerator Swirl(GameColor color, float duration)
        {
            Quaternion baseRotation = transform.rotation;
            bool recoloured = false;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float spin = 360f * (k * k * (3f - 2f * k));
                transform.rotation = baseRotation * Quaternion.Euler(0f, spin, 0f);
                if (!recoloured && k >= 0.5f)
                {
                    GameColors.Recolor(renderers, color);
                    recoloured = true;
                }
                yield return null;
            }
            transform.rotation = baseRotation;
            if (!recoloured) GameColors.Recolor(renderers, color);
        }

        public void Setup(int id, VehicleData data)
        {
            Id = id;
            Data = data;
            name = $"{data.type}_{data.color}_{id}";
            transform.SetPositionAndRotation(BoardGeometry.VehicleCenter(data), Quaternion.Euler(0f, data.direction.ToYaw(), 0f));
            transform.localScale = Vector3.one;

            GameColors.Recolor(renderers, data.color);

            if (model != null)
            {
                model.localScale = modelBaseScale * visualScale;
            }
            if (arrow != null && model != null)
            {
                // The model may be narrowed on X (see Step 1); compensate so the arrow stays square and bold.
                float squash = model.localScale.z / Mathf.Max(0.0001f, model.localScale.x);
                arrow.localScale = new Vector3(arrowScale * squash, 1f, arrowScale);
            }
        }

        /// <summary>True if a point on the ground (any height) lies over this vehicle's footprint.</summary>
        public bool FootprintContains(Vector3 worldPoint, float margin = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float halfLength = Data.Length * BoardGeometry.CellSize * 0.5f + margin;
            float halfWidth = BoardGeometry.CellSize * 0.5f + margin;
            return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength;
        }

        public void PopIn(float delay)
        {
            transform.localScale = Vector3.zero;
            Tween.Scale(this, transform, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, delay);
        }

        /// <summary>
        /// Marks the vehicle as leaving the board: cancels bump feedback (it would fight the drive). The contact shadow
        /// stays on and follows the vehicle around the road.
        /// </summary>
        public void BeginExit()
        {
            if (feedback != null)
            {
                StopCoroutine(feedback);
                feedback = null;
                transform.position = BoardGeometry.VehicleCenter(Data);
            }
            IsExiting = true;
        }

        /// <summary>Blocked tap: short nudge toward the blocker and back, a quick shake and a small exhaust puff.</summary>
        public void Bump() => StartFeedback(true);

        /// <summary>No free bay: shake in place without moving forward.</summary>
        public void Shake() => StartFeedback(false);

        private void StartFeedback(bool nudge)
        {
            if (IsExiting) return;
            if (feedback != null) StopCoroutine(feedback);
            feedback = StartCoroutine(BumpRoutine(nudge));
            if (Motion != null && Motion.Exhaust != null) Motion.Exhaust.Burst(Data.type == VehicleType.Bus ? 3 : 2);
        }

        private IEnumerator BumpRoutine(bool nudge)
        {
            Vector3 rest = BoardGeometry.VehicleCenter(Data);
            Vector3 dir = Data.direction.ToWorld();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            const float bumpTime = 0.12f;
            const float bumpDistance = 0.22f;

            for (float t = 0f; nudge && t < bumpTime; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / bumpTime * Mathf.PI); // out and back
                transform.position = rest + dir * (bumpDistance * k);
                yield return null;
            }

            const float shakeTime = 0.16f;
            for (float t = 0f; t < shakeTime; t += Time.deltaTime)
            {
                float damp = 1f - t / shakeTime;
                transform.position = rest + side * (Mathf.Sin(t * 70f) * 0.05f * damp);
                yield return null;
            }
            transform.position = rest;
            feedback = null;
        }

        /// <summary>Emissive flash (materials keep _EMISSION enabled with black emission for this).</summary>
        public void Flash(Color color, float duration)
        {
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            block ??= new MaterialPropertyBlock();
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                SetEmission(color * (k * k * 1.6f));
                yield return null;
            }
            foreach (Renderer r in renderers) r.SetPropertyBlock(null);
            flash = null;
        }

        private void SetEmission(Color c)
        {
            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, c);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>World bounds of all renderers (used to know when an exiting vehicle left the view).</summary>
        public Bounds RenderBounds()
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public void SetShadowVisible(bool visible)
        {
            if (contactShadow != null) contactShadow.gameObject.SetActive(visible);
        }
    }
}
