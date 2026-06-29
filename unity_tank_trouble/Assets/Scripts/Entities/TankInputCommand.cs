namespace TankTrouble.Entities
{
    public readonly struct TankInputCommand
    {
        public readonly float Move;
        public readonly float Rotate;
        public readonly bool FireHeld;

        public TankInputCommand(float move, float rotate, bool fireHeld)
        {
            Move = move;
            Rotate = rotate;
            FireHeld = fireHeld;
        }

        public static TankInputCommand None => new TankInputCommand(0f, 0f, false);
    }
}
