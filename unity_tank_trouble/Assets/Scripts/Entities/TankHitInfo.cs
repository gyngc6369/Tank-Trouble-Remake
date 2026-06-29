namespace TankTrouble.Entities
{
    public readonly struct TankHitInfo
    {
        public readonly TankController DeadTank;
        public readonly TankController KillerTank;

        public TankHitInfo(TankController deadTank, TankController killerTank)
        {
            DeadTank = deadTank;
            KillerTank = killerTank;
        }
    }
}
