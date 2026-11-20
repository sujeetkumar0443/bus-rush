using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusRush.Gameplay
{
    public enum TapResult
    {
        Ignored,
        Moved,
        Blocked
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

        public event Action<VehicleView> VehicleExited;
        public event Action BoardCleared;

        public BoardState State => state;
        public int RemainingCount => vehicles.Count;
        public IEnumerable<VehicleView> Vehicles => vehicles.Values;

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

        public TapResult TryMove(VehicleView v)
        {
            if (v == null || v.IsExiting || state == null || !vehicles.ContainsKey(v.Id)) return TapResult.Ignored;

            if (state.IsPathClear(v.Data, out int blockerId))
            {
                state.Free(v.Id, v.Data); // cells are free immediately so other vehicles can follow
                v.BeginExit();
                exitBehaviour.Exit(v, OnExited);
                return TapResult.Moved;
            }

            v.Bump();
            if (vehicles.TryGetValue(blockerId, out VehicleView blocker))
            {
                blocker.Flash(blockedFlashColor, blockedFlashTime);
            }
            return TapResult.Blocked;
        }

        private void OnExited(VehicleView v)
        {
            if (!vehicles.Remove(v.Id)) return;
            VehicleExited?.Invoke(v);
            Destroy(v.gameObject);
            if (vehicles.Count == 0) BoardCleared?.Invoke();
        }

        /// <summary>Debug: removes every vehicle at once and reports the board as cleared.</summary>
        public void DebugClear()
        {
            if (vehicles.Count == 0) return;
            foreach (VehicleView v in new List<VehicleView>(vehicles.Values))
            {
                state.Free(v.Id, v.Data);
                OnExited(v);
            }
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
