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
            InitializeComponent();

            // Instantiate new decoder
            streamDecoder = new MjpegDecoder();

            // Set FrameReady event handler
            streamDecoder.FrameReady += mjpeg_FrameReady;

            // Set error event handler
            streamDecoder.Error += mjpeg_Error;

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
    }
}
