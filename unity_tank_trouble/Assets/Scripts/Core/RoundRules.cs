using System.Collections.Generic;
using TankTrouble.Entities;

namespace TankTrouble.Core
{
    public static class RoundRules
    {
        public static bool ShouldEndRound(int aliveCount)
        {
            return aliveCount <= 1;
        }

        public static TankController GetSoleSurvivor(IReadOnlyList<TankController> tanks)
        {
            TankController survivor = null;
            var aliveCount = 0;

            for (var i = 0; i < tanks.Count; i++)
            {
                var tank = tanks[i];
                if (tank == null || !tank.Alive)
                    continue;

                survivor = tank;
                aliveCount++;
                if (aliveCount > 1)
                    return null;
            }

            return aliveCount == 1 ? survivor : null;
        }
    }
}
