using UnityEngine;

namespace TankTrouble.Entities
{
    [RequireComponent(typeof(TankController))]
    public sealed class PlayerTankInput : MonoBehaviour
    {
        [SerializeField] private PlayerIndex player = PlayerIndex.Player1;

        private TankController tank;

        private void Awake()
        {
            tank = GetComponent<TankController>();
        }

        private void Update()
        {
            tank.SetCommand(ReadInput());
        }

        private TankInputCommand ReadInput()
        {
            var move = 0f;
            var rotate = 0f;
            var fire = false;

            if (player == PlayerIndex.Player1)
            {
                if (Input.GetKey(KeyCode.W)) move = 1f;
                else if (Input.GetKey(KeyCode.S)) move = -1f;

                if (Input.GetKey(KeyCode.A)) rotate = -1f;
                else if (Input.GetKey(KeyCode.D)) rotate = 1f;

                fire = Input.GetKey(KeyCode.F);
            }
            else
            {
                if (Input.GetKey(KeyCode.UpArrow)) move = 1f;
                else if (Input.GetKey(KeyCode.DownArrow)) move = -1f;

                if (Input.GetKey(KeyCode.LeftArrow)) rotate = -1f;
                else if (Input.GetKey(KeyCode.RightArrow)) rotate = 1f;

                fire = Input.GetKey(KeyCode.Slash);
            }

            return new TankInputCommand(move, rotate, fire);
        }
    }
}
