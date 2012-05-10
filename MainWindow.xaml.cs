using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using System.Windows;
using KinectSabre.Render;
using Microsoft.Kinect;

namespace KinectSabre
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        RenderGame game;
        KinectSensor kinectSensor;

        byte[] bits;
        Skeleton[] skeletons;

        private double angle;
        private bool running = true;
        private DispatcherTimer readyTimer;
        private SpeechRecognitionEngine speechRecognizer;

        public MainWindow()
        {
            InitializeComponent();
            //var colorList = new List<Color> { Colors.Black, Colors.Green };
            //this.bitmapWave = new WriteableBitmap(WaveImageWidth, WaveImageHeight, 96, 96, PixelFormats.Indexed1, new BitmapPalette(colorList));

            //this.pixels = new byte[WaveImageWidth];
            //for (int i = 0; i < this.pixels.Length; i++)
            //{
            //    this.pixels[i] = 0xff;
            //}

            //imgWav.Source = this.bitmapWave;

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeKinect();           
            using (game = new RenderGame())
            {
                game.Exiting += game_Exiting;
                game.Run();
            }
      
            //if (kinectSensor != null)
                //kinectSensor.Stop();
        }


        private void InitializeKinect()
        {
            kinectSensor = KinectSensor.KinectSensors[0];
            this.speechRecognizer = this.CreateSpeechRecognizer();

            try
            {
                kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                kinectSensor.ColorFrameReady += kinectRuntime_VideoFrameReady;
                kinectSensor.SkeletonStream.Enable(new TransformSmoothParameters()
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                });
                kinectSensor.SkeletonFrameReady += kinectRuntime_SkeletonFrameReady;
                kinectSensor.Start();
            }
            catch
            {
                kinectSensor = null;
            }
            if (this.speechRecognizer != null && kinectSensor != null)
            {
                // NOTE: Need to wait 4 seconds for device to be ready to stream audio right after initialization
                this.readyTimer = new DispatcherTimer();
                this.readyTimer.Tick += this.ReadyTimerTick;
                this.readyTimer.Interval = new TimeSpan(0, 0, 4);
                this.readyTimer.Start();
            }
            this.running = true;
        }


        void kinectRuntime_VideoFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            var frame = e.OpenColorImageFrame();

            if (frame == null)
                return;

            if (bits == null || bits.Length != frame.PixelDataLength)
            {
                bits = new byte[frame.PixelDataLength];
            }
            frame.CopyPixelDataTo(bits);

            game.UpdateColorTexture(bits);
        }

        void kinectRuntime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame frame = e.OpenSkeletonFrame();

            if (frame == null)
                return;

            Kinect.Toolbox.Tools.GetSkeletons(frame, ref skeletons);

            bool player1 = true;

            foreach (Skeleton data in skeletons)
            {
                if (data.TrackingState == SkeletonTrackingState.Tracked)
                {
                    foreach (Joint joint in data.Joints)
                    {
                        // Quality check
                        if (joint.TrackingState != JointTrackingState.Tracked)
                            continue;

                        switch (joint.JointType)
                        {
                            case JointType.HandLeft:
                                if (player1)
                                    game.P1LeftHandPosition = joint.Position.ToVector3();
                                else
                                    game.P2LeftHandPosition = joint.Position.ToVector3();
                                break;
                            case JointType.HandRight:
                                if (player1)
                                    game.P1RightHandPosition = joint.Position.ToVector3();
                                else
                                    game.P2RightHandPosition = joint.Position.ToVector3();
                                break;
                            case JointType.WristLeft:
                                if (player1)
                                    game.P1LeftWristPosition = joint.Position.ToVector3();
                                else
                                    game.P2LeftWristPosition = joint.Position.ToVector3();
                                break;
                            case JointType.ElbowLeft:
                                if (player1)
                                    game.P1LeftElbowPosition = joint.Position.ToVector3();
                                else
                                    game.P2LeftElbowPosition = joint.Position.ToVector3();
                                break;
                            case JointType.WristRight:
                                if (player1)
                                    game.P1RightWristPosition = joint.Position.ToVector3();
                                else
                                    game.P2RightWristPosition = joint.Position.ToVector3();
                                break;
                            case JointType.ElbowRight:
                                if (player1)
                                    game.P1RightElbowPosition = joint.Position.ToVector3();
                                else
                                    game.P2RightElbowPosition = joint.Position.ToVector3();
                                break;
                        }
                    }

                    if (player1)
                    {
                        player1 = false;
                        game.P1IsActive = true;
                    }
                    else
                    {
                        game.P2IsActive = true;
                        return;
                    }
                }
            }

            if (player1)
                game.P1IsActive = false;

            game.P2IsActive = false;
        }

        void game_Exiting(object sender, EventArgs e)
        {
            Close();
        }
        private SpeechRecognitionEngine CreateSpeechRecognizer()
        {
            RecognizerInfo ri = GetKinectRecognizer();
            if (ri == null)
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                this.Close();
                return null;
            }

            SpeechRecognitionEngine sre;
            try
            {
                sre = new SpeechRecognitionEngine(ri.Id);
            }
            catch
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed and configured.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                this.Close();
                return null;
            }

            var grammar = new Choices();
            grammar.Add("red");
            grammar.Add("green");
            grammar.Add("blue");
            grammar.Add("purple");
            grammar.Add("Camera on");
            grammar.Add("Camera off");

            var gb = new GrammarBuilder { Culture = ri.Culture };
            gb.Append(grammar);

            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);

            sre.LoadGrammar(g);
            sre.SpeechRecognized += this.SreSpeechRecognized;
            sre.SpeechHypothesized += this.SreSpeechHypothesized;
            sre.SpeechRecognitionRejected += this.SreSpeechRecognitionRejected;

            return sre;
        }
        private void RejectSpeech(RecognitionResult result)
        {
            string status = "Rejected: " + (result == null ? string.Empty : result.Text + " " + result.Confidence);
            //this.ReportSpeechStatus(status);

           // Dispatcher.BeginInvoke(new Action(() => { tbColor.Background = blackBrush; }), DispatcherPriority.Normal);
        }

        private void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            this.RejectSpeech(e.Result);
        }

        private void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            //this.ReportSpeechStatus("Hypothesized: " + e.Result.Text + " " + e.Result.Confidence);
        }

        private void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
           // SolidColorBrush brush;

            if (e.Result.Confidence < 0.5)
            {
                this.RejectSpeech(e.Result);
                return;
            }

            switch (e.Result.Text.ToUpperInvariant())
            {
                case "RED":
                    game.redVal = 1;
                    game.greenVal = 0;
                    game.blueVal = 0;                   
                    break;
                case "GREEN":
                    game.redVal = 0;
                    game.greenVal = 1;
                    game.blueVal = 0;
                    break;
                case "BLUE":
                    game.redVal = 0;
                    game.greenVal = 0;
                    game.blueVal = 1;
                    break;
                case "PURPLE":
                    game.redVal = 1;
                    game.blueVal = 1;
                    game.greenVal = 0;
                    break;
                default:
                    game.redVal = 1;
                    game.blueVal = 1;
                    game.greenVal = 0;
                    
                    break;
            }

            string status = "Recognized: " + e.Result.Text + " " + e.Result.Confidence;
            //this.ReportSpeechStatus(status);
           // Dispatcher.BeginInvoke(new Action(() => { tbColor.Background = brush; }), DispatcherPriority.Normal);
        }
        /*
        private void ReportSpeechStatus(string status)
        {
            Dispatcher.BeginInvoke(new Action(() => { tbSpeechStatus.Text = status; }), DispatcherPriority.Normal);
        }

        private void UpdateInstructionsText(string instructions)
        {
            Dispatcher.BeginInvoke(new Action(() => { tbColor.Text = instructions; }), DispatcherPriority.Normal);
        }*/

        private void Start()
        {
            var audioSource = kinectSensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = audioSource.Start();
            
            //this.stream = new EnergyCalculatingPassThroughStream(kinectStream);
            this.speechRecognizer.SetInputToAudioStream(
                kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            this.speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
            //var t = new Thread(this.PollSoundSourceLocalization);
            
            //t.Start();
        }
       /* public MainWindow()
        {
            InitializeComponent();

            var colorList = new List<Color> { Colors.Black, Colors.Green };
            this.bitmapWave = new WriteableBitmap(WaveImageWidth, WaveImageHeight, 96, 96, PixelFormats.Indexed1, new BitmapPalette(colorList));

            this.pixels = new byte[WaveImageWidth];
            for (int i = 0; i < this.pixels.Length; i++)
            {
                this.pixels[i] = 0xff;
            }

            imgWav.Source = this.bitmapWave;

            SensorChooser.KinectSensorChanged += this.SensorChooserKinectSensorChanged;*/
        

        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

      /*  private void SensorChooserKinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor oldSensor = e.OldValue as KinectSensor;
            if (oldSensor != null)
            {
                this.UninitializeKinect();
            }

            KinectSensor newSensor = e.NewValue as KinectSensor;
            this.kinect = newSensor;

            // Only enable this checkbox if we have a sensor
            enableAec.IsEnabled = this.kinect != null;

            if (newSensor != null)
            {
                this.InitializeKinect();
            }
        }*/

       /* private void InitializeKinect()
        {
            var sensor = this.kinect;
            this.speechRecognizer = this.CreateSpeechRecognizer();
            try
            {
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                sensor.Start();
            }
            catch (Exception)
            {
                SensorChooser.AppConflictOccurred();
                return;
            }

            if (this.speechRecognizer != null && sensor != null)
            {
                // NOTE: Need to wait 4 seconds for device to be ready to stream audio right after initialization
                this.readyTimer = new DispatcherTimer();
                this.readyTimer.Tick += this.ReadyTimerTick;
                this.readyTimer.Interval = new TimeSpan(0, 0, 4);
                this.readyTimer.Start();

                this.ReportSpeechStatus("Initializing audio stream...");
                this.UpdateInstructionsText(string.Empty);

                this.Closing += this.MainWindowClosing;
            }

            this.running = true;
        }*/

        private void ReadyTimerTick(object sender, EventArgs e)
        {
            this.Start();
            //this.ReportSpeechStatus("Ready to recognize speech!");
            //this.UpdateInstructionsText("Say: 'red', 'green' or 'blue'");
            this.readyTimer.Stop();
            this.readyTimer = null;
        }

        private void UninitializeKinect()
        {
            var sensor = this.kinectSensor;
            this.running = false;
            if (this.speechRecognizer != null && sensor != null)
            {
                sensor.AudioSource.Stop();
                sensor.Stop();
                this.speechRecognizer.RecognizeAsyncCancel();
                this.speechRecognizer.RecognizeAsyncStop();
            }

            if (this.readyTimer != null)
            {
                this.readyTimer.Stop();
                this.readyTimer = null;
            }
        }
    }
}
