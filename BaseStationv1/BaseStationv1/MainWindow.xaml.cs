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
using System.Timers;

using SharpDX.XInput;


namespace BaseStationv1
{
    public class MOTOR_CONSTANTS
    {
        public const int LEFT_MOTOR = 2;
        public const int RIGHT_MOTOR = 1;
        public const int LEFT_SERVO = 3;
        public const int RIGHT_SERVO = 4;
        public const int FORWARD = 1;
        public const int REVERSE = 2;
    }

    static class SocketExtensions
    {
        public static bool IsConnected(this Socket socket)
        {
            try
            {
                return !((socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0)) || !socket.Connected);
            }
            catch(Exception e)
            {
                return false;
            }
            
        }
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
        private Socket socket;
        private string piIpAddress;
        private int portNum;
        private System.Timers.Timer keepAliveTimer;
        private Boolean constructorRan = false;
        private bool videoStreamUp;
        
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

            // Set up socket variable

            // Attempt to locate pi address

            IPAddress[] addresses = Dns.GetHostAddresses("piblimp.");

            if(addresses.Length > 0)
            {
                piIpAddress = addresses[0].ToString();
            }
            else
            {
                piIpAddress = "192.168.1.126";
            }
                       
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                ProtocolType.Tcp);
            portNum = 2619;

            // initialize keepAlive timer
            this.keepAliveTimer = new System.Timers.Timer(500);
            this.keepAliveTimer.Elapsed += new ElapsedEventHandler(sendKeepAlivePacket);

            // Initialize uri string
            this.txbURI.Text = "http://" + this.piIpAddress.ToString() + ":8080/?action=stream";

            this.constructorRan = true;
            videoStreamUp = false;
        }

        private void sendKeepAlivePacket(object source, ElapsedEventArgs e)
        {
            try
            {
                PiBlimpPacket packet = new PiBlimpPacket();
                byte[] temp = packet.getPacket(PiBlimpPacketType.KeepAlive);
                this.socket.Send(temp);
            }
            catch(Exception ex)
            {
                System.Console.WriteLine(ex.Message);
                
            }
            
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
            //this.establishConnection();
            try
            {
                // Send packet to start video stream
                //if(this.videoStreamUp == false)
                //{
                PiBlimpPacket packet = new PiBlimpPacket();
                byte[] array = packet.getPacket(PiBlimpPacketType.StartVideoStream);
                socket.Send(array);                    
                //}
                //else
                //{
                //    PiBlimpPacket packet = new PiBlimpPacket();
                //    byte[] array = packet.getPacket(PiBlimpPacketType.RestartVideoStream);
                //    socket.Send(array);  
               //}
                streamDecoder.ParseStream(new Uri(this.txbURI.Text.ToString()));
                this.videoStreamUp = true;
            }
            catch(Exception err)
            {
                // Display error message
                System.Windows.MessageBox.Show(err.Message);
                this.videoStreamUp = false;
            }            
        }

        private void btnStopCapture_Click(object sender, RoutedEventArgs e)
        {
            // Stop stream
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] array = packet.getPacket(PiBlimpPacketType.StopVideoStream);
            socket.Send(array);
            streamDecoder.StopStream();
            this.videoStreamUp = false;
        }

        // Called when the test button is clicked
        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] array1 = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, 40);
            socket.Send(array1);

            System.Threading.Thread.Sleep(2000);

            byte[] array2 = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, 0);
            socket.Send(array2);
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
                byte[] packetArray = new byte[0];
                switch(e.Key.ToString())
                {
                    case "W":
                        
                        // getPacket(packetType, motor_1_id, motor_1_fwd/rev, motor_1_power, motor_2_id, motor_2_power, motor_2_power
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue);
                        break;
                    case "A":
                        //System.Windows.MessageBox.Show("Rotate Left");
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.REVERSE, throttleValue, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue);
                        break;
                    case "S":
                        //System.Windows.MessageBox.Show("Both Back");
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.REVERSE, throttleValue, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.REVERSE, throttleValue);
                        break;
                    case "D":
                        //System.Windows.MessageBox.Show("Rotate Left");
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, throttleValue, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.REVERSE, throttleValue);
                        break;
                    case "O":
                        // Increment throttle value
                        if(throttleValue != 100)
                        {
                            throttleValue++;
                            // Send packet for new throttle value
                            packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.RIGHT_MOTOR, 0, throttleValue, MOTOR_CONSTANTS.LEFT_MOTOR, 0, throttleValue);
                        }                        
                        break;
                    case "L":
                        if(throttleValue != 0)
                        {
                            throttleValue--;
                            packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.RIGHT_MOTOR, 0, throttleValue, MOTOR_CONSTANTS.LEFT_MOTOR, 0, throttleValue);                        
                        }
                        break;
                    case "I":
                        if(servoElevationAngle != 100)
                        {
                            servoElevationAngle++;
                            packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_SERVO, 0, servoElevationAngle, MOTOR_CONSTANTS.RIGHT_SERVO, 0, servoElevationAngle);

                        }
                        break;
                    case "K":
                        if(servoElevationAngle != 0)
                        {
                            servoElevationAngle--;
                            packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_SERVO, 0, servoElevationAngle, MOTOR_CONSTANTS.RIGHT_SERVO, 0, servoElevationAngle);
                        }
                        break;
                    case "Q":
                        // Stop all motors
                        packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, 0, MOTOR_CONSTANTS.RIGHT_MOTOR, MOTOR_CONSTANTS.FORWARD, 0, MOTOR_CONSTANTS.LEFT_SERVO, 0, 100, MOTOR_CONSTANTS.RIGHT_SERVO, 0, 100);
                        this.servoElevationAngle = 100;
                        this.throttleValue = 0;
                        this.lblElevationAngle.Content = servoElevationAngle.ToString();
                        this.lblThrottleValue.Content = throttleValue.ToString();
                        break;
                    default:
                        break;
                }

                lblThrottleValue.Content = throttleValue.ToString();
                lblElevationAngle.Content = servoElevationAngle.ToString();

                // Send the packet (if necessary)
                if(packetArray.Length > 0)
                {
                    // Check if the socket is connected
                    if(SocketExtensions.IsConnected(this.socket))
                    {
                        this.socket.Send(packetArray);
                    }
                    else
                    {
                        this.connectSocket();
                        this.socket.Send(packetArray);
                    }
                }
            }            
        }

        // Initializes and establishes the socket connection to the raspberry pi
        private void establishConnection()
        {
            if(!SocketExtensions.IsConnected(socket))
            {
                bool result = this.connectSocket();
                if (result)
                {
                    // Enable keep alive timer
                    if (this.keepAliveTimer == null)
                    {
                        this.keepAliveTimer = new System.Timers.Timer(500);
                    }
                    this.keepAliveTimer.Elapsed += new ElapsedEventHandler(sendKeepAlivePacket);
                    this.keepAliveTimer.Enabled = true;


                    System.Windows.MessageBox.Show("Connected to " + piIpAddress);
                }
                else
                {
                    System.Windows.MessageBox.Show("Could not connect to " + piIpAddress);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Already connected to " + piIpAddress);
            }
           
        }

        // Closes the socket connection to the raspberry pi
        private void closeConnection()
        {
            if (SocketExtensions.IsConnected(this.socket))
            {
                // Close the socket connection
                socket.Close();
                keepAliveTimer.Enabled = false;
                keepAliveTimer = null;

                System.Windows.MessageBox.Show("Connection to " + piIpAddress + " closed.");
            }
            else
            {
                System.Windows.MessageBox.Show("Socket was not connected.");
            }
        }

        // Establish the TCP Connection
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            this.establishConnection();
        }

        // Disconnect from the Pi
        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            this.closeConnection();            
        }

        private bool connectSocket()
        {
            // Attempt to connect
            IPAddress serverAddr = IPAddress.Parse(this.piIpAddress);
            IPEndPoint endPoint = new IPEndPoint(serverAddr, this.portNum);
            this.socket = null;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                ProtocolType.Tcp);
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                this.socket.Connect(endPoint);
            }
            catch(Exception e)
            {
                System.Windows.MessageBox.Show(e.Message);
                return false;
            }


            // Check for a connection acknowledgement
            EndPoint thisEP = new IPEndPoint(IPAddress.Any, 2619);
            byte[] response = new byte[8000];
            this.socket.ReceiveTimeout = 5000;
            this.socket.ReceiveFrom(response, ref thisEP);
            PiBlimpPacket receivedPacket = new PiBlimpPacket();
            receivedPacket.importByteArray(response);

            if (receivedPacket.pType == PiBlimpPacketType.ConnectionEstablished)
            {
                return true;
            }
            else
            {
                return false;
            }            
        }

        private void txbIpAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(this.constructorRan)
            {
                this.portNum = 2619;
                this.piIpAddress = txbIpAddress.Text;

                if (SocketExtensions.IsConnected(this.socket))
                {
                    socket.Close();
                    connectSocket();
                }                
                this.txbURI.Text = "http://" + this.piIpAddress.ToString() + ":8080/?action=stream";
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_SERVO, 0, throttleValue, MOTOR_CONSTANTS.RIGHT_SERVO, 0, throttleValue);
            socket.Send(packetArray);
            this.servoElevationAngle = 0;
            this.lblElevationAngle.Content = 0;
        }

        private void btnShutdown_Click(object sender, RoutedEventArgs e)
        {
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] packetArray = packet.getPacket(PiBlimpPacketType.Shutdown);
            socket.Send(packetArray);
            this.servoElevationAngle = 100;
            this.lblElevationAngle.Content = "100";
            this.throttleValue = 0;
            this.lblThrottleValue.Content = "0";
        }

        private void btnPntUpward_Click(object sender, RoutedEventArgs e)
        {
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] packetArray = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_SERVO, 100, throttleValue, MOTOR_CONSTANTS.RIGHT_SERVO, 100, throttleValue);
            socket.Send(packetArray);
            this.servoElevationAngle = 0;
            this.lblElevationAngle.Content = 0;
        }
    }
}
