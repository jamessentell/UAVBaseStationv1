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

namespace BaseStationv1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MjpegDecoder streamDecoder;
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

        }

        // Event handler for MjpegDecoder FrameReady event
        private void mjpeg_FrameReady(object sender, FrameReadyEventArgs e)
        {
            this.imgStreamDisplay.Source = e.BitmapImage;
        }

        // Event handler for MjpegDecoder error
        private void mjpeg_Error(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(e.Message);
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
                MessageBox.Show(err.Message);
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
                sock.ReceiveFrom(response, ref thisEP);
                PiBlimpPacket receivedPacket = new PiBlimpPacket();
                receivedPacket.importByteArray(response);

                if(receivedPacket.pType == PiBlimpPacketType.ConnectionEstablished)
                {
                    // Set up a getPWM packet
                    PiBlimpPacket packet = new PiBlimpPacket();
                    packet.pType = PiBlimpPacketType.GetPWM;
                    byte[] array = packet.generateByteArray();
                    sock.SendTo(array, endPoint);
                }
                else
                {
                    MessageBox.Show("Error: Connection not established.");
                }

                sock.Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
