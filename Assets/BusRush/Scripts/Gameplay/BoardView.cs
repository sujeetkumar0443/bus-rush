using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    public enum TapResult
    {
        Ignored,
        Moved,
        Blocked,
        NoBay
    }

    /// <summary>Spawns the level's vehicles, owns the <see cref="BoardState"/> and resolves taps.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private VehicleView carPrefab;
        [SerializeField] private VehicleView vanPrefab;
        [SerializeField] private VehicleView busPrefab;
        [SerializeField] private float popStagger = 0.03f;
        [SerializeField] private Color blockedFlashColor = new Color(1f, 0.12f, 0.08f);
        [SerializeField] private float blockedFlashTime = 0.25f;

        private readonly Dictionary<int, VehicleView> vehicles = new Dictionary<int, VehicleView>();
        private BoardState state;
        private IVehicleExitBehaviour exitBehaviour;

        /// <summary>A vehicle started driving off the board (its cells are already free).</summary>
        public event Action<VehicleView> VehicleLeftBoard;

        /// <summary>The exit behaviour finished with a vehicle (e.g. it parked in a bay).</summary>
        public event Action<VehicleView> VehicleExited;

        /// <summary>The last vehicle left the board (others may still be on the road / in bays).</summary>
        public event Action BoardCleared;

        public BoardState State => state;
        public int RemainingCount => vehicles.Count;
        /// <summary>Vehicles on the board (struct enumerator: no allocation in foreach).</summary>
        public Dictionary<int, VehicleView>.ValueCollection Vehicles => vehicles.Values;

        public void Build(LevelData level, IVehicleExitBehaviour exit)
        {
            Clear();
            exitBehaviour = exit;
            state = new BoardState(level.columns, level.rows);

            for (int i = 0; i < level.vehicles.Count; i++)
            {
                VehicleData data = level.vehicles[i];
                VehicleView prefab = data.type switch
                {
                    VehicleType.Bus => busPrefab,
                    VehicleType.Van => vanPrefab,
                    _ => carPrefab
                };
                VehicleView view = Instantiate(prefab, transform);
                view.Setup(i, data);
                state.Occupy(i, data);
                vehicles.Add(i, view);
                view.PopIn(i * popStagger);
            }
        }

        public bool IsPathClear(VehicleView v) => state != null && !v.IsExiting && state.IsPathClear(v.Data);

        public bool CanExit(VehicleView v) => exitBehaviour != null && exitBehaviour.CanExit(v);

        public TapResult TryMove(VehicleView v)
        {
            if (v == null || v.IsExiting || state == null || !vehicles.ContainsKey(v.Id)) return TapResult.Ignored;

            if (!state.IsPathClear(v.Data, out int blockerId))
            {
                v.Bump();
                if (vehicles.TryGetValue(blockerId, out VehicleView blocker))
                {
                    blocker.Flash(blockedFlashColor, blockedFlashTime);
                }
                return TapResult.Blocked;
            }

            if (!exitBehaviour.CanExit(v))
            {
                v.Shake();
                return TapResult.NoBay;
            }

            // Leaves the board right now: cells free, no longer tappable, owned by the exit behaviour.
            state.Free(v.Id, v.Data);
            vehicles.Remove(v.Id);
            v.BeginExit();
            v.transform.SetParent(null, true);
            VehicleLeftBoard?.Invoke(v);
            exitBehaviour.Exit(v, done => VehicleExited?.Invoke(done));
            if (vehicles.Count == 0) BoardCleared?.Invoke();
            return TapResult.Moved;
        }

        /// <summary>Debug: removes every vehicle still on the board at once.</summary>
        public void DebugClear()
        {
            if (vehicles.Count == 0) return;
            foreach (VehicleView v in new List<VehicleView>(vehicles.Values))
            {
                state.Free(v.Id, v.Data);
                Destroy(v.gameObject);
            }
            vehicles.Clear();
            BoardCleared?.Invoke();
        }

        private void Clear()
        {
            foreach (VehicleView v in vehicles.Values)
            {
                if (v != null) Destroy(v.gameObject);
            }
            vehicles.Clear();
        }
    }
}
