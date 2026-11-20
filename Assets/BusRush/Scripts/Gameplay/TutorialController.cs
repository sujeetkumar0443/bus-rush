using System.Collections;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// Tutorial hints for levels 1-3 (once per level, saved on completion) and an idle hint on every level: after 8 s
    /// without input the hand softly points at a good move (a free vehicle matching the queue front, else any free one).
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        private enum Step
        {
            None,
            L1TapCar,
            L1Board,
            L2Blocked,
            L2Unblock,
            L3WaitPark,
            L3Pill,
            Done
        }

        public const string TapCarText = "Tap a car to drive it out!";
        public const string BoardText = "Passengers board the same colour!";
        public const string BlockedText = "Blocked! Free the path first.";
        public const string UnblockText = "Tap this one first!";
        public const string PillText = "Fill every seat to send the bus away!";

        [SerializeField] private GameSession session;
        [SerializeField] private InputController input;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float idleDelay = 8f;
        [SerializeField] private float introDelay = 1.6f;
        [SerializeField] private float blockedHintSeconds = 2.5f;

        private Step step;
        private int level;
        private bool tutorialLevel;
        private VehicleView target;
        private VehicleView blocker;
        private BayState pillBay;
        private float stepStart;
        private float lastInput;
        private bool idleShowing;
        private VehicleView idleTarget;
        private float nextIdleRetarget;

        public bool IsTutorialActive => step != Step.None && step != Step.Done;

        private void Start()
        {
            lastInput = Time.unscaledTime;
            session.OnTap += HandleTap;
            session.OnVehicleParked += HandleParked;
            session.OnLevelCleared += HandleCleared;
            input.AnyPointerDown += HandleAnyInput;
            StartCoroutine(Begin());
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.OnTap -= HandleTap;
                session.OnVehicleParked -= HandleParked;
                session.OnLevelCleared -= HandleCleared;
            }
            if (input != null) input.AnyPointerDown -= HandleAnyInput;
        }

        private IEnumerator Begin()
        {
            yield return new WaitForSeconds(introDelay); // after the intro (pop-in, crowd walk-in, banner)
            level = session.Level;
            tutorialLevel = level <= 3 && !SaveSystem.Data.IsTutorialDone(level);
            if (!tutorialLevel) yield break;

            switch (level)
            {
                case 1:
                    SetStep(Step.L1TapCar);
                    break;
                case 2:
                    if (FindBlockedPair(out target, out blocker)) SetStep(Step.L2Blocked);
                    break;
                case 3:
                    SetStep(Step.L3WaitPark);
                    break;
            }
        }

        private void SetStep(Step s)
        {
            step = s;
            stepStart = Time.unscaledTime;
            HintView hint = HintView.Instance;
            if (hint != null && (s == Step.Done || s == Step.None || s == Step.L3WaitPark)) hint.Hide();
        }

        private void Update()
        {
            HintView hint = HintView.Instance;
            if (hint == null) return;
            if (session.IsCleared || session.IsLost || (UIManager.Instance != null && UIManager.Instance.OpenCount > 0))
            {
                if (hint.IsShowing) hint.Hide();
                return;
            }

            switch (step)
            {
                case Step.L1TapCar:
                    if (target == null || target.IsExiting) target = GoodMove();
                    if (target != null) PointAt(hint, target.transform.position, TapCarText);
                    break;
                case Step.L1Board:
                    PointAt(hint, session.Crowd.FrontWorldPosition, BoardText);
                    if (session.BoardedCount > 0) SetStep(Step.Done);
                    break;
                case Step.L2Blocked:
                    if (target == null || target.IsExiting) { SetStep(Step.Done); break; }
                    PointAt(hint, target.transform.position, BlockedText);
                    if (Time.unscaledTime - stepStart > blockedHintSeconds) SetStep(Step.L2Unblock);
                    break;
                case Step.L2Unblock:
                    if (blocker == null || blocker.IsExiting) { SetStep(Step.Done); break; }
                    PointAt(hint, blocker.transform.position, UnblockText);
                    break;
                case Step.L3Pill:
                    if (pillBay == null || pillBay.Vehicle == null || pillBay.Full || pillBay.Departing) { SetStep(Step.Done); break; }
                    hint.HideHand();
                    hint.ShowBubble(PillText, ScreenOf(pillBay.View.PillWorldPosition, 0f));
                    break;
                default:
                    UpdateIdle(hint);
                    break;
            }
        }

        private void UpdateIdle(HintView hint)
        {
            if (Time.unscaledTime - lastInput < idleDelay)
            {
                if (idleShowing)
                {
                    hint.HideHand();
                    idleShowing = false;
                }
                return;
            }

            if (idleTarget == null || idleTarget.IsExiting || Time.unscaledTime >= nextIdleRetarget)
            {
                idleTarget = GoodMove();
                nextIdleRetarget = Time.unscaledTime + 1f;
            }
            if (idleTarget == null)
            {
                hint.HideHand();
                idleShowing = false;
                return;
            }
            hint.HideBubble();
            hint.ShowHand(ScreenOf(idleTarget.transform.position, 0.6f), true);
            idleShowing = true;
        }

        private void PointAt(HintView hint, Vector3 world, string text)
        {
            Vector2 p = ScreenOf(world, 0.6f);
            hint.ShowHand(p);
            hint.ShowBubble(text, p);
        }

        private Vector2 ScreenOf(Vector3 world, float height) => viewCamera.WorldToScreenPoint(world + Vector3.up * height);

        // ------------------------------------------------------------------ events

        private void HandleAnyInput()
        {
            lastInput = Time.unscaledTime;
            if (idleShowing && HintView.Instance != null)
            {
                HintView.Instance.HideHand();
                idleShowing = false;
            }
        }

        private void HandleTap(VehicleView v, TapResult result)
        {
            switch (step)
            {
                case Step.L1TapCar:
                    if (result == TapResult.Moved) SetStep(Step.L1Board);
                    else if (v != target) SetStep(Step.Done); // tapped something else: drop the hint
                    break;
                case Step.L2Blocked:
                case Step.L2Unblock:
                    if (result == TapResult.Moved || (v != target && v != blocker)) SetStep(Step.Done);
                    break;
            }
        }

        private void HandleParked(BayState bay)
        {
            if (step != Step.L3WaitPark) return;
            pillBay = bay;
            SetStep(Step.L3Pill);
        }

        private void HandleCleared()
        {
            if (!tutorialLevel) return;
            SaveSystem.Data.MarkTutorialDone(level);
            SaveSystem.Commit();
        }

        // ------------------------------------------------------------------ move picking

        /// <summary>A free vehicle that matches the queue front, otherwise any free vehicle (needs a free bay).</summary>
        public VehicleView GoodMove()
        {
            if (!session.Bays.HasFreeBay) return null;
            bool hasFront = session.Crowd.HasFront;
            GameColor front = hasFront ? session.Crowd.FrontColor : GameColor.Red;
            VehicleView any = null;
            foreach (VehicleView v in session.Board.Vehicles)
            {
                if (!session.Board.IsPathClear(v)) continue;
                if (hasFront && v.Data.color == front) return v;
                if (any == null) any = v;
            }
            return any;
        }

        /// <summary>A blocked vehicle whose first blocker can leave right now (BoardState path check).</summary>
        private bool FindBlockedPair(out VehicleView blocked, out VehicleView freeBlocker)
        {
            BoardState state = session.Board.State;
            foreach (VehicleView v in session.Board.Vehicles)
            {
                if (state.IsPathClear(v.Data, out int blockerId)) continue;
                foreach (VehicleView b in session.Board.Vehicles)
                {
                    if (b.Id != blockerId || !session.Board.IsPathClear(b)) continue;
                    blocked = v;
                    freeBlocker = b;
                    return true;
                }
            }
            blocked = freeBlocker = null;
            return false;
        }
    }
}
