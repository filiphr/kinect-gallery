﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Kinect_Gallery
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;
    using Microsoft.Kinect;
    using Microsoft.Samples.Kinect.SwipeGestureRecognizer;
    using System.Windows.Controls;
    using Kinect_Gallery.Helpers;
    using System.Windows.Forms;
    using Kinect_Gallery.Properties;
    using Kinect_Gallery.Exceptions;
    using Microsoft.Speech.Recognition;
    using System.Text;
    using Microsoft.Speech.AudioFormat;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// The recognizer being used.
        /// </summary>
        private readonly Recognizer activeRecognizer;

        /// <summary>
        /// The paths of the picture files.
        /// </summary>
        private string[] picturePaths = null;

        /// <summary>
        /// Array of arrays of contiguous line segements that represent a skeleton.
        /// </summary>
        private static readonly JointType[][] SkeletonSegmentRuns = new JointType[][]
        {
            new JointType[] 
            { 
                JointType.Head, JointType.ShoulderCenter, JointType.HipCenter 
            },
            new JointType[] 
            { 
                JointType.HandLeft, JointType.WristLeft, JointType.ElbowLeft, JointType.ShoulderLeft,
                JointType.ShoulderCenter,
                JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight
            },
            new JointType[]
            {
                JointType.FootLeft, JointType.AnkleLeft, JointType.KneeLeft, JointType.HipLeft,
                JointType.HipCenter,
                JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight
            }
        };

        /// <summary>
        /// The sensor we're currently tracking.
        /// </summary>
        private KinectSensor nui;

        /// <summary>
        /// There is currently no connected sensor.
        /// </summary>
        private bool isDisconnectedField = true;

        /// <summary>
        /// Any message associated with a failure to connect.
        /// </summary>
        private string disconnectedReasonField;

        /// <summary>
        /// Array to receive skeletons from sensor, resize when needed.
        /// </summary>
        private Skeleton[] skeletons = new Skeleton[0];

        /// <summary>
        /// Time until skeleton ceases to be highlighted.
        /// </summary>
        private DateTime highlightTime = DateTime.MinValue;

        /// <summary>
        /// The ID of the skeleton to highlight.
        /// </summary>
        private int highlightId = -1;

        /// <summary>
        /// The ID if the skeleton to be tracked.
        /// </summary>
        private int nearestId = -1;

        /// <summary>
        /// The index of the current image.
        /// </summary>
        private int indexField = 1;

        /// <summary>
        /// The folders that the application is showing
        /// </summary>
        private Folders folders = new Folders();

        /// <summary>
        ///Hand image
        /// </summary>
        private Image handImg = new Image();

        /// <summary>
        /// Flag used for selecting a folder
        /// </summary>
        private int flag = 0;

        /// <summary>
        /// Z coordinate for depth calculation
        /// </summary>
        private float z1;

        /// <summary>
        /// The previous selected path in the folderpicker
        /// </summary>
        private string folderPickerSelectedPath = null;

        /// <summary>
        /// Speech recognition engine using audio data from Kinect.
        /// </summary>
        private SpeechRecognitionEngine speechEngine;


        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        ///  
        public MainWindow()
        {
            //this.PreviousPicture = this.LoadPicture(this.Index - 1);
            //this.Picture = this.LoadPicture(this.Index);
            //this.NextPicture = this.LoadPicture(this.Index + 1);

            InitializeComponent();

            // Create the gesture recognizer.
            this.activeRecognizer = this.CreateRecognizer();

            // Wire-up window loaded event.
            Loaded += this.OnMainWindowLoaded;

            // Wire-up window closing event.
            Closing += this.WindowClosing;

            lstFolders.ItemsSource = folders;

        }

        /// <summary>
        /// Event implementing INotifyPropertyChanged interface.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets a value indicating whether no Kinect is currently connected.
        /// </summary>
        public bool IsDisconnected
        {
            get
            {
                return this.isDisconnectedField;
            }

            private set
            {
                if (this.isDisconnectedField != value)
                {
                    this.isDisconnectedField = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("IsDisconnected"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets any message associated with a failure to connect.
        /// </summary>
        public string DisconnectedReason
        {
            get
            {
                return this.disconnectedReasonField;
            }

            private set
            {
                if (this.disconnectedReasonField != value)
                {
                    this.disconnectedReasonField = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("DisconnectedReason"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the index number of the image to be shown.
        /// </summary>
        public int Index
        {
            get
            {
                return this.indexField;
            }

            set
            {
                if (this.indexField != value)
                {
                    this.indexField = value;

                    // Notify world of change to Index and Picture.
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("Index"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the previous image displayed.
        /// </summary>
        public BitmapImage PreviousPicture { get; private set; }

        /// <summary>
        /// Gets the current image to be displayed.
        /// </summary>
        public BitmapImage Picture { get; private set; }

        /// <summary>
        /// Gets teh next image displayed.
        /// </summary>
        public BitmapImage NextPicture { get; private set; }

        /// <summary>
        /// Get list of files to display as pictures.
        /// </summary>
        /// <returns>Paths to pictures.</returns>
        private static string[] CreatePicturePaths()
        {
            var list = new List<string>();

            var commonPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures);
            list.AddRange(Directory.GetFiles(commonPicturesPath, "*.jpg", SearchOption.AllDirectories));
            if (list.Count == 0)
            {
                list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
            }

            if (list.Count == 0)
            {
                var myPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                list.AddRange(Directory.GetFiles(myPicturesPath, "*.jpg", SearchOption.AllDirectories));
                if (list.Count == 0)
                {
                    list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Get list of files to display as pictures.
        /// </summary>
        /// <param name="folderPath">The path of the folder with images to be displayed.</param>
        /// <returns>Paths to pictures.</returns>

        private static string[] CreatePicturePathsNova(string folderPath)
        {
            var list = new List<string>();

            //var commonPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonPictures);
            list.AddRange(Directory.GetFiles(folderPath, "*.jpg", SearchOption.AllDirectories));

            if (list.Count == 0)
            {
                list.AddRange(Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories));
            }

            return list.ToArray();
        }


        /// <summary>
        /// Load the picture with the given index.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>Corresponding image.</returns>
        private BitmapImage LoadPicture(int index)
        {
            BitmapImage value;

            if (this.picturePaths.Length != 0)
            {
                var actualIndex = index % this.picturePaths.Length;
                if (actualIndex < 0)
                {
                    actualIndex += this.picturePaths.Length;
                }

                Debug.Assert(0 <= actualIndex, "Index used will be non-negative");
                Debug.Assert(actualIndex < this.picturePaths.Length, "Index is within bounds of path array");

                try
                {
                    value = new BitmapImage(new Uri(this.picturePaths[actualIndex]));
                }
                catch (NotSupportedException)
                {
                    value = null;
                }
            }
            else
            {
                value = null;
            }

            return value;
        }

        /// <summary>
        /// Create a wired-up recognizer for running the slideshow.
        /// </summary>
        /// <returns>The wired-up recognizer.</returns>
        private Recognizer CreateRecognizer()
        {
            // Instantiate a recognizer.
            var recognizer = new Recognizer();

            // Wire-up swipe right to manually advance picture.
            recognizer.SwipeRightDetected += (s, e) =>
            {


                if (e.Skeleton.TrackingId == nearestId)
                {
                    if (GalleryView.Visibility == Visibility.Visible)
                    {
                        GalleryViewRightRecognized();
                    }
                    else
                    {
                        FolderViewRightRecognized();
                    }
                    var storyboard = Resources["LeftAnimate"] as Storyboard;
                    if (storyboard != null)
                    {
                        storyboard.Begin();
                    }

                    HighlightSkeleton(e.Skeleton);
                }
            };

            // Wire-up swipe left to manually reverse picture.
            recognizer.SwipeLeftDetected += (s, e) =>
            {
                if (e.Skeleton.TrackingId == nearestId)
                {
                    if (GalleryView.Visibility == Visibility.Visible)
                    {
                        GalleryViewLeftRecognized();
                    }
                    else { FolderViewLeftRecognized(); }

                    var storyboard = Resources["RightAnimate"] as Storyboard;
                    if (storyboard != null)
                    {
                        storyboard.Begin();
                    }

                    HighlightSkeleton(e.Skeleton);
                }
            };

            return recognizer;
        }

        private void FolderViewLeftRecognized()
        {
            var scrollViewer = ScrollHelper.GetScrollViewer(lstFolders) as ScrollViewer;

            if (scrollViewer != null)
            {
                // Logical Scrolling by Item
                scrollViewer.PageLeft();
                // Physical Scrolling by Offset
                //scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - 1);
            }
        }

        private void GalleryViewLeftRecognized()
        {
            Index--;

            // Setup corresponding picture if pictures are available.
            this.NextPicture = this.Picture;
            this.Picture = this.PreviousPicture;
            this.PreviousPicture = LoadPicture(Index - 1);

            // Notify world of change to Index and Picture.
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
            }
        }

        private void FolderViewRightRecognized()
        {
            var scrollViewer = ScrollHelper.GetScrollViewer(lstFolders) as ScrollViewer;

            if (scrollViewer != null)
            {
                // Logical Scrolling by Item
                scrollViewer.PageRight();
                // Physical Scrolling by Offset
                //scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + 1);
            }
        }

        private void GalleryViewRightRecognized()
        {
            Index++;

            // Setup corresponding picture if pictures are available.
            this.PreviousPicture = this.Picture;
            this.Picture = this.NextPicture;
            this.NextPicture = LoadPicture(Index + 1);

            // Notify world of change to Index and Picture.
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
            }
        }

        /// <summary>
        /// Handle insertion of Kinect sensor.
        /// </summary>
        private void InitializeNui()
        {
            this.UninitializeNui();

            var index = 0;
            while (this.nui == null && index < KinectSensor.KinectSensors.Count)
            {
                try
                {
                    this.nui = KinectSensor.KinectSensors[index];

                    this.nui.Start();

                    this.IsDisconnected = false;
                    this.DisconnectedReason = null;
                }
                catch (IOException ex)
                {
                    this.nui = null;

                    this.DisconnectedReason = ex.Message;
                }
                catch (InvalidOperationException ex)
                {
                    this.nui = null;

                    this.DisconnectedReason = ex.Message;
                }

                index++;
            }

            if (this.nui != null)
            {
                this.nui.SkeletonStream.Enable();

                this.nui.SkeletonFrameReady += this.OnSkeletonFrameReady;
            }
        }

        /// <summary>
        /// Handle removal of Kinect sensor.
        /// </summary>
        private void UninitializeNui()
        {
            if (this.nui != null)
            {
                this.nui.SkeletonFrameReady -= this.OnSkeletonFrameReady;

                this.nui.Stop();

                this.nui = null;
            }

            this.IsDisconnected = true;
            this.DisconnectedReason = null;
        }

        /// <summary>
        /// Window loaded actions to initialize Kinect handling.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Start the Kinect system, this will cause StatusChanged events to be queued.
            this.InitializeNui();

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri(@"Images\right-hand.png", UriKind.Relative);
            logo.EndInit();
            handImg.Source = logo;
            handImg.Width = 39;
            handImg.Height = 37;
            folderViewCanvas.Children.Add(handImg);
            handImg.Visibility = Visibility.Hidden;


            // Handle StatusChange events to pick the first sensor that connects.
            KinectSensor.KinectSensors.StatusChanged += (s, ee) =>
            {
                switch (ee.Status)
                {
                    case KinectStatus.Connected:
                        if (nui == null)
                        {
                            Debug.WriteLine("New Kinect connected");

                            InitializeNui();
                        }
                        else
                        {
                            Debug.WriteLine("Existing Kinect signalled connection");
                        }

                        break;
                    default:
                        if (ee.Sensor == nui)
                        {
                            Debug.WriteLine("Existing Kinect disconnected");

                            UninitializeNui();
                        }
                        else
                        {
                            Debug.WriteLine("Other Kinect event occurred");
                        }

                        break;
                }
            };

            RecognizerInfo ri = GetKinectRecognizer();

            if (null != ri)
            {

                this.speechEngine = new SpeechRecognitionEngine(ri.Id);

                /****************************************************************
                * 
                * Use this code to create grammar programmatically rather than from
                * a grammar file.
                * 
                * var directions = new Choices();
                * directions.Add(new SemanticResultValue("forward", "FORWARD"));
                * directions.Add(new SemanticResultValue("forwards", "FORWARD"));
                * directions.Add(new SemanticResultValue("straight", "FORWARD"));
                * directions.Add(new SemanticResultValue("backward", "BACKWARD"));
                * directions.Add(new SemanticResultValue("backwards", "BACKWARD"));
                * directions.Add(new SemanticResultValue("back", "BACKWARD"));
                * directions.Add(new SemanticResultValue("turn left", "LEFT"));
                * directions.Add(new SemanticResultValue("turn right", "RIGHT"));
                *
                * var gb = new GrammarBuilder { Culture = ri.Culture };
                * gb.Append(directions);
                *
                * var g = new Grammar(gb);
                * 
                ****************************************************************/

                // Create a grammar from grammar definition XML file.
                using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
                {
                    var g = new Grammar(memoryStream);
                    speechEngine.LoadGrammar(g);
                }

                speechEngine.SpeechRecognized += SpeechRecognized;

                // For long recognition sessions (a few hours or more), it may be beneficial to turn off adaptation of the acoustic model. 
                // This will prevent recognition accuracy from degrading over time.
                speechEngine.UpdateRecognizerSetting("AdaptationOn", 0);

                speechEngine.SetInputToAudioStream(
                    nui.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                this.statusBarText.Text = Properties.Resources.NoSpeechRecognizer;
            }
        }

        /// <summary>
        /// Execute uninitialization tasks.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (null != this.nui)
            {
                this.nui.AudioSource.Stop();

                this.nui.Stop();
                this.nui = null;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= SpeechRecognized;
                this.speechEngine.RecognizeAsyncStop();
            }
        }

        /// <summary>
        /// Handler for skeleton ready handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            // Get the frame.
            using (var frame = e.OpenSkeletonFrame())
            {
                // Ensure we have a frame.
                if (frame != null)
                {
                    // Resize the skeletons array if a new size (normally only on first call).
                    if (this.skeletons.Length != frame.SkeletonArrayLength)
                    {
                        this.skeletons = new Skeleton[frame.SkeletonArrayLength];
                    }

                    // Get the skeletons.
                    frame.CopySkeletonDataTo(this.skeletons);

                    // Assume no nearest skeleton and that the nearest skeleton is a long way away.
                    var newNearestId = -1;
                    var nearestDistance2 = double.MaxValue;

                    // Look through the skeletons.
                    foreach (var skeleton in this.skeletons)
                    {
                        // Only consider tracked skeletons.
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            // Find the distance squared.
                            var distance2 = (skeleton.Position.X * skeleton.Position.X) +
                                (skeleton.Position.Y * skeleton.Position.Y) +
                                (skeleton.Position.Z * skeleton.Position.Z);

                            // Is the new distance squared closer than the nearest so far?
                            if (distance2 < nearestDistance2)
                            {
                                // Use the new values.
                                newNearestId = skeleton.TrackingId;
                                nearestDistance2 = distance2;
                            }

                            if (GalleryView.Visibility == Visibility.Collapsed)
                            {
                                Joint handJoint = skeleton.Joints[JointType.HandRight];
                                SkeletonPoint handPosition = handJoint.Position;

                                double top = (folderViewCanvas.Height / 2) * ((-1) * handPosition.Y + 1);
                                double left = (folderViewCanvas.Width / 2) * (handPosition.X + 1);
                                Canvas.SetTop(handImg, top);
                                Canvas.SetLeft(handImg, left);
                                handImg.Visibility = Visibility.Visible;



                                if (flag == 0)
                                {
                                    z1 = (float)handPosition.Z;
                                    flag = 1;
                                }else if ((z1 - ((float)handPosition.Z)) >= 0.3)
                                {//txtHi5.Text = "Hi 5";
                                    BitmapImage logo = new BitmapImage();
                                    logo.BeginInit();
                                    logo.UriSource = new Uri(@"Images\right-hand-green.png", UriKind.Relative);
                                    logo.EndInit();
                                    handImg.Source = logo;

                                    lstFolders.SelectedIndex = selectItem(left);

                                    flag = 0;
                                }

                            }
                        }
                    }

                    if (this.nearestId != newNearestId)
                    {
                        this.nearestId = newNearestId;
                    }

                    // Pass skeletons to recognizer.
                    this.activeRecognizer.Recognize(sender, frame, this.skeletons);

                    this.DrawStickMen(this.skeletons);




                }
            }
        }

        /// <summary>
        /// Selection of the selected folder using Kinect
        /// </summary>
        /// <param name="handPositionX">The X coordinate of the Kinect Sensors</param>
        /// <returns>The index of the selected folder</returns>
        private int selectItem(double handPositionX)
        {
            var scrollViewer = ScrollHelper.GetScrollViewer(lstFolders) as ScrollViewer;
            int index = 0;

            if (handPositionX < folderViewCanvas.Width / 3)
            {
                index = (int)scrollViewer.HorizontalOffset + 0;
            }
            else if (handPositionX < 2 * folderViewCanvas.Width / 3)
            {
                index = (int)scrollViewer.HorizontalOffset + 1;
            }
            else { index = (int)scrollViewer.HorizontalOffset + 2; }


            return index;

        }

        /// <summary>
        /// Select a skeleton to be highlighted.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        private void HighlightSkeleton(Skeleton skeleton)
        {
            // Set the highlight time to be a short time from now.
            this.highlightTime = DateTime.UtcNow + TimeSpan.FromSeconds(0.5);

            // Record the ID of the skeleton.
            this.highlightId = skeleton.TrackingId;
        }

        /// <summary>
        /// Draw stick men for all the tracked skeletons.
        /// </summary>
        /// <param name="skeletons">The skeletons to draw.</param>
        private void DrawStickMen(Skeleton[] skeletons)
        {
            // Remove any previous skeletons.
            StickMen.Children.Clear();

            foreach (var skeleton in skeletons)
            {
                // Only draw tracked skeletons.
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // Draw a background for the next pass.
                    this.DrawStickMan(skeleton, Brushes.WhiteSmoke, 7);
                }
            }

            foreach (var skeleton in skeletons)
            {
                // Only draw tracked skeletons.
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // Pick a brush, Red for a skeleton that has recently gestures, black for the nearest, gray otherwise.
                    var brush = DateTime.UtcNow < this.highlightTime && skeleton.TrackingId == this.highlightId ? Brushes.Red :
                        skeleton.TrackingId == this.nearestId ? Brushes.Black : Brushes.Gray;

                    // Draw the individual skeleton.
                    this.DrawStickMan(skeleton, brush, 3);
                }
            }
        }

        /// <summary>
        /// Draw an individual skeleton.
        /// </summary>
        /// <param name="skeleton">The skeleton to draw.</param>
        /// <param name="brush">The brush to use.</param>
        /// <param name="thickness">This thickness of the stroke.</param>
        private void DrawStickMan(Skeleton skeleton, Brush brush, int thickness)
        {
            Debug.Assert(skeleton.TrackingState == SkeletonTrackingState.Tracked, "The skeleton is being tracked.");

            foreach (var run in SkeletonSegmentRuns)
            {
                var next = this.GetJointPoint(skeleton, run[0]);
                for (var i = 1; i < run.Length; i++)
                {
                    var prev = next;
                    next = this.GetJointPoint(skeleton, run[i]);

                    var line = new Line
                    {
                        Stroke = brush,
                        StrokeThickness = thickness,
                        X1 = prev.X,
                        Y1 = prev.Y,
                        X2 = next.X,
                        Y2 = next.Y,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeStartLineCap = PenLineCap.Round
                    };

                    StickMen.Children.Add(line);
                }
            }
        }

        /// <summary>
        /// Convert skeleton joint to a point on the StickMen canvas.
        /// </summary>
        /// <param name="skeleton">The skeleton.</param>
        /// <param name="jointType">The joint to project.</param>
        /// <returns>The projected point.</returns>
        private Point GetJointPoint(Skeleton skeleton, JointType jointType)
        {
            var joint = skeleton.Joints[jointType];

            // Points are centered on the StickMen canvas and scaled according to its height allowing
            // approximately +/- 1.5m from center line.
            var point = new Point
            {
                X = (StickMen.Width / 2) + (StickMen.Height * joint.Position.X / 3),
                Y = (StickMen.Width / 2) - (StickMen.Height * joint.Position.Y / 3)
            };

            return point;
        }

        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// Handler for the SelecctionChanged of the ListBox
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The event args</param>
        private void lstFolders_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int selectedIndex = lstFolders.SelectedIndex;
            if (selectedIndex != -1)
            {
                Folder folder = (Folder)lstFolders.SelectedItem;

                picturePaths = CreatePicturePathsNova(folder.FolderPath);

                FolderView.Visibility = Visibility.Collapsed;
                GalleryView.Visibility = Visibility.Visible;
                this.PreviousPicture = this.LoadPicture(this.Index - 1);
                this.Picture = this.LoadPicture(this.Index);
                this.NextPicture = this.LoadPicture(this.Index + 1);
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                    this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                    this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
                }
                //this.nui.AudioSource.Start();
            }
        }

        /// <summary>
        /// Handler for back button in GalleryView
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The event args</param>
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            picturePaths = null;
            GalleryView.Visibility = Visibility.Collapsed;
            FolderView.Visibility = Visibility.Visible;
            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri(@"Images\right-hand.png", UriKind.Relative);
            logo.EndInit();
            handImg.Source = logo;
            Index = 1;
            lstFolders.SelectedIndex = -1;
            //this.nui.AudioSource.Stop();
        }

        /// <summary>
        /// Handler for the Add Folder button in Folder View
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The event args</param>
        private void btnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (folderPickerSelectedPath != null)
            {
                dialog.SelectedPath = folderPickerSelectedPath;
            }
            DialogResult result = dialog.ShowDialog();
            if (result.ToString() == "OK")
            {
                string folderPath = System.IO.Path.GetFullPath(dialog.SelectedPath.ToString()).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                folderPickerSelectedPath = folderPath;
                try
                {
                    folders.InsertAndAdd(folderPath);
                }
                catch (ExistsException ex)
                {
                    System.Windows.MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// Handler for recognized speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.3;
            if (GalleryView.Visibility == Visibility.Visible)
            {
                if (e.Result.Confidence >= ConfidenceThreshold)
                {
                    switch (e.Result.Semantics.Value.ToString())
                    {
                        case "BACKWARD":
                            this.btnBack.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                            break;
                    }
                }
            }
        }
    }
}
