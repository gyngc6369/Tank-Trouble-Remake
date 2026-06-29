using UnityEngine;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public readonly struct HitSolution
    {
        public readonly Vector2 Direction;
        public readonly TankController Target;
        public readonly int Bounces;
        public readonly float PathLength;
        public readonly float AngleError;
        public readonly float SelfRisk;

        public HitSolution(Vector2 direction, TankController target, int bounces, float pathLength, float angleError, float selfRisk = 0f)
        {
            Direction = direction;
            Target = target;
            Bounces = bounces;
            PathLength = pathLength;
            AngleError = angleError;
            SelfRisk = selfRisk;
        }
    }
}
