using System;
using System.Collections;
using UnityEngine;

namespace BusRush.Gameplay
{
    /// <summary>How a released vehicle leaves the board. Step 4 replaces the straight exit with the road route to the bays.</summary>
    public interface IVehicleExitBehaviour
    {
        /// <summary>Starts the exit; must call <paramref name="onExited"/> exactly once when the vehicle is gone.</summary>
        void Exit(VehicleView vehicle, Action<VehicleView> onExited);
    }

    /// <summary>PLACEHOLDER exit: accelerate straight ahead until out of the camera view.</summary>
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

        public void Exit(VehicleView vehicle, Action<VehicleView> onExited)
        {
            vehicle.StartCoroutine(Drive(vehicle, onExited));
        }

        private IEnumerator Drive(VehicleView vehicle, Action<VehicleView> onExited)
        {
            Vector3 dir = vehicle.Data.direction.ToWorld();
            var planes = new Plane[6];
            float t = 0f;
            const float safetyTime = 5f;

            while (t < safetyTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / accelerationTime);
                float speed = maxSpeed * k * k; // ease-in
                vehicle.transform.position += dir * (speed * Time.deltaTime);

                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                if (!GeometryUtility.TestPlanesAABB(planes, vehicle.RenderBounds())) break;
                yield return null;
            }
            onExited(vehicle);
        }
    }
}
