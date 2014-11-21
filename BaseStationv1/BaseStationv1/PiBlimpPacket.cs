using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseStationv1
{
    enum PiBlimpPacketType
    {
        GetPWM,
        SetPWM,
        ConnectionEstablished,
        ConnectionClose,
        UnknownType,
        StartVideoStream,
        StopVideoStream,
        RestartVideoStream,
        KeepAlive,
        Shutdown
    };

    class PiBlimpPacket
    {
        // Private data members
        public PiBlimpPacketType pType { get; set; }

        public PiBlimpPacket()
        {}

        // Send packet
        // Converts the current packet to a byte array and returns said array
        // Argument formats
        //      setPWM:
        //          list[0] -> Motor ID (1 - 4)
        //          list[1] -> FWD/REV (1/2)
        //          list[2] -> Power Level (0 - 100)
        //      If any more motors are specified, they will be in consecutive sets of 3 with the same order
        //          list[3] -> Motor ID (1 - 4)
        //          list[4] -> FWD/REV (1/2)
        //          list[5] -> Power Level (0 - 100)
        //
        // Note: arguments are passed in as integers - Programmers responsibility to avoid overflow
        public byte[] getPacket(PiBlimpPacketType type, params int[] list)
        {
            // Set the packet type
            this.pType = type;

            // Instantiate
            byte[] bArray = new byte[128];
            
            // Set delimiter byte "?"
            bArray[127] = 63;

            // Switch on packet type
            switch(this.pType)
            {
                case PiBlimpPacketType.SetPWM:
                    bArray[0] = 100;
                    int index = 0;
                    // Add values into appropriate packet slots
                    for(int i = 0; i < (list.Length / 3); i++)
                    {
                        int packetFirst = (i + 1) * 4;
                        int listFirst = i * 3;
                        bArray[packetFirst] = (byte) list[listFirst];
                        bArray[packetFirst + 1] = (byte) list[listFirst + 1];
                        bArray[packetFirst + 2] = (byte) list[listFirst + 2];
                    }
                    break;
                case PiBlimpPacketType.GetPWM:
                    bArray[0] = 101;
                    break;
                case PiBlimpPacketType.ConnectionClose:
                    bArray[0] = 201;
                    break;
                case PiBlimpPacketType.KeepAlive:
                    bArray[0] = 1;
                    break;
                case PiBlimpPacketType.StartVideoStream:
                    bArray[0] = 50;
                    bArray[4] = 1;
                    break;
                case PiBlimpPacketType.StopVideoStream:
                    bArray[0] = 50;
                    bArray[4] = 0;
                    break;
                case PiBlimpPacketType.RestartVideoStream:
                    bArray[0] = 50;
                    bArray[4] = 2;
                    break;
                case PiBlimpPacketType.Shutdown:
                    bArray[0] = 75;
                    break;
                default:
                    break;
            }

            return bArray;
        }

        // Received packet
        // Converts from a byte array to a PiBlimpPacket
        public void importByteArray(byte[] array)
        {
            switch (array[0])
            {
                case 200:
                    this.pType = PiBlimpPacketType.ConnectionEstablished;
                    break;
                case 201:
                    this.pType = PiBlimpPacketType.ConnectionClose;
                    break;
                default:
                    this.pType = PiBlimpPacketType.UnknownType;
                    break;
            }
        }
    }
}
