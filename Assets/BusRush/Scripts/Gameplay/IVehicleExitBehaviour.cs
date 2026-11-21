using System;
using System.Collections;
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

    /// <summary>Drives the road loop to the first free bay and parks there (Step 4).</summary>
    public sealed class RoadExitBehaviour : IVehicleExitBehaviour
    {
        private readonly BayManager bays;

        public RoadExitBehaviour(BayManager bays)
        {
            this.bays = bays;
        }

        public bool CanExit(VehicleView vehicle) => bays.HasFreeBay;

        public void Exit(VehicleView vehicle, Action<VehicleView> onExited)
        {
            BayState bay = bays.ReserveFirstFree(vehicle);
            if (bay == null)
            {
                Debug.LogError("[BusRush] RoadExitBehaviour.Exit called without a free bay.");
                return;
            }
            vehicle.StartCoroutine(Drive(vehicle, bay, onExited));
        }

        private IEnumerator Drive(VehicleView vehicle, BayState bay, Action<VehicleView> onExited)
        {
            var route = RouteBuilder.ToWorld(RouteBuilder.Build(vehicle.Data, bay.Index));
            yield return vehicle.Motion.Drive(route, false, GameTuning.VehicleDriveSpeed, true, true);
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
