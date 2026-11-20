using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>
    /// The 8 parking bays: bay 0 VIP, then the open bays, then locked ones (counts from GameConfig).
    /// Hands out reservations, parks arriving vehicles and runs departures (reverse out, drive off right, coins).
    /// </summary>
    public sealed class BayManager : MonoBehaviour
    {
        [SerializeField] private BayView bayPrefab;
        [SerializeField] private TMP_FontAsset floatFont;
        [SerializeField] private Material floatMaterial;
        [Tooltip("Reference px from BAY_Y down to the centre of the painted slot.")]
        [SerializeField] private float slotCenterOffsetPx = 30f;
        [SerializeField] private float departSpeedMultiplier = 1.4f;

        private readonly List<BayState> bays = new List<BayState>();
        private int movers;

        public IReadOnlyList<BayState> Bays => bays;

        /// <summary>Vehicles driving to a bay or away from one.</summary>
        public int Movers => movers;

        public event Action<BayState> OnVehicleParked;
        public event Action<BayState> OnVehicleDeparted;

        public void Build()
        {
            foreach (BayState b in bays)
            {
                if (b.View != null) Destroy(b.View.gameObject);
            }
            bays.Clear();

            GameConfig cfg = GameConfig.Instance;
            int open = cfg != null ? cfg.openBays : 5;
            int total = Mathf.Min(ScreenRef.BayCount, cfg != null ? cfg.TotalBays : ScreenRef.BayCount);
            int cost = cfg != null ? cfg.extraBayCost : 200;

            for (int i = 0; i < total; i++)
            {
                BayKind kind = i == 0 ? BayKind.Vip : i < open ? BayKind.Open : BayKind.Locked;
                BayView view = Instantiate(bayPrefab, transform);
                view.transform.SetPositionAndRotation(SlotCenter(i, slotCenterOffsetPx), Quaternion.identity);
                view.Setup(i, kind, cost);
                bays.Add(new BayState { Index = i, Kind = kind, View = view, Owner = this });
            }
        }

        public static Vector3 SlotCenter(int index, float offsetPx = 30f)
        {
            return ScreenRef.RefToWorld(ScreenRef.BayX(index), ScreenRef.BayY + offsetPx);
        }

        public bool HasFreeBay
        {
            get
            {
                foreach (BayState b in bays) if (b.IsFree) return true;
                return false;
            }
        }

        /// <summary>Every open bay holds a parked vehicle (none driving in or backing out).</summary>
        public bool AllOpenOccupied
        {
            get
            {
                bool any = false;
                foreach (BayState b in bays)
                {
                    if (b.Kind != BayKind.Open) continue;
                    any = true;
                    if (!b.IsParked) return false;
                }
                return any;
            }
        }

        public bool AllEmpty
        {
            get
            {
                if (movers > 0) return false;
                foreach (BayState b in bays) if (b.Vehicle != null || b.Reserved || b.Departing) return false;
                return true;
            }
        }

        /// <summary>Reserves the first free open bay for a vehicle about to drive off the board.</summary>
        public BayState ReserveFirstFree(VehicleView vehicle)
        {
            foreach (BayState b in bays)
            {
                if (!b.IsFree) continue;
                b.Vehicle = vehicle;
                b.Reserved = true;
                b.ResetFilled();
                b.View.ShowPill(false);
                movers++;
                return b;
            }
            return null;
        }

        public void Arrive(BayState bay)
        {
            movers = Mathf.Max(0, movers - 1);
            bay.Reserved = false;
            bay.SetFilled(0);
            bay.View.ShowPill(true);
            if (bay.Vehicle != null && bay.Vehicle.Motion != null) bay.Vehicle.Motion.Park();
            OnVehicleParked?.Invoke(bay);
        }

        public void Depart(BayState bay)
        {
            if (!bay.IsParked) return;
            bay.Departing = true;
            bay.View.ShowPill(false);
            movers++;
            StartCoroutine(DepartRoutine(bay));
        }

        private IEnumerator DepartRoutine(BayState bay)
        {
            VehicleView v = bay.Vehicle;
            int coins = bay.Capacity;
            SaveSystem.AddCoins(coins);
            FloatingText.Spawn(SlotCenter(bay.Index, -10f) + Vector3.up * 1.6f, "+" + coins, floatFont, floatMaterial,
                               new Color(1f, 0.79f, 0.24f));

            VehicleMotion motion = v.Motion;
            yield return motion.Drive(RouteBuilder.ToWorld(RouteBuilder.BuildReverseOut(v.Data.type, bay.Index)),
                                      true, 1f, true, true);

            // Out of the slot: the bay can take the next vehicle.
            bay.Vehicle = null;
            bay.Departing = false;

            yield return motion.Drive(RouteBuilder.ToWorld(RouteBuilder.BuildDriveOff(bay.Index)),
                                      false, departSpeedMultiplier, false, true, clampToScreen: false);

            Destroy(v.gameObject);
            movers = Mathf.Max(0, movers - 1);
            OnVehicleDeparted?.Invoke(bay);
        }

        public bool UnlockNextBay()
        {
            foreach (BayState b in bays)
            {
                if (b.Kind != BayKind.Locked) continue;
                b.Kind = BayKind.Open;
                b.View.SetKind(BayKind.Open);
                return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            for (int i = 0; i < ScreenRef.BayCount; i++)
            {
                Vector3 front = ScreenRef.RefToWorld(ScreenRef.BayX(i), ScreenRef.BayY);
                Gizmos.DrawWireCube(SlotCenter(i, slotCenterOffsetPx), new Vector3(0.9f, 0.05f, 2f));
                Gizmos.DrawSphere(front, 0.08f);
            }

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Vector3 tl = ScreenRef.RefToWorld(ScreenRef.LeftX, ScreenRef.TopY), tr = ScreenRef.RefToWorld(ScreenRef.RightX, ScreenRef.TopY);
            Vector3 bl = ScreenRef.RefToWorld(ScreenRef.LeftX, ScreenRef.BotY), br = ScreenRef.RefToWorld(ScreenRef.RightX, ScreenRef.BotY);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);
        }
    }
}
