using UnityEngine;
using TankTrouble.Entities;

namespace TankTrouble.AI
{
    public struct AIMotionPlan
    {
        public TankInputCommand FirstCommand;
        public TankInputCommand SecondCommand;
        public float FirstDuration;
        public float SecondDuration;
        public float Elapsed;
        public float Score;
        public AIMotionIntent Intent;
        public bool Valid;

        public float TotalDuration => FirstDuration + SecondDuration;

        public TankInputCommand CurrentCommand
        {
            get
            {
                if (!Valid)
                    return TankInputCommand.None;

                return Elapsed < FirstDuration ? FirstCommand : SecondCommand;
            }
        }

        public bool Expired => !Valid || Elapsed >= TotalDuration;

        public void Advance(float dt)
        {
            Elapsed = Mathf.Min(TotalDuration, Elapsed + Mathf.Max(0f, dt));
        }

        public static AIMotionPlan Create(TankInputCommand first, float firstDuration, TankInputCommand second, float secondDuration, float score, AIMotionIntent intent)
        {
            return new AIMotionPlan
            {
                FirstCommand = first,
                FirstDuration = Mathf.Max(0f, firstDuration),
                SecondCommand = second,
                SecondDuration = Mathf.Max(0f, secondDuration),
                Score = score,
                Intent = intent,
                Elapsed = 0f,
                Valid = firstDuration > 0f || secondDuration > 0f
            };
        }

        public static AIMotionPlan Invalid => new AIMotionPlan { Valid = false };
    }
}
