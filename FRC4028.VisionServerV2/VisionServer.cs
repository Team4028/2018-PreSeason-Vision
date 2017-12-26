using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;
using Emgu.CV.UI;
using Emgu.CV.Util;
using SimpleTCP;

using rtaNetworking.Streaming;
using FRC4028.VisionServerV2.Entities;
using FRC4028.VisionServerV2.MPEG;
using FRC4028.VisionServerV2.StatusLED;
using FRC4028.VisionServerV2.NetworkTables;
using FRC4028.VisionServerV2.Utilities;

namespace FRC4028.VisionServerV2
{
    /// <summary>
    /// This Class is the FRC Team 4028's Main Vision Data Server.
    /// 
    /// It consists of five (5) Threads:
    ///   - Camera Polling Server (reads frame from camera, analyzes w OpenCV to recognize the target)
    ///   - TCP Socket ImageData Server (supplies data to RoboRIO)
    ///   - MPEG Streaming Image Server (displays live image to dashboard)
    ///   - Blinkstick heartbeat strobing thread
    ///   - Network Tables reConnect thread
    /// </summary>
    public class VisionServer
    {
        // ================================
        // global working variables
        // ================================
        VideoCapture _capture;

        // allows multiple threads to be in read mode, 
        // allows one thread to be in write mode with exclusive ownership of the lock,
        ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim();

        // Define the shared resource that will be protected by the ReaderWriterLock.
        TargetInfoBE _targetInfo;

        Stopwatch _fpsStopWatch;
        Stopwatch _frameStopWatch;  // ie: how long it takes to process each frame

        int _fpsLoopCounter = 0;

        MovingAverage _fpsMovingAverage;

        System.Threading.Timer _cameraThread;
        System.Threading.Timer _heartbeatThread;

        SimpleTcpServer _tcpServer;
        ImageStreamingServer _mpegServer;
        BlinkStickController _blinkStickCtlr;
        NetworkTablesClient _networkTablesClient;

        System.Management.ManagementClass _wmi;
        PerformanceCounter _cpuPerfCtr;

        // Define Range Values of Green Color in HSV (http://www.colorizer.org/)
        Hsv LOWER_GREEN;
        Hsv UPPER_GREEN;

        // ================================
        // Config Constants
        // ================================
        const string CONFIG_FILENAME = @"ConfigData.json";

        MCvScalar CONTOUR_COLOR = new MCvScalar(255, 0, 0);
        MCvScalar TARGET_COLOR = new MCvScalar(0, 0, 255);
        MCvScalar FPS_COLOR = new MCvScalar(0, 0, 255);
        MCvScalar CTR_OF_SCREEN_COLOR = new MCvScalar(255, 255, 255);
        MCvScalar OFFSET_COLOR = new MCvScalar(255, 255, 255);

        const int MOVING_AVG_WINDOW_SIZE = 10;

        /// <summary>
        /// Starts all threads
        /// </summary>
        public void Start()
        { 
            // ==========================================
            // Step 1: load config data
            // ==========================================
            ConfigDataBE configData = GeneralUtilities.GetConfigData(CONFIG_FILENAME);


            // OpenCV Notes: For HSV
            //  Hue range is        [0,179]
            //  Saturation range is [0,255]
            //  Value range is      [0,255]
            LOWER_GREEN = new Hsv(configData.TargetColorBounds.LowerBound.Hue,
                                    configData.TargetColorBounds.LowerBound.Saturation,
                                    configData.TargetColorBounds.LowerBound.Value);

            UPPER_GREEN = new Hsv(configData.TargetColorBounds.UpperBound.Hue,
                                    configData.TargetColorBounds.UpperBound.Saturation,
                                    configData.TargetColorBounds.UpperBound.Value);

            // ==========================================
            // Step 2: create misc mgmt objs to query WMI & Perf Ctrs
            // ==========================================
            _wmi = new System.Management.ManagementClass("Win32_Battery");

            _cpuPerfCtr = new PerformanceCounter();
            _cpuPerfCtr.CategoryName = "Processor";
            _cpuPerfCtr.CounterName = "% Processor Time";
            _cpuPerfCtr.InstanceName = "_Total";

            // ==========================================
            // Step 3: init some global objects
            // ==========================================
            _targetInfo = new TargetInfoBE();
            _fpsStopWatch = new Stopwatch();
            _fpsMovingAverage = new MovingAverage(MOVING_AVG_WINDOW_SIZE);
            _frameStopWatch = new Stopwatch();

            // ==========================================
            // Step 4: setup camera capture (main OpenCV Loop)
            // ==========================================
            GeneralFrameInfoBE generalFrameInfo = SetupCameraCapture(configData.TargetCamera);

            // setup timer thread to pull camera images at a fixed interval, every (1000 / TARGET_FPS) mSec
            _cameraThread = new System.Threading.Timer((o) =>
            {
                ProcessCameraFrameV2(generalFrameInfo, configData);
            }, null, 0, (int)(1000 / configData.TargetCamera.TargetFPS));

            // ==========================================
            // Step 5: setup tcp socket image data server
            // ==========================================

            if (configData.VisionDataServer.IsEnabled)
            {
                _tcpServer = new SimpleTcpServer().Start(configData.VisionDataServer.TCPPort);

                #region setup event handler to respond to incoming packets
                //server.DelimiterDataReceived += (sender, msg) =>
                _tcpServer.DataReceived += (sender, msg) =>
                {
                    _rwl.EnterReadLock();
                    try
                    {
                        string serverReply = string.Empty;

                    // It's safe for this thread to access the shared resource.
                    switch (configData.VisionDataServer.MessageFormat)
                        {
                            case @"V1":
                            //  	    	vector = _rrAPI.getVariables("SW_X,SW_Y,SE_X,SE_Y,HI_MID_Y,SCREEN_WIDTH,SCREEN_HEIGHT,BLOB_COUNT,CamType");
                            serverReply = "<response>"
                                                    + "<SW_X>" + _targetInfo.SW_X + "</SW_X>"
                                                    + "<SW_Y>" + _targetInfo.SW_Y + "</SW_Y>"
                                                    + "<SE_X>" + _targetInfo.SE_X + "</SE_X>"
                                                    + "<SE_Y>" + _targetInfo.SE_Y + "</SE_Y>"
                                                    + "<HI_MID_Y>" + _targetInfo.HighMiddleY + "</HI_MID_Y>"
                                                    + "<SCREEN_WIDTH>" + generalFrameInfo.Width + "</SCREEN_WIDTH>"
                                                    + "<SCREEN_HEIGHT>" + generalFrameInfo.Height + "</SCREEN_HEIGHT>"
                                                    + "<BLOB_COUNT>" + 1 + "</BLOB_COUNT>"
                                                    + "<CamType>" + "BOILER" + "</CamType>"
                                                 + "</response>";
                                break;

                            case @"V2":
                            default:
                                serverReply = "<response>"
                                               + "<IS_VALID>" + _targetInfo.IsTargetInFOV + "</IS_VALID>"
                                               + "<SW_X>" + _targetInfo.SW_X + "</SW_X>"
                                               + "<SW_Y>" + _targetInfo.SW_Y + "</SW_Y>"
                                               + "<SE_X>" + _targetInfo.SE_X + "</SE_X>"
                                               + "<SE_Y>" + _targetInfo.SE_Y + "</SE_Y>"
                                               + "<HI_MID_Y>" + _targetInfo.HighMiddleY + "</HI_MID_Y>"
                                               + "<FPS>" + _targetInfo.FramesPerSec + "</FPS>"
                                               + "<FRAMECTR>" + _targetInfo.FrameCounter + "</FRAMECTR>"
                                               + "<FRAMEMS>" + _targetInfo.PerFrameMsec + "</FRAMEMS>"
                                               + "<CPU>" + _targetInfo.CpuUsage + "</CPU>"
                                               + "<BATTERY>" + _targetInfo.BatteryChargeLevel + "</BATTERY>"
                                            + "</response>";
                                break;
                        }
                        msg.ReplyLine(serverReply);
                    }
                    finally
                    {
                    // Ensure that the lock is released.
                    _rwl.ExitReadLock();
                    }
                };
                #endregion
            }

            // ==========================================
            // Step 6: setup network tables client
            // ==========================================

            if (configData.NetworkTablesClient.IsEnabled)
            {
                _networkTablesClient = new NetworkTablesClient(configData.NetworkTablesClient);
            }

            // ==========================================
            // Step 7: setup mjpeg server
            // ==========================================

            if (configData.MPEGServer.IsEnabled)
            {
                // config the MPEG Server
                _mpegServer = new ImageStreamingServer(OpenCVImageSource.Snapshots(configData.MPEGServer.ImageWidth, configData.MPEGServer.ImageHeight));
                //_mpegServer = new ImageStreamingServer(ScreenshotImageSource.Snapshots(MPEG_FRAME_WIDTH, MPEG_FRAME_HEIGHT));

                // start the MPEG Server
                _mpegServer.Start(configData.MPEGServer.TCPPort);
            }

            // ==========================================
            // Step 8: setup blinkstrip heartbeat (run 1x sec)
            // ==========================================

            if (configData.USBBlinkStick.IsEnabled)
            {
                _blinkStickCtlr = new BlinkStickController(configData.USBBlinkStick, configData.TargetCamera.TargetFPS);
                if (_blinkStickCtlr.IsAvailable)
                {
                    _heartbeatThread = new System.Threading.Timer((o) =>
                    {
                        _rwl.EnterReadLock();
                        try
                        {
                            _blinkStickCtlr.ToggleLED(_targetInfo);
                        }
                        finally
                        {
                            // Ensure that the lock is released.
                            _rwl.ExitReadLock();
                        }
                
                    }, null, 0, configData.USBBlinkStick.HeartbeatIntervalMsec);
                }
            }
        }

        /// <summary>
        /// Stops all running threads
        /// </summary>
        public void Stop()
        {
            // Step 1: Stop MPEG Thread
            if(_mpegServer != null)
            {
                //_mpegServer.Stop();       // this command seems to hang
            }

            // Step 2: Stop Data Socket Thread
            if(_tcpServer != null)
            {
                //_tcpServer.Stop();
            }

            // Step 3: Stop Image Polling Thread
            if(_cameraThread != null)
            {
                try
                {
                    //_timerThread.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// Sets up the camera capture parameters.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>FrameInfoBE.</returns>
        GeneralFrameInfoBE SetupCameraCapture(TargetCameraBE config)
        {
            _capture = new VideoCapture(config.USBCamera);

            #region CaptureProperty enum values
            /*
             *  from https://docs.opencv.org/3.3.0/d4/d15/group__videoio__flags__base.html
             *  
                PosMsec = 0,            CAP_PROP_POS_MSEC Current position of the video file in milliseconds.
                PosFrames = 1,          CAP_PROP_POS_FRAMES 0-based index of the frame to be decoded/captured next.
                PosAviRatio = 2,        CAP_PROP_POS_AVI_RATIO Relative position of the video file: 0 - start of the film, 1 - end of the film.
                FrameWidth = 3,         CAP_PROP_FRAME_WIDTH Width of the frames in the video stream.
                FrameHeight = 4,        CAP_PROP_FRAME_HEIGHT Height of the frames in the video stream.
                Fps = 5,                CAP_PROP_FPS Frame rate.
                FourCC = 6,             CAP_PROP_FOURCC 4-character code of codec.
                FrameCount = 7,         CAP_PROP_FRAME_COUNT Number of frames in the video file.
                Format = 8,             CAP_PROP_FORMAT Format of the Mat objects returned by retrieve() .
                Mode = 9,               CAP_PROP_MODE Backend-specific value indicating the current capture mode.
                Brightness = 10,        CAP_PROP_BRIGHTNESS Brightness of the image (only for cameras).
                Contrast = 11,          CAP_PROP_CONTRAST Contrast of the image (only for cameras).
                Saturation = 12,        CAP_PROP_SATURATION Saturation of the image (only for cameras).
                Hue = 13,               CAP_PROP_HUE Hue of the image (only for cameras).
                Gain = 14,              CAP_PROP_GAIN Gain of the image (only for cameras).
                Exposure = 15,          CAP_PROP_EXPOSURE Exposure (only for cameras).
                ConvertRgb = 16,        CAP_PROP_CONVERT_RGB Boolean flags indicating whether images should be converted to RGB.
                WhiteBalanceBlueU = 17, CAP_PROP_WHITE_BALANCE Currently unsupported
                Rectification = 18,     CAP_PROP_RECTIFICATION Rectification flag for stereo cameras (note: only supported by DC1394 v 2.x backend currently) 
                Monochrome = 19,
                Sharpness = 20,
                AutoExposure = 21,
                Gamma = 22,
                Temperature = 23,
                Trigger = 24,
                TriggerDelay = 25,
                WhiteBalanceRedV = 26,
                Zoom = 27,
                Focus = 28,
                Guid = 29,
                IsoSpeed = 30,
                MaxDC1394 = 31,
                Backlight = 32,
                Pan = 33,
                Tilt = 34,
                Roll = 35,
                Iris = 36,
                Settings = 37,
                Buffersuze = 38,
                Autofocus = 39,
                SarNum = 40,
                SarDen = 41,
             */
            #endregion

            // config camera image size
            _capture.SetCaptureProperty(CapProp.FrameWidth, config.HorizontalResolution);
            _capture.SetCaptureProperty(CapProp.FrameHeight, config.VerticalResolution);

            // config camera image properties (only set if value provided in config file)

            if (config.Exposure.HasValue)
            { _capture.SetCaptureProperty(CapProp.Exposure, config.Exposure.Value); }

            if (config.Brightness.HasValue)
            { _capture.SetCaptureProperty(CapProp.Brightness, config.Brightness.Value); }

            if (config.Contrast.HasValue)
            { _capture.SetCaptureProperty(CapProp.Contrast, config.Contrast.Value); }

            if (config.Saturation.HasValue)
            { _capture.SetCaptureProperty(CapProp.Gain, config.Saturation.Value); }

            if (config.Sharpness.HasValue)
            { _capture.SetCaptureProperty(CapProp.Sharpness, config.Sharpness.Value); }

            if (config.Gain.HasValue)
            { _capture.SetCaptureProperty(CapProp.Gain, config.Gain.Value); }

            // On each startup, read and store camera properties to a text file for debugging
            CapProp property;
            string fileName = @"Camera_" + DateTime.Now.ToString("yyyyMMddHHmmss") + @".txt";
            using (var dest = File.AppendText(Path.Combine(@".\", fileName)))
            {

                for (int propIndex = 0; propIndex<=41; propIndex++)
                {
                    property = (CapProp)propIndex;
                    dest.WriteLine($"GetCaptureProperty: [{property}][{propIndex}] [{_capture.GetCaptureProperty(property)}]");
                }
            }

            // get some basic information about a camera frame
            using (Mat frame = new Mat())
            {
                _capture.Read(frame);

                int frameHeight = frame.Height;
                int frameWidth = frame.Width;
                int frameCenter_X = (int)(frameWidth / 2);
                int frameCenter_Y = (int)(frameHeight / 2);

                System.Drawing.Point centerPoint = new System.Drawing.Point(frameCenter_X, frameCenter_Y);
                System.Drawing.Point centerPointHorizLeft = new System.Drawing.Point(frameCenter_X - 100, frameCenter_Y);
                System.Drawing.Point centerPointHorizRight = new System.Drawing.Point(frameCenter_X + 100, frameCenter_Y);
                System.Drawing.Point centerPointVertTop = new System.Drawing.Point(frameCenter_X, frameCenter_Y + 100);
                System.Drawing.Point centerPointVertBottom = new System.Drawing.Point(frameCenter_X, frameCenter_Y - 100);

                // calc label positions on the frame based on camera resolution
                System.Drawing.Point fpsLabelPoint = new System.Drawing.Point((int)(.78M * frameWidth), (int)(.93M * frameHeight));
                System.Drawing.Point offsetLabelPoint = new System.Drawing.Point((int)(.12M * frameWidth), (int)(.93M * frameHeight));
                System.Drawing.Point estDistanceLabelPoint = new System.Drawing.Point((int)(.12M * frameWidth), (int)(.95M * frameHeight));

                // build object containing information about the frame in general
                return new GeneralFrameInfoBE()
                {
                    Height = frameHeight,
                    Width = frameWidth,
                    CenterPoint = centerPoint,
                    CenterPointHorizLeft = centerPointHorizLeft,
                    CenterPointHorizRight = centerPointHorizRight,
                    CenterPointVertTop = centerPointVertTop,
                    CenterPointVertBottom = centerPointVertBottom,
                    FpsLabelPoint = fpsLabelPoint,
                    OffsetLabelPoint = offsetLabelPoint,
                    EstDistanceLabelPoint = estDistanceLabelPoint
                };
            }
        }

        #region Version 1 of ProcessCameraFrame
        ///// <summary>
        ///// Processes the camera frame.
        ///// </summary>
        ///// <param name="frameInfo">The frame information.</param>
        //void ProcessCameraFrame(GeneralFrameInfoBE frameInfo, ConfigDataBE configData)
        //{
        //    _frameStopWatch.Restart();

        //    var (batteryChargeLevel, cpuUsage) = GetComputerPerfInfo();

        //    // all in a using block since they are wrappers around unmanaged memory
        //    // since they are in a event handler this would be a continuous memory leak
        //    using (Mat currentFrame = new Mat())
        //    using (Mat hsv = new Mat())
        //    using (Mat mask = new Mat())
        //    using (Mat res = new Mat())
        //    using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
        //    {
        //        //====================================================================
        //        //Set up Rectangle Values
        //        int largestContourHeight = 0;
        //        int largestContourWidth = 0;
        //        int largestRectX = 0;
        //        int largestRectY = 0;
        //        int secondLargestContourHeight = 0;
        //        int secondLargestContourWidth = 0;
        //        int secondLargestRectX = 0;
        //        int secondLargestRectY = 0;
        //        int thirdLargestContourHeight = 0;
        //        int thirdLargestContourWidth = 0;

        //        //====================================================================
        //        //Set Up Ratio Variables
        //        decimal HeightRatio1to2 = 0;
        //        decimal WidthRatio1to2 = 0;
        //        decimal HeightRatio2to3 = 0;
        //        decimal WidthRatio2to3 = 0;
        //        decimal HeightRatio1to3 = 0;
        //        decimal WidthRatio1to3 = 0;

        //        //====================================================================
        //        // local working variables 
        //        int centerOfTarget_X = 0;
        //        int centerOfTarget_Y = 0;
        //        int targetCornerPoint_X = 0;
        //        int targetCornerPoint_Y = 0;
        //        int targetHeight = 0;
        //        int targetWidth = 0;
        //        System.Drawing.Point targetCenterPoint;
        //        System.Drawing.Point targetCenterPointHL;
        //        System.Drawing.Point targetCenterPointHR;
        //        System.Drawing.Point targetCenterPointVT;
        //        System.Drawing.Point targetCenterPointVB;

        //        // Manage FPS averaging, calc over 10 frames
        //        // increment  counter
        //        _fpsLoopCounter++;

        //        if (_fpsLoopCounter == 1)
        //        {
        //            // 1st time thru loop
        //            _fpsStopWatch.Restart();
        //        }
        //        else if (_fpsLoopCounter == 10)
        //        {
        //            // Calculate frames per sec.
        //            long elapsedMsec = _fpsStopWatch.ElapsedMilliseconds;
        //            _fps = (int)((1000.0M / elapsedMsec) * 10);

        //            // reset counter
        //            _fpsLoopCounter = 0;
        //        }

        //        // Read a BGR frame from the camera and store in "currentFrame"
        //        _capture.Read(currentFrame);

        //        // Convert BGR frame to HSV format so that you can more easily filter on a color
        //        CvInvoke.CvtColor(currentFrame, hsv, ColorConversion.Bgr2Hsv);

        //        // Threshold the HSV image to get only green colors, based on LOWERGREEN, UPPERGREEN
        //        CvInvoke.InRange(hsv, new ScalarArray(LOWER_GREEN.MCvScalar), new ScalarArray(UPPER_GREEN.MCvScalar), mask);

        //        // Bitwise-AND mask and original image and the green mask to get a final result that "only" has the green colors.
        //        CvInvoke.BitwiseAnd(currentFrame, currentFrame, res, mask);

        //        // #make a copy of mask, some documents suggest that the contours function changes the image that is passed.
        //        using (Mat maskcopy = mask.Clone())
        //        {
        //            // Find the contours
        //            CvInvoke.FindContours(maskcopy, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
        //            //CvInvoke.DrawContours(currentFrame, contours, -1, CONTOUR_COLOR, 2);
        //        }

        //        // Draw Cross Hairs At Center of Frame
        //        // Draw a circle using center and radius of target
        //        CvInvoke.Circle(currentFrame, frameInfo.CenterPoint, 10, CTR_OF_SCREEN_COLOR, 1);
        //        // Draw a red horizontal line
        //        CvInvoke.Line(currentFrame, frameInfo.CenterPointHorizLeft, frameInfo.CenterPointHorizRight, CTR_OF_SCREEN_COLOR, 1);
        //        CvInvoke.Line(currentFrame, frameInfo.CenterPointVertBottom, frameInfo.CenterPointVertTop, CTR_OF_SCREEN_COLOR, 1);

        //        //==============================================================
        //        //Manage Defining Objects + Define Object Existance Booleans
        //        bool obj1Exists = false;
        //        bool obj2Exists = false;
        //        bool obj3Exists = false;

        //        if (contours.Size > 1)
        //        {

        //            double maxContourArea = 0;

        //            List<FoundObjectBE> foundObjects = new List<FoundObjectBE>();

        //            for (int index = 0; index < contours.Size; index++)
        //            {
        //                var contour = contours[index];
        //                double coutourArea = CvInvoke.ContourArea(contour, false);

        //                foundObjects.Add(new FoundObjectBE(contour, new Rectangle(), coutourArea));
        //            }

        //            foundObjects = foundObjects.OrderByDescending(fo => fo.Area).ToList();

        //            VectorOfPoint largestContour = null; 
        //            if (foundObjects.Count > 0)
        //            {
        //                largestContour = foundObjects[0].Contour;
        //                obj1Exists = true;
        //            }

        //            VectorOfPoint secondlargestContour = null;
        //            if (foundObjects.Count > 1)
        //            {
        //                secondlargestContour = foundObjects[1].Contour;
        //                obj2Exists = true;
        //            }

        //            VectorOfPoint thirdlargestContour = null;

        //            if (foundObjects.Count > 2)
        //            {
        //                thirdlargestContour = foundObjects[2].Contour;
        //                obj3Exists = true;
        //            }

        //            //====================================================================
        //            //Gather Values for Objects
        //            if (obj1Exists)
        //            {
        //                //CvInvoke.DrawContours(currentFrame, largestContour, -1, CONTOUR_COLOR, 3);

        //                System.Drawing.Rectangle obj1Rect = CvInvoke.BoundingRectangle(largestContour);
        //                largestContourHeight = obj1Rect.Height;
        //                largestContourWidth = obj1Rect.Width;
        //                largestRectX = obj1Rect.Left;
        //                largestRectY = obj1Rect.Top;
        //            }
        //            if (obj2Exists)
        //            {
        //                //CvInvoke.DrawContours(currentFrame, secondlargestContour, -1, CONTOUR_COLOR, 3);

        //                System.Drawing.Rectangle obj2Rect = CvInvoke.BoundingRectangle(secondlargestContour);
        //                secondLargestContourHeight = obj2Rect.Height;
        //                secondLargestContourWidth = obj2Rect.Width;
        //                secondLargestRectX = obj2Rect.Left;
        //                secondLargestRectY = obj2Rect.Top;
        //            }
        //            if (obj3Exists)
        //            {
        //                //CvInvoke.DrawContours(currentFrame, thirdlargestContour, -1, CONTOUR_COLOR, 3);

        //                System.Drawing.Rectangle obj3Rect = CvInvoke.BoundingRectangle(thirdlargestContour);
        //                thirdLargestContourHeight = obj3Rect.Height;
        //                thirdLargestContourWidth = obj3Rect.Width;
        //            }

        //            //====================================================================
        //            //Calculate the Ratios
        //            if (obj1Exists && obj2Exists)
        //            {
        //                HeightRatio1to2 = largestContourHeight / secondLargestContourHeight;
        //                WidthRatio1to2 = largestContourWidth / secondLargestContourWidth;
        //            }
        //            if (obj2Exists && obj3Exists)
        //            {
        //                HeightRatio2to3 = secondLargestContourHeight / thirdLargestContourHeight;
        //                WidthRatio2to3 = secondLargestContourWidth / thirdLargestContourWidth;
        //            }
        //            if (obj1Exists && obj3Exists)
        //            {
        //                HeightRatio1to3 = largestContourHeight / thirdLargestContourHeight;
        //                WidthRatio1to3 = largestContourWidth / thirdLargestContourWidth;
        //            }

        //            //====================================================================
        //            //Check if Ratios are within Limits
        //            bool is1to2WISpec = false;
        //            bool is2to3WISpec = false;
        //            bool is1to3WISpec = false;

        //            if (configData.TargetRatios.RatioMinH <= HeightRatio1to2
        //                    && HeightRatio1to2 <= configData.TargetRatios.RatioMaxH
        //                && configData.TargetRatios.RatioMinW <= WidthRatio1to2
        //                    && WidthRatio1to2 <= configData.TargetRatios.RatioMaxW)
        //            {
        //                is1to2WISpec = true;
        //            }
        //            if (configData.TargetRatios.RatioMinH <= HeightRatio2to3
        //                    && HeightRatio2to3 <= configData.TargetRatios.RatioMaxH
        //                && configData.TargetRatios.RatioMinW <= WidthRatio2to3
        //                    && WidthRatio2to3 <= configData.TargetRatios.RatioMaxW)
        //            {
        //                is2to3WISpec = true;
        //            }
        //            if (configData.TargetRatios.RatioMinH <= HeightRatio1to3
        //                    && HeightRatio1to3 <= configData.TargetRatios.RatioMaxH
        //                && configData.TargetRatios.RatioMinW <= WidthRatio1to3
        //                    && WidthRatio1to3 <= configData.TargetRatios.RatioMaxW)
        //            {
        //                is1to3WISpec = true;
        //            }

        //            //====================================================================
        //            //Make decision on which Object to Use as Goal
        //            bool targetExists = false;

        //            if (is1to2WISpec)
        //            {
        //                targetCornerPoint_X = largestRectX;
        //                targetCornerPoint_Y = largestRectY;
        //                targetHeight = largestContourHeight;
        //                targetWidth = largestContourWidth;

        //                targetExists = true;
        //            }
        //            else if (is2to3WISpec)
        //            {
        //                targetCornerPoint_X = secondLargestRectX;
        //                targetCornerPoint_Y = secondLargestRectY;
        //                targetHeight = secondLargestContourHeight;
        //                targetWidth = secondLargestContourWidth;

        //                targetExists = true;
        //            }
        //            else if (is1to3WISpec)
        //            {
        //                targetCornerPoint_X = largestRectX;
        //                targetCornerPoint_Y = largestRectY;
        //                targetHeight = largestContourHeight;
        //                targetWidth = largestContourWidth;

        //                targetExists = true;
        //            }

        //            //====================================================================
        //            //Deal with Target if it Exists
        //            if (targetExists)
        //            {
        //                //Calculate Data to send to Roborio

        //                centerOfTarget_X = targetCornerPoint_X + (targetWidth / 2);
        //                centerOfTarget_Y = targetCornerPoint_Y + (targetHeight / 2);

        //                //====================================================================
        //                //Draw Target and Line of Travel
        //                targetCenterPoint = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y);
        //                targetCenterPointHL = new System.Drawing.Point(centerOfTarget_X - 10, centerOfTarget_Y);
        //                targetCenterPointHR = new System.Drawing.Point(centerOfTarget_X + 10, centerOfTarget_Y);
        //                targetCenterPointVT = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y + 10);
        //                targetCenterPointVB = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y - 10);

        //                // Draw a circle using center and radius of target
        //                CvInvoke.Circle(currentFrame, targetCenterPoint, 10, TARGET_COLOR, 1);
        //                // Draw a red horizontal line
        //                CvInvoke.Line(currentFrame, targetCenterPointHL, targetCenterPointHR, TARGET_COLOR, 1);
        //                // Draw a red horizontal line
        //                CvInvoke.Line(currentFrame, targetCenterPointVB, targetCenterPointVT, TARGET_COLOR, 1);

        //                // acquire a write lock (to be thread safe)
        //                // fyi: the other thread is the Socket Server
        //                _rwl.EnterWriteLock();
        //                try
        //                {
        //                    // It's safe for this thread to access from the shared resource.
        //                    // calc data for socket server
        //                    _targetInfo.IsTargetInFOV = true;
        //                    _targetInfo.SW_X = targetCornerPoint_X;
        //                    _targetInfo.SW_Y = targetCornerPoint_Y - targetHeight;
        //                    _targetInfo.SE_X = targetCornerPoint_X + targetWidth;
        //                    _targetInfo.SE_Y = targetCornerPoint_Y - targetHeight;
        //                    _targetInfo.HighMidY = centerOfTarget_Y;
        //                    _targetInfo.FrameCounter++;
        //                    _targetInfo.FramesPerSec = _fps;
        //                    _targetInfo.PerFrameMsec = _frameStopWatch.ElapsedMilliseconds;
        //                    _targetInfo.BatteryChargeLevel = batteryChargeLevel;
        //                    _targetInfo.CpuUsage = cpuUsage;
        //                }
        //                finally
        //                {
        //                    // Ensure that the lock is released.
        //                    _rwl.ExitWriteLock();
        //                }
        //            }
        //            else
        //            {
        //                // target is NOT recognized in FOV (Field Of View)

        //                // acquire a write lock (to be thread safe)
        //                // fyi: the other thread is the Socket Server

        //                _rwl.EnterWriteLock();
        //                try
        //                {
        //                    // It's safe for this thread to access from the shared resource.
        //                    // calc data for socket server
        //                    _targetInfo.IsTargetInFOV = false;
        //                    _targetInfo.SW_X = -1;
        //                    _targetInfo.SW_Y = -1;
        //                    _targetInfo.SE_X = -1;
        //                    _targetInfo.SE_Y = -1;
        //                    _targetInfo.HighMidY = -1;
        //                    _targetInfo.FrameCounter++;
        //                    _targetInfo.FramesPerSec = _fps;
        //                    _targetInfo.PerFrameMsec = _frameStopWatch.ElapsedMilliseconds;
        //                    _targetInfo.BatteryChargeLevel = batteryChargeLevel;
        //                    _targetInfo.CpuUsage = cpuUsage;
        //                }
        //                finally
        //                {
        //                    // Ensure that the lock is released.
        //                    _rwl.ExitWriteLock();
        //                }
        //            }
        //        }
        //        //==============================================================

        //        CvInvoke.PutText(currentFrame, $"FPS: {_fps}", frameInfo.FpsLabelPoint, FontFace.HersheySimplex, 1, FPS_COLOR, 2, LineType.AntiAlias);

        //        OpenCVImageSource.CurrentFrame = currentFrame.Clone();
        //    }
        //}

        #endregion

        /// <summary>
        /// Version 2 of Logic to Process a camera frame.
        /// </summary>
        /// <param name="frameInfo">The frame information.</param>
        /// <param name="configData">The configuration data.</param>
        /// <remarks>
        /// implement a different, hopefully more robust approach
        /// 1st pass filters objects by min/max height to width ratios
        /// 2nd pass tries to find the matching top & bottom band using min/max area ratio
        /// </remarks>
        void ProcessCameraFrameV2(GeneralFrameInfoBE frameInfo, ConfigDataBE configData)
        {
            _frameStopWatch.Restart();

            var (batteryChargeLevel, cpuUsage) = GeneralUtilities.GetComputerPerfInfo(_wmi, _cpuPerfCtr);

            // all in a using block since they are wrappers around unmanaged memory
            // since they are in a event handler this would be a continuous memory leak
            using (Mat currentFrame = new Mat())
            using (Mat hsv = new Mat())
            using (Mat mask = new Mat())
            using (Mat res = new Mat())
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                // Manage FPS averaging, calc over 10 frames
                // increment  counter
                _fpsLoopCounter++;

                //if (_fpsLoopCounter == 1)
                //{
                //    // 1st time thru loop
                //    _fpsStopWatch.Restart();
                //}
                //else if (_fpsLoopCounter == 10)
                //{
                //    // Calculate frames per sec (FPS).
                //    // from start of 1st frame to start of 10th frame
                //    long elapsedMsec = _fpsStopWatch.ElapsedMilliseconds;
                //    _fps = (int)((1000.0M / elapsedMsec) * 10);

                //    // reset counter
                //    _fpsLoopCounter = 0;
                //}

                int elapsedMsec = (int)_fpsStopWatch.ElapsedMilliseconds;
                _fpsMovingAverage.AddSample(elapsedMsec);

                // Read a BGR frame from the camera and store in "currentFrame"
                _capture.Read(currentFrame);

                // Convert BGR frame to HSV format so that you can more easily filter on a color
                CvInvoke.CvtColor(currentFrame, hsv, ColorConversion.Bgr2Hsv);

                // Threshold the HSV image to get only green colors, based on LOWERGREEN, UPPERGREEN
                CvInvoke.InRange(hsv, new ScalarArray(LOWER_GREEN.MCvScalar), new ScalarArray(UPPER_GREEN.MCvScalar), mask);

                // Bitwise-AND mask and original image and the green mask to get a final result that "only" has the green colors.
                CvInvoke.BitwiseAnd(currentFrame, currentFrame, res, mask);

                // make a copy of mask, some documents suggest that the contours function changes the image that is passed.
                using (Mat maskcopy = mask.Clone())
                {
                    // Find the contours
                    CvInvoke.FindContours(maskcopy, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    
                    // To Debug, draw all the raw contours
                    // CvInvoke.DrawContours(currentFrame, contours, -1, CONTOUR_COLOR, 2);
                }

                // Draw Cross Hairs At Center of Frame
                CvInvoke.Circle(currentFrame, frameInfo.CenterPoint, 10, CTR_OF_SCREEN_COLOR, 1);
                CvInvoke.Line(currentFrame, frameInfo.CenterPointHorizLeft, frameInfo.CenterPointHorizRight, CTR_OF_SCREEN_COLOR, 1);
                CvInvoke.Line(currentFrame, frameInfo.CenterPointVertBottom, frameInfo.CenterPointVertTop, CTR_OF_SCREEN_COLOR, 1);

                // Did we find any objects?
                if (contours.Size >= 1)
                {
                    List<FoundObjectBE> foundObjects = new List<FoundObjectBE>();
                    VectorOfVectorOfPoint targetContours = new VectorOfVectorOfPoint();

                    // loop over each found object, build a collection of Found Objects with some metadata
                    for (int index = 0; index < contours.Size; index++)
                    {
                        var contour = contours[index];
                        // calc the area
                        double contourArea = CvInvoke.ContourArea(contour, false);

                        // calc the bounding rectangle
                        System.Drawing.Rectangle boundingRectangle = CvInvoke.BoundingRectangle(contour);

                        // calc the Height to Width Ratio for the bounding rectangle
                        double h2wRatio = (double)(boundingRectangle.Height) / (double)(boundingRectangle.Width);

                        // 1st pass filter, include only objects with h2wRatio w/i config params
                        if (h2wRatio >= (double)configData.TargetRatios.HeightToWidthRatioMin
                            && h2wRatio <= (double)configData.TargetRatios.HeightToWidthRatioMax)
                        {
                            foundObjects.Add(new FoundObjectBE(contour, boundingRectangle, contourArea));
                        }
                    }

                    // sort largest to smallest on area
                    foundObjects = foundObjects.OrderByDescending(fo => fo.Area).ToList();

                    FoundObjectBE topTarget = null;
                    FoundObjectBE bottomTarget = null;

                    // loop over all the sorted objects
                    // compare current object in loop to all smaller objects
                    for (int loopCtr = 0; loopCtr < foundObjects.Count; loopCtr++)
                    {
                        topTarget = foundObjects[loopCtr];
                        // if we assume current object is the top object
                        //  compare to all smaller objects (ie after it in the array)
                        //  and see if we can find one that has the correct ratio of areas
                        // if we find this object then we have the top & bottom band recognized
                        bottomTarget = FindSibling(foundObjects, loopCtr, configData);

                        if (bottomTarget != null)
                        {
                            // we found the top + bottom pair, so stop searching!
                            break;
                        }
                    }

                    if (topTarget != null)
                    {
                        // add the top (larger) object to the targetContours collection
                        targetContours.Push(topTarget.Contour);
                    }

                    if (bottomTarget != null)
                    {
                        // add the bottom (smaller) object to the targetContours collection
                        targetContours.Push(bottomTarget.Contour);
                    }

                    if (targetContours.Size > 0)
                    {
                        // draw a border around the target contours
                        CvInvoke.DrawContours(currentFrame, targetContours, -1, CONTOUR_COLOR, 3);
                    }

                    // if we have identified both targets then we have valid data
                    if (topTarget != null && bottomTarget != null)
                    {
                        // calc location of the center of the top target
                        int centerOfTarget_X = topTarget.BoundingRectangle.Left + (topTarget.BoundingRectangle.Width / 2);
                        int centerOfTarget_Y = topTarget.BoundingRectangle.Top + (topTarget.BoundingRectangle.Height / 2);

                        // Calc cross hairs location on the target
                        var targetCenterPoint = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y);
                        var targetCenterPointHL = new System.Drawing.Point(centerOfTarget_X - 10, centerOfTarget_Y);
                        var targetCenterPointHR = new System.Drawing.Point(centerOfTarget_X + 10, centerOfTarget_Y);
                        var targetCenterPointVT = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y + 10);
                        var targetCenterPointVB = new System.Drawing.Point(centerOfTarget_X, centerOfTarget_Y - 10);

                        // Draw a cross hairs on the target's top band
                        CvInvoke.Circle(currentFrame, targetCenterPoint, 10, TARGET_COLOR, 1);
                        CvInvoke.Line(currentFrame, targetCenterPointHL, targetCenterPointHR, TARGET_COLOR, 1);
                        CvInvoke.Line(currentFrame, targetCenterPointVB, targetCenterPointVT, TARGET_COLOR, 1);

                        // calc offset from center of frame
                        int deltaX = centerOfTarget_X - frameInfo.CenterPoint.X;
                        int deltaY = frameInfo.CenterPoint.Y - centerOfTarget_Y;

                        // calc the estimated distance
                        int estDistanceInches = CalcEstimatedDistance(centerOfTarget_Y, configData.DistanceEstPolynomialCoefficients);

                        // write the offset amount to the frame
                        CvInvoke.PutText(currentFrame, $"Offset: {deltaX},{deltaY}", frameInfo.OffsetLabelPoint, FontFace.HersheySimplex, 1, OFFSET_COLOR, 2, LineType.AntiAlias);
                        // write the est distance to the frame
                        CvInvoke.PutText(currentFrame, $"Est Dist (in): {estDistanceInches}", frameInfo.EstDistanceLabelPoint, FontFace.HersheySimplex, 1, OFFSET_COLOR, 2, LineType.AntiAlias);

                        // acquire a write lock (to be thread safe)
                        // fyi: the other thread is the Socket Server
                        _rwl.EnterWriteLock();
                        try
                        {
                            // It's safe for this thread to access from the shared resource.
                            // calc data for socket server
                            _targetInfo.IsTargetInFOV = true;
                            _targetInfo.SW_X = topTarget.BoundingRectangle.Left;
                            _targetInfo.SW_Y = topTarget.BoundingRectangle.Top;
                            _targetInfo.SE_X = topTarget.BoundingRectangle.Right;
                            _targetInfo.SE_Y = topTarget.BoundingRectangle.Bottom;
                            _targetInfo.HighMiddleY = centerOfTarget_Y;

                            _targetInfo.Delta_X = deltaX;
                            _targetInfo.Delta_Y = deltaY;
                            _targetInfo.Estimated_Distance_Inches = estDistanceInches;

                            _targetInfo.FrameCounter++;
                            _targetInfo.FramesPerSec = _fpsMovingAverage.Current;
                            _targetInfo.PerFrameMsec = _frameStopWatch.ElapsedMilliseconds;
                            _targetInfo.BatteryChargeLevel = batteryChargeLevel;
                            _targetInfo.CpuUsage = cpuUsage;
                        }
                        finally
                        {
                            // Ensure that the lock is released.
                            _rwl.ExitWriteLock();
                        }
                    }
                    else
                    {
                        // contours found, but we did not find a top & bottom pair

                        // acquire a write lock (to be thread safe)
                        // fyi: the other thread is the Socket Server

                        _rwl.EnterWriteLock();
                        try
                        {
                            // It's safe for this thread to access from the shared resource.
                            // calc data for socket server
                            _targetInfo.IsTargetInFOV = false;
                            _targetInfo.SW_X = -1;
                            _targetInfo.SW_Y = -1;
                            _targetInfo.SE_X = -1;
                            _targetInfo.SE_Y = -1;
                            _targetInfo.HighMiddleY = -1;

                            _targetInfo.Delta_X = -1;
                            _targetInfo.Delta_Y = -1;
                            _targetInfo.Estimated_Distance_Inches = -1;

                            _targetInfo.FrameCounter++;
                            _targetInfo.FramesPerSec = _fpsMovingAverage.Current;
                            _targetInfo.PerFrameMsec = _frameStopWatch.ElapsedMilliseconds;
                            _targetInfo.BatteryChargeLevel = batteryChargeLevel;
                            _targetInfo.CpuUsage = cpuUsage;
                        }
                        finally
                        {
                            // Ensure that the lock is released.
                            _rwl.ExitWriteLock();
                        }
                    }
                }
                else
                {
                    // no contours found

                    // acquire a write lock (to be thread safe)
                    // fyi: the other thread is the Socket Server

                    _rwl.EnterWriteLock();
                    try
                    {
                        // It's safe for this thread to access from the shared resource.
                        // calc data for socket server
                        _targetInfo.IsTargetInFOV = false;
                        _targetInfo.SW_X = -1;
                        _targetInfo.SW_Y = -1;
                        _targetInfo.SE_X = -1;
                        _targetInfo.SE_Y = -1;
                        _targetInfo.HighMiddleY = -1;

                        _targetInfo.Delta_X = -1;
                        _targetInfo.Delta_Y = -1;
                        _targetInfo.Estimated_Distance_Inches = -1;

                        _targetInfo.FrameCounter++;
                        _targetInfo.FramesPerSec = _fpsMovingAverage.Current;
                        _targetInfo.PerFrameMsec = _frameStopWatch.ElapsedMilliseconds;
                        _targetInfo.BatteryChargeLevel = batteryChargeLevel;
                        _targetInfo.CpuUsage = cpuUsage;
                    }
                    finally
                    {
                        // Ensure that the lock is released.
                        _rwl.ExitWriteLock();
                    }
                }

                // optionally publish the data via network tables
                if (_networkTablesClient != null)
                {
                    _networkTablesClient.Publish(_targetInfo);
                }

                // add the FPS label to the frame
                CvInvoke.PutText(currentFrame, $"FPS: {_fpsMovingAverage.Current}", frameInfo.FpsLabelPoint, FontFace.HersheySimplex, 1, FPS_COLOR, 2, LineType.AntiAlias);

                // store a snapshot (clone) of the frame for the mpeg server
                // we need to do this because that runs async in a different thread
                // and our copy of the frame may be gone when it wants to use it
                OpenCVImageSource.CurrentFrame = currentFrame.Clone();
            }
        }

        #region Utilites

        /// <summary>
        /// Try and find the sibling (bottom) object to a potential top object
        /// </summary>
        /// <param name="foundObjects">The found objects.</param>
        /// <param name="startAt">The start at.</param>
        /// <param name="configData">The configuration data.</param>
        /// <returns>FoundObjectBE.</returns>
        FoundObjectBE FindSibling(List<FoundObjectBE> foundObjects, int baseObjectIndex, ConfigDataBE configData)
        {
            FoundObjectBE baseObject = foundObjects[baseObjectIndex];
            FoundObjectBE thisObject;

            // look thru all the objects after the current object
            for (int loopCtr = baseObjectIndex + 1; loopCtr < foundObjects.Count; loopCtr++)
            {
                thisObject = foundObjects[loopCtr];

                // calc the ratio of the areas of the base object to this object
                double areaRatio = baseObject.Area / thisObject.Area;

                // pick an object where the ratio of the areas is within the deadband
                if (areaRatio >= (double)configData.TargetRatios.AreaRatioMin 
                    && areaRatio <= (double)configData.TargetRatios.AreaRatioMax)
                {
                    return thisObject;
                }
            }

            // no matches found
            return null;
        }

        /// <summary>
        /// Calculates the estimated distance.
        /// </summary>
        /// <param name="highMiddleY">The high middle y.</param>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>System.Int32.</returns>
        int CalcEstimatedDistance(int highMiddleY, DistanceEstPolynomialCoefficientsBE coefficients)
        {
            double estDistanceInInches = ((double)(coefficients.A3) * Math.Pow(highMiddleY, 3))
                                            + ((double)(coefficients.A2) * Math.Pow(highMiddleY, 2))
                                            + (double)(coefficients.A1 * highMiddleY)
                                            + (double)coefficients.A0;

            return (int)estDistanceInInches;
        }

        #endregion
    }
}