using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MjpegProcessor;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;

namespace BaseStationv1
{
    public class MOTOR_CONSTANTS
    {
        public const int LEFT_MOTOR = 1;
        public const int RIGHT_MOTOR = 2;
        public const int LEFT_SERVO = 3;
        public const int RIGHT_SERVO = 4;
        public const int FORWARD = 1;
        public const int REVERSE = 2;
    }

        
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MjpegDecoder streamDecoder;
        private bool captureKeys;
        private int throttleValue;
        private int servoElevationAngle;
        
        public MainWindow()
        {
            // Initialize the window
            InitializeComponent();

            // Instantiate new decoder
            streamDecoder = new MjpegDecoder();

            // Set FrameReady event handler
            streamDecoder.FrameReady += mjpeg_FrameReady;

            // Set error event handler
            streamDecoder.Error += mjpeg_Error;

            // Set no data image
            BitmapImage noDataPic = new BitmapImage(new Uri("images/NoData.png", UriKind.Relative));
            this.imgStreamDisplay.Source = noDataPic;

            // Set global variables
            throttleValue = 0;
            captureKeys = false;
            servoElevationAngle = 100;          

        }

        // Event handler for MjpegDecoder FrameReady event
        private void mjpeg_FrameReady(object sender, FrameReadyEventArgs e)
        {
            this.imgStreamDisplay.Source = e.BitmapImage;
        }

        // Event handler for MjpegDecoder error
        private void mjpeg_Error(object sender, ErrorEventArgs e)
        {
            System.Windows.MessageBox.Show(e.Message);
        }

        private void btnStartCapture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                streamDecoder.ParseStream(new Uri(this.txbURI.Text.ToString()));
            }
            catch(Exception err)
            {
                // Display error message
                System.Windows.MessageBox.Show(err.Message);
            }            
        }

        private void btnStopCapture_Click(object sender, RoutedEventArgs e)
        {
            // Stop stream
            streamDecoder.StopStream();
        }

        // Called when the test button is clicked
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Test sending a packet
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                ProtocolType.Tcp);

                IPAddress serverAddr = IPAddress.Parse("192.168.1.126");

                IPEndPoint endPoint = new IPEndPoint(serverAddr, 2619);

                sock.Connect(endPoint);

                EndPoint thisEP = new IPEndPoint(IPAddress.Any, 2619);
                byte[] response = new byte[8000];
                sock.ReceiveTimeout = 5000;
                sock.ReceiveFrom(response, ref thisEP);
                PiBlimpPacket receivedPacket = new PiBlimpPacket();
                receivedPacket.importByteArray(response);

                if(receivedPacket.pType == PiBlimpPacketType.ConnectionEstablished)
                {
                    // Set up a getPWM packet
                    PiBlimpPacket packet = new PiBlimpPacket();
                    byte[] array = packet.getPacket(PiBlimpPacketType.GetPWM);
                    sock.SendTo(array, endPoint);
                }
                else
                {
                    System.Windows.MessageBox.Show("Error: Connection not established.");
                }

                //sock.Close();
            }
            catch(Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void btnToggleKeys_Click(object sender, RoutedEventArgs e)
        {
            captureKeys = !captureKeys;
        }

        // WASD for steering
        // I/K increases/decreases elevation angle
        // O/L increases/decreases elevation angle
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(captureKeys)
            {
                PiBlimpPacket packet = new PiBlimpPacket();
                byte[] packetArray;
                switch(e.Key.ToString())
                {
                    case "W":
                        
                        // getPacket(packetType, motor_1_id, motor_1_fwd/rev, motor_1_power, motor_2_id, motor_2_power, motor_2_power
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue, MOTOR_CONSTANTS.LEFT_SERVO, 0, servoElevationAngle, MOTOR_CONSTANTS.RIGHT_SERVO, 0, servoElevationAngle);
                        break;
                    case "A":
                        System.Windows.MessageBox.Show("Both Back");
                        break;
                    case "S":
                        System.Windows.MessageBox.Show("Both Back");
                        break;
                    case "D":
                        System.Windows.MessageBox.Show("Rotate Left");
                        break;
                    case "O":
                        if(throttleValue != 100)
                        {
                            throttleValue++;
                        }                        
                        break;
                    case "L":
                        if(throttleValue != 0)
                        {
                            throttleValue--;
                        }
                        break;
                    case "I":
                        if(servoElevationAngle != 100)
                        {
                            servoElevationAngle++;
                        }
                        break;
                    case "K":
                        if(servoElevationAngle != 0)
                        {
                            servoElevationAngle--;
                        }
                        break;
                    default:
                        break;
                }

                lblThrottleValue.Content = throttleValue.ToString();
                lblElevationAngle.Content = servoElevationAngle.ToString();

                // Send the packet (if necessary)
                int i = 0;
                i++;
            }
            
        }



    }
}
