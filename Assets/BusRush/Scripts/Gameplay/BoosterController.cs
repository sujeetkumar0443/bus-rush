using System;
using System.Collections;
using System.Collections.Generic;
using BusRush.UI;
using UnityEngine;

namespace BusRush.Gameplay
{
    public enum BoosterOutcome
    {
        /// <summary>Applied; one was spent.</summary>
        Used,
        /// <summary>Nothing left (the UI offers to buy).</summary>
        NoneLeft,
        /// <summary>Not unlocked at this level.</summary>
        Locked,
        /// <summary>Vehicles moving / passengers boarding for longer than the wait (button shakes, nothing spent).</summary>
        Busy,
        /// <summary>Nothing to do (VIP already open, queue already arranged, no winnable shuffle): nothing spent.</summary>
        NoEffect
    }

    /// <summary>
    /// Applies the boosters in a running level (added by <see cref="GameSession"/>). Each one waits for a calm board (no
    /// vehicle waiting / driving, nobody walking to a vehicle, no bay mid-arrival or departure) for up to
    /// <see cref="GameTuning.BoosterCalmWaitSeconds"/>, applies the rule from <see cref="BoosterLogic"/> (which keeps the
    /// level winnable) and only then spends one.
    /// </summary>
    public sealed class BoosterController : MonoBehaviour
    {
        public static BoosterController Current { get; private set; }

        private GameSession session;
        private bool busy;
        private readonly System.Random rng = new System.Random();

        public void Init(GameSession owner)
        {
            session = owner;
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>No vehicle waiting or driving, nobody walking to a vehicle, no bay between states.</summary>
        public bool IsCalm
        {
            get
            {
                if (session == null || VehicleMotion.AnyActive || session.Crowd.WalkerCount > 0 || session.Bays.Movers > 0) return false;
                foreach (BayState b in session.Bays.Bays)
                {
                    if (b.Reserved || b.Departing || b.Full || b.SeatsReserved > 0) return false;
                }
                return true;
            }
        }

        /// <summary>Uses a booster (after waiting for a calm board); done reports what happened.</summary>
        public void Request(BoosterType type, Action<BoosterOutcome> done)
        {
            if (session == null || busy || session.IsCleared)
            {
                done?.Invoke(BoosterOutcome.Busy);
                return;
            }
            if (!BoosterInventory.IsUnlocked(type, session.Level))
            {
                done?.Invoke(BoosterOutcome.Locked);
                return;
            }
            if (BoosterInventory.Count(type) <= 0)
            {
                done?.Invoke(BoosterOutcome.NoneLeft);
                return;
            }
            if (type == BoosterType.Vip && session.Bays.VipOpen)
            {
                done?.Invoke(BoosterOutcome.NoEffect); // already open: shake, nothing spent
                return;
            }
            StartCoroutine(Run(type, done));
        }

        private IEnumerator Run(BoosterType type, Action<BoosterOutcome> done)
        {
            busy = true;
            float deadline = Time.unscaledTime + GameTuning.BoosterCalmWaitSeconds;
            while (!IsCalm && Time.unscaledTime < deadline) yield return null;
            BoosterOutcome outcome = IsCalm ? Apply(type) : BoosterOutcome.Busy;
            busy = false;
            done?.Invoke(outcome);
        }

        private BoosterOutcome Apply(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Vip:
                    if (!session.Bays.OpenVipBay()) return BoosterOutcome.NoEffect;
                    Spend(type);
                    Toast.Show(Localizer.T("VIP bay open!"));
                    return BoosterOutcome.Used;

                case BoosterType.Arrange:
                {
                    LevelSnapshot snap = Snapshot();
                    List<GameColor> arranged = BoosterLogic.Arrange(snap, GameTuning.FrontWindow, out bool changed);
                    if (!changed)
                    {
                        Toast.Show(Localizer.T("The line is already in order."));
                        return BoosterOutcome.NoEffect;
                    }
                    session.Crowd.Rearrange(arranged, GameTuning.ArrangeAnimSeconds);
                    Spend(type);
                    return BoosterOutcome.Used;
                }

                case BoosterType.Shuffle:
                {
                    LevelSnapshot snap = Snapshot();
                    Dictionary<int, GameColor> colours = BoosterLogic.Shuffle(snap, rng, GameTuning.ShuffleRetries);
                    if (colours == null)
                    {
                        // No winnable new colouring in 20 tries: colours stay, the booster is not spent (refund).
                        Toast.Show(Localizer.T("No better shuffle right now - booster kept."));
                        return BoosterOutcome.NoEffect;
                    }
                    foreach (VehicleView v in session.Board.Vehicles)
                    {
                        if (colours.TryGetValue(v.Id, out GameColor c) && c != v.Data.color) v.SwirlTo(c, GameTuning.ShuffleAnimSeconds);
                    }
                    Spend(type);
                    return BoosterOutcome.Used;
                }
            }
            return BoosterOutcome.NoEffect;
        }

        private static void Spend(BoosterType type)
        {
            BoosterInventory.Use(type);
            Sfx.Play("booster_" + type.ToString().ToLowerInvariant());
            Haptics.Light();
        }

        /// <summary>The level as it stands now, for the winnability check.</summary>
        private LevelSnapshot Snapshot()
        {
            LevelData level = session.LevelData;
            var snap = new LevelSnapshot
            {
                columns = level.columns,
                rows = level.rows,
                vehicles = level.vehicles,
                exitOrder = level.exitOrder,
                queue = session.Crowd.QueueColors(),
                usableBays = session.Bays.UsableCount
            };
            foreach (VehicleView v in session.Board.Vehicles) snap.onBoard.Add(v.Id);
            foreach (BayState b in session.Bays.Bays)
            {
                if (!b.IsParked || b.Full) continue;
                snap.parked.Add(new GameSimulator.ParkedState
                {
                    color = b.Vehicle.Data.color,
                    capacity = b.Capacity,
                    filled = b.Filled + b.SeatsReserved
                });
            }
            return snap;
        }
    }
}
