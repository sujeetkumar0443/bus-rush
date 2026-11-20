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

        /// <summary>Vehicle indices in the order the generator guarantees they can leave the board.</summary>
        public List<int> exitOrder = new List<int>();

        /// <summary>Passenger colours, front of the queue first. Per colour it sums to that colour's vehicle capacity.</summary>
        public List<GameColor> queue = new List<GameColor>();

        public Dictionary<GameColor, int> QueueCountPerColor()
        {
            var result = new Dictionary<GameColor, int>();
            foreach (GameColor c in queue)
            {
                result.TryGetValue(c, out int n);
                result[c] = n + 1;
            }
            return result;
        }

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
