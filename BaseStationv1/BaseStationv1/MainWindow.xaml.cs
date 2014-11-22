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
        private System.Timers.Timer controllerPollTimer;
        private GamePadConnector connector;
        private ControllerMode controllerMode = ControllerMode.NoSynch;
        private PiBlimpPacket packetGenerator;
        int counter = 0;
        
        enum ControllerMode
        {
            Synch,
            NoSynch
        }
        
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
            IPAddress[] addresses = null;
            try
            {
                addresses = Dns.GetHostAddresses("piblimp.");
            }
            catch(Exception ex)
            {
                addresses = null;
            }
            

            if(addresses != null && addresses.Length > 0)
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

            // Initialize GamePadConnector
            connector = new GamePadConnector();

            packetGenerator = new PiBlimpPacket();
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
            /*
            PiBlimpPacket packet = new PiBlimpPacket();
            byte[] array1 = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, 40);
            socket.Send(array1);

            System.Threading.Thread.Sleep(2000);

            byte[] array2 = packet.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, MOTOR_CONSTANTS.FORWARD, 0);
            socket.Send(array2);
            */

            // Start controller timer for every 1/100 second
            this.controllerPollTimer = new System.Timers.Timer(10);
            this.controllerPollTimer.Enabled = true;
            this.controllerPollTimer.Elapsed += new ElapsedEventHandler(pollController);


            

        }


        private void pollController(object source, ElapsedEventArgs e)
        {
            this.counter++;
            State currentState = connector.getControllerState();

            this.lblLeftY.Dispatcher.Invoke(new setLblLeftYContentCallback(this.setLblLeftYContent), new object[] { currentState.Gamepad.LeftThumbY.ToString() });
            this.lblLeftX.Dispatcher.Invoke(new setLblLeftXContentCallback(this.setLblLeftXContent), new object[] { currentState.Gamepad.LeftThumbX.ToString() });
            this.lblRightY.Dispatcher.Invoke(new setLblRightYContentCallback(this.setLblRightYContent), new object[] { currentState.Gamepad.RightThumbY.ToString() });
            this.lblRightX.Dispatcher.Invoke(new setLblRightXContentCallback(this.setLblRightXContent), new object[] { currentState.Gamepad.RightThumbX.ToString() });
            this.lblRightLowerTrigger.Dispatcher.Invoke(new setLblRightLowerTriggerContentCallback(this.setLblRightLowerTriggerContent), new object[] { currentState.Gamepad.RightTrigger.ToString() });

            double maxThrottle = 50;
            double deadzone = 4000;
            double leftThumbY = Math.Abs(currentState.Gamepad.LeftThumbY - 1);  // "-1" added to handle the case when negating the min value of a two's complement number.  Loss of precision will be minimal.
            double rightThumbY = Math.Abs(currentState.Gamepad.RightThumbY - 1);
            double rightThrottle = 0;
            double leftThrottle = 0;
            int leftDirection = MOTOR_CONSTANTS.FORWARD; // true = forward
            int rightDirection = MOTOR_CONSTANTS.FORWARD; // true = forward

            // Check for a thumb button press
            if(counter == 7)
            {
                counter = 0;
                if (currentState.Gamepad.Buttons.ToString().Contains("RightShoulder"))
                {
                    // Toggle mode
                    if (this.controllerMode == ControllerMode.Synch)
                    {
                        this.controllerMode = ControllerMode.NoSynch;
                    }
                    else
                    {
                        this.controllerMode = ControllerMode.Synch;
                    }
                }
            }            

            // Check for synchronized mode
            if(this.controllerMode == ControllerMode.Synch)
            {
                rightThumbY = leftThumbY;
            }

            // Left analog stick
            if(leftThumbY < deadzone && leftThumbY > -deadzone)
            {
                leftThumbY = 0;
                leftThrottle = 0;
            }
            else
            {
                // Calculate throttle value
                double ratio = (leftThumbY - deadzone) / (32767 - deadzone);
                leftThrottle = Math.Round(ratio * maxThrottle);

                // Set direction
                if(currentState.Gamepad.LeftThumbY > 0)
                {
                    leftDirection = MOTOR_CONSTANTS.FORWARD;
                }
                else
                {
                    leftDirection = MOTOR_CONSTANTS.REVERSE;
                }                
            }
            
            // Right analog stick
            if (rightThumbY < deadzone && rightThumbY > -deadzone)
            {
                rightThumbY = 0;
                rightThrottle = 0;
            }
            else
            {
                // Calculate throttle value
                double ratio = (rightThumbY - deadzone) / (32767 - deadzone);
                rightThrottle = Math.Round(ratio * maxThrottle);

                // Set direction
                if(controllerMode == ControllerMode.Synch)
                {
                    rightDirection = leftDirection;
                }
                else
                {
                    if (currentState.Gamepad.RightThumbY > 0)
                    {
                        rightDirection = MOTOR_CONSTANTS.FORWARD;
                    }
                    else
                    {
                        rightDirection = MOTOR_CONSTANTS.REVERSE;
                    }
                }
                
            }

            // Right Trigger (Servo elevation angle)
            double rightTriggerValue = currentState.Gamepad.RightTrigger;
            rightTriggerValue = rightTriggerValue - 255;
            rightTriggerValue = Math.Abs(rightTriggerValue);
            double servoPercent = (rightTriggerValue / 255) * 100;
            
            // Update left analog stick display values
            this.lblLeftMotorThrottle.Dispatcher.Invoke(new setLblLeftMotorThrottleContentCallback(this.setLblLeftMotorThrottleContent), new object[] { Math.Round(leftThrottle).ToString() });

            if(leftDirection == MOTOR_CONSTANTS.FORWARD)
            {
                this.lblLeftMotorDirection.Dispatcher.Invoke(new setLblLeftMotorDirectionContentCallback(this.setLblLeftMotorDirectionContent), new object[] { "F" });
            }
            else
            {
                this.lblLeftMotorDirection.Dispatcher.Invoke(new setLblLeftMotorDirectionContentCallback(this.setLblLeftMotorDirectionContent), new object[] { "R" });
            }

            // Update right analog stick display values
            this.lblRightMotorThrottle.Dispatcher.Invoke(new setLblRightMotorThrottleContentCallback(this.setLblRightMotorThrottleContent), new object[] { Math.Round(rightThrottle).ToString() });

            if(rightDirection == MOTOR_CONSTANTS.FORWARD)
            {
                this.lblRightMotorDirection.Dispatcher.Invoke(new setLblRightMotorDirectionContentCallback(this.setLblRightMotorDirectionContent), new object[] { "F" });
            }
            else
            {
                this.lblRightMotorDirection.Dispatcher.Invoke(new setLblRightMotorDirectionContentCallback(this.setLblRightMotorDirectionContent), new object[] { "R" });
            }

            // Update servo angle percent display
            this.lblServoAnglePercent.Dispatcher.Invoke(new setLblServoAnglePercentContentCallback(this.setLblServoAnglePercentContent), new object[] { Math.Round(servoPercent).ToString() });
            
            // Create packet and send
            byte[] array = packetGenerator.getPacket(PiBlimpPacketType.SetPWM, MOTOR_CONSTANTS.LEFT_MOTOR, leftDirection, Convert.ToInt32(leftThrottle), MOTOR_CONSTANTS.RIGHT_MOTOR, rightDirection, Convert.ToInt32(rightThrottle), MOTOR_CONSTANTS.LEFT_SERVO, 0, Convert.ToInt32(servoPercent), MOTOR_CONSTANTS.RIGHT_SERVO, 0, Convert.ToInt32(servoPercent));
            //socket.Send(array);

        }

        public delegate void setLblServoAnglePercentContentCallback(string message);

        private void setLblServoAnglePercentContent(String content)
        {
            this.lblServoAnglePercent.Content = content;
        }

        public delegate void setLblLeftMotorDirectionContentCallback(string message);

        private void setLblLeftMotorDirectionContent(String content)
        {
            this.lblLeftMotorDirection.Content = content;
        }

        public delegate void setLblLeftMotorThrottleContentCallback(string message);

        private void setLblLeftMotorThrottleContent(String content)
        {
            this.lblLeftMotorThrottle.Content = content;
        }

        public delegate void setLblRightMotorDirectionContentCallback(string message);

        private void setLblRightMotorDirectionContent(String content)
        {
            this.lblRightMotorDirection.Content = content;
        }

        public delegate void setLblRightMotorThrottleContentCallback(string message);

        private void setLblRightMotorThrottleContent(String content)
        {
            this.lblRightMotorThrottle.Content = content;
        }

        public delegate void setLblRightLowerTriggerContentCallback(string message);

        private void setLblRightLowerTriggerContent(String content)
        {
            this.lblRightLowerTrigger.Content = content;
        }

        public delegate void setLblRightXContentCallback(string message);

        private void setLblRightXContent(String content)
        {
            this.lblRightX.Content = content;
        }

        public delegate void setLblRightYContentCallback(string message);

        private void setLblRightYContent(String content)
        {
            this.lblRightY.Content = content;
        }

        public delegate void setLblLeftXContentCallback(string message);

        private void setLblLeftXContent(String content)
        {
            this.lblLeftX.Content = content;
        }

        public delegate void setLblLeftYContentCallback(string message);

        private void setLblLeftYContent(String content)
        {
            this.lblLeftY.Content = content;
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
