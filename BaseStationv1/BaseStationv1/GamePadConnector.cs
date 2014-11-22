using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.XInput;

namespace BaseStationv1
{
    class GamePadConnector
    {
        public GamePadConnector()
        {
            
        }

        public State getControllerState()
        {
            // Grab the connected controller reference
            Controller[] conArray = new[] { new Controller(UserIndex.One), new Controller(UserIndex.Two), new Controller(UserIndex.Three), new Controller(UserIndex.Four) };

            // Search for connected controller
            Controller controller = null;
            foreach (Controller selectedController in conArray)
            {
                if (selectedController.IsConnected)
                {
                    controller = selectedController;
                    break;
                }
            }

            if (controller != null)
            {
                State currentState = controller.GetState();
                return currentState;
            }
            else
            {
                return new State();

            }

        }

        
    }
}
