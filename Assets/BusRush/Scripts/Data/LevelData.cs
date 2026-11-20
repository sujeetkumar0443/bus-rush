using System;
using System.Collections.Generic;

namespace BusRush
{
    /// <summary>A generated level: the vehicles on the board plus how it was made.</summary>
    [Serializable]
    public sealed class LevelData
    {
        public int level;
        public int seed;
        public int worldIndex;
        public int columns = 9;
        public int rows = 10;
        public List<VehicleData> vehicles = new List<VehicleData>();

        /// <summary>
        /// Total seat capacity per colour. The crowd (later step) spawns exactly this many passengers of each colour,
        /// so every vehicle can be filled.
        /// </summary>
        public Dictionary<GameColor, int> PassengerCountPerColor()
        {
            var result = new Dictionary<GameColor, int>();
            foreach (VehicleData v in vehicles)
            {
                result.TryGetValue(v.color, out int n);
                result[v.color] = n + v.Capacity;
            }
            return result;
        }

        public int TotalPassengers()
        {
            int sum = 0;
            foreach (VehicleData v in vehicles) sum += v.Capacity;
            return sum;
        }
    }
}
