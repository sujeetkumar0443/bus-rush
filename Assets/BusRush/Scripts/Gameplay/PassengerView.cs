using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// One rigged passenger (separate Torso / Head / Cap / ArmL / ArmR / LegL / LegR, pivots at the joints).
    /// Colours via MaterialPropertyBlock (cap = darker shade). The pose is driven by <see cref="CrowdView"/> from its
    /// single update loop through <see cref="Pose"/>; there is no Update per person.
    /// </summary>
    public sealed class PassengerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock block;

        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armL;
        [SerializeField] private Transform armR;
        [SerializeField] private Transform legL;
        [SerializeField] private Transform legR;
        [SerializeField] private Renderer[] bodyRenderers = new Renderer[0];
        [SerializeField] private Renderer[] capRenderers = new Renderer[0];

        [Header("Walk (at 85 ref px/s)")]
        [SerializeField] private float walkFrequency = 7f;
        [SerializeField] private float walkSwing = 35f;
        [SerializeField] private float referenceWalkSpeedPx = 85f;
        [Header("Run (boarding)")]
        [SerializeField] private float runFrequency = 10f;
        [SerializeField] private float runSwing = 50f;
        [Header("Body")]
        [SerializeField] private float headBob = 0.01f;
        [SerializeField] private float leanDegrees = 6f;
        [SerializeField] private float rollDegrees = 3f;
        [SerializeField] private float idleArmSway = 1f;
        [SerializeField] private float breathing = 0.02f;

        private Quaternion torsoRot, armLRot, armRRot, legLRot, legRRot;
        private Vector3 headPos, torsoScale;
        private float phase;
        private float idleTime;
        private float blend;

        public GameColor Color { get; private set; }
        public IReadOnlyList<Renderer> BodyRenderers => bodyRenderers;
        public IReadOnlyList<Renderer> CapRenderers => capRenderers;

        private void Awake()
        {
            torsoRot = torso.localRotation;
            armLRot = armL.localRotation;
            armRRot = armR.localRotation;
            legLRot = legL.localRotation;
            legRRot = legR.localRotation;
            headPos = head.localPosition;
            torsoScale = torso.localScale;
            phase = Random.value * Mathf.PI * 2f;   // staggered per person
            idleTime = Random.value * 10f;
        }

        public void SetColor(GameColor color)
        {
            Color = color;
            Tint(bodyRenderers, GameColors.GetColor(color));
            Tint(capRenderers, GameColors.GetCapColor(color));
        }

        private static void Tint(Renderer[] renderers, Color c)
        {
            block ??= new MaterialPropertyBlock();
            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Advances the pose. speedPx = current speed in reference px/s (0 = standing); running = boarding sprint.
        /// Arms swing opposite to the legs on the same side; the torso leans forward and rolls with the step.
        /// </summary>
        public void Pose(float dt, float speedPx, bool running)
        {
            bool moving = speedPx > 1f;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, dt * 8f);
            idleTime += dt;

            float frequency = running
                ? runFrequency
                : walkFrequency * Mathf.Clamp(speedPx / referenceWalkSpeedPx, 0.4f, 1.5f);
            float amplitude = running ? runSwing : walkSwing;
            if (moving) phase += dt * frequency * Mathf.PI * 2f;

            float swing = Mathf.Sin(phase) * amplitude * blend;
            float sway = Mathf.Sin(idleTime * 1.3f) * idleArmSway * (1f - blend);

            armL.localRotation = armLRot * Quaternion.Euler(swing + sway, 0f, 0f);
            armR.localRotation = armRRot * Quaternion.Euler(-swing + sway, 0f, 0f);
            legL.localRotation = legLRot * Quaternion.Euler(-swing, 0f, 0f);
            legR.localRotation = legRRot * Quaternion.Euler(swing, 0f, 0f);

            // The model faces -Z in its own space, so a forward lean is a negative X rotation.
            torso.localRotation = torsoRot * Quaternion.Euler(-leanDegrees * blend, 0f, Mathf.Sin(phase) * rollDegrees * blend);
            head.localPosition = headPos + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * headBob * blend);

            // Breathing scales the torso only: its children (head, arms) are counter-scaled.
            float breathe = 1f + breathing * (1f - blend) * (0.5f + 0.5f * Mathf.Sin(idleTime * 2.2f));
            torso.localScale = new Vector3(torsoScale.x, torsoScale.y * breathe, torsoScale.z);
            var counter = new Vector3(1f, 1f / breathe, 1f);
            head.localScale = counter;
            armL.localScale = counter;
            armR.localScale = counter;
        }

        public void ResetPose()
        {
            blend = 0f;
            torso.localRotation = torsoRot;
            armL.localRotation = armLRot;
            armR.localRotation = armRRot;
            legL.localRotation = legLRot;
            legR.localRotation = legRRot;
            head.localPosition = headPos;
            torso.localScale = torsoScale;
            head.localScale = Vector3.one;
            armL.localScale = Vector3.one;
            armR.localScale = Vector3.one;
        }
    }
}
