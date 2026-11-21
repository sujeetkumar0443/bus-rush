using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>How a released vehicle leaves the board.</summary>
    public interface IVehicleExitBehaviour
    {
        /// <summary>False if the vehicle must stay (e.g. no free bay).</summary>
        bool CanExit(VehicleView vehicle);

        /// <summary>
        /// Starts the exit. Calls <paramref name="onExited"/> once when the vehicle has finished leaving the board
        /// (arrived in a bay, or gone). The behaviour owns the vehicle from now on.
        /// </summary>
        void Exit(VehicleView vehicle, Action<VehicleView> onExited);
    }

    /// <summary>
    /// Drives the road loop to a free bay and parks there. The trajectory is reserved in the bay manager's
    /// <see cref="TrafficSchedule"/> at tap time; the vehicle waits in its lot cell until its reserved start, then drives
    /// without ever stopping.
    /// </summary>
    public sealed class RoadExitBehaviour : IVehicleExitBehaviour
    {
        private readonly BayManager bays;

        public RoadExitBehaviour(BayManager bays)
        {
            this.bays = bays;
        }

        private VehicleView pendingVehicle;
        private BayState pendingBay;
        private VehiclePlan pendingPlan;

        /// <summary>
        /// A free bay whose whole trajectory can be reserved (in preference order: clean turn-in first). A turn-in that
        /// would clip a parked neighbour can never be reserved, so that bay is skipped; with none left the tap is a
        /// "no free bay" (the vehicle shakes and stays).
        /// </summary>
        public bool CanExit(VehicleView vehicle)
        {
            pendingVehicle = null;
            if (!bays.HasFreeBay) return false;
            float now = Time.time;
            foreach (BayState b in bays.FreeBaysFor(vehicle))
            {
                VehiclePlan p = TrafficSchedule.ArrivalPlan(vehicle.Id, vehicle.Data, b.Index, now);
                if (!bays.Traffic.CanSchedule(p, now)) continue;
                pendingVehicle = vehicle;
                pendingBay = b;
                pendingPlan = p;
                return true;
            }
            return false;
        }

        public void Exit(VehicleView vehicle, Action<VehicleView> onExited)
        {
            if ((pendingVehicle != vehicle || pendingBay == null || !pendingBay.IsFree) && !CanExit(vehicle))
            {
                Debug.LogError("[BusRush] RoadExitBehaviour.Exit called without a reservable bay.");
                return;
            }
            BayState bay = pendingBay;
            VehiclePlan plan = pendingPlan;
            pendingVehicle = null;
            bays.Reserve(bay, vehicle);
            vehicle.StartCoroutine(Drive(vehicle, bay, plan, onExited));
        }

        private IEnumerator Drive(VehicleView vehicle, BayState bay, VehiclePlan plan, Action<VehicleView> onExited)
        {
            float now = Time.time;
            plan.WaitFrom = now;
            if (!bays.Traffic.Schedule(plan, now))
            {
                Debug.LogWarning($"[Traffic] {vehicle.name}: reservation lost ({bays.Traffic.LastFailure}); starting anyway.");
            }
            yield return vehicle.Motion.Run(plan);
            bays.Arrive(bay);
            onExited(vehicle);
        }
    }

    /// <summary>Step 3 placeholder kept for reference/tests: accelerate straight ahead until out of view, then destroy.</summary>
    public sealed class StraightExitBehaviour : IVehicleExitBehaviour
    {
        private readonly Camera camera;
        private readonly float maxSpeed;
        private readonly float accelerationTime;

        public StraightExitBehaviour(Camera camera, float maxSpeed = 14f, float accelerationTime = 0.45f)
        {
            this.camera = camera;
            this.maxSpeed = maxSpeed;
            this.accelerationTime = accelerationTime;
        }

        public bool CanExit(VehicleView vehicle) => true;

        public void Exit(VehicleView vehicle, Action<VehicleView> onExited)
        {
            vehicle.StartCoroutine(Drive(vehicle, onExited));
        }

        private IEnumerator Drive(VehicleView vehicle, Action<VehicleView> onExited)
        {
            Vector3 dir = vehicle.Data.direction.ToWorld();
            var planes = new Plane[6];
            float t = 0f;
            while (t < 5f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / accelerationTime);
                vehicle.transform.position += dir * (maxSpeed * k * k * Time.deltaTime);
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                if (!GeometryUtility.TestPlanesAABB(planes, vehicle.RenderBounds())) break;
                yield return null;
            }
            onExited(vehicle);
            UnityEngine.Object.Destroy(vehicle.gameObject);
        }
    }
}
