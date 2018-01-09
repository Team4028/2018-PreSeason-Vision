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

using rtaNetworking.Streaming;
using FRC4028.VisionServerV3.Entities;
using FRC4028.VisionServerV3.MPEG;
using FRC4028.VisionServerV3.Utilities;
using FRC4028.VisionServerV3.StatusLED;

namespace FRC4028.VisionServerV3
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
        VideoCapture _captureLeft;
        VideoCapture _captureRight;

        // allows multiple threads to be in read mode, 
        // allows one thread to be in write mode with exclusive ownership of the lock,
        ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim();

        Stopwatch _fpsStopWatch;
        Stopwatch _frameStopWatch;  // ie: how long it takes to process each frame

        int _fpsLoopCounter = 0;

        MovingAverage _fpsMovingAverage;

        System.Threading.Timer _cameraThread;
        System.Threading.Timer _heartbeatThread;

        ImageStreamingServer _mpegServer;
        BlinkStickController _blinkStickCtlr;

        // ================================
        // Config Constants
        // ================================
        const string CONFIG_FILENAME = @"ConfigData.json";

        MCvScalar FPS_COLOR = new MCvScalar(0, 0, 255);

        const int MOVING_AVG_WINDOW_SIZE = 10;

        /// <summary>
        /// Starts all threads
        /// </summary>
        public void Start()
        {
            // ==========================================
            // Step 1: load config data
            // ==========================================
            ConfigData2BE configData = GeneralUtilities.GetConfigData(CONFIG_FILENAME);


            // ==========================================
            // Step 3: init some global objects
            // ==========================================
            _fpsStopWatch = new Stopwatch();
            _fpsMovingAverage = new MovingAverage(MOVING_AVG_WINDOW_SIZE);
            _frameStopWatch = new Stopwatch();

            // ==========================================
            // Step 4: setup camera capture (main OpenCV Loop)
            // ==========================================
            GeneralFrameInfoBE generalFrameInfo = SetupCameraCapture(configData.TargetCameras);

            // setup timer thread to pull camera images at a fixed interval, every (1000 / TARGET_FPS) mSec
            _cameraThread = new System.Threading.Timer((o) =>
            {
                ProcessCameraFrameV2(generalFrameInfo, configData);
            }, null, 0, (int)(1000 / configData.TargetCameras.TargetFPS));


            // ==========================================
            // Step 7: setup mjpeg server
            // ==========================================

            if (configData.MPEGServer.IsEnabled)
            {
                // config the MPEG Server
                _mpegServer = new ImageStreamingServer(OpenCVImageSource.Snapshots(configData.MPEGServer.ImageWidth, configData.MPEGServer.ImageHeight));
                //_mpegServer = new ImageStreamingServer(ScreenshotImageSource.Snapshots(MPEG_FRAME_WIDTH, MPEG_FRAME_HEIGHT));

                _mpegServer.Interval = 20;

                // start the MPEG Server
                _mpegServer.Start(configData.MPEGServer.TCPPort);
            }

            // ==========================================
            // Step 8: setup blinkstrip heartbeat (run 1x sec)
            // ==========================================

            if (configData.USBBlinkStick.IsEnabled)
            {
                _blinkStickCtlr = new BlinkStickController(configData.USBBlinkStick);
                if (_blinkStickCtlr.IsAvailable)
                {
                    _heartbeatThread = new System.Threading.Timer((o) =>
                    {
                        _rwl.EnterReadLock();
                        try
                        {
                            _blinkStickCtlr.ToggleLED();
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
            if (_mpegServer != null)
            {
                //_mpegServer.Stop();       // this command seems to hang
            }

            // Step 3: Stop Image Polling Thread
            if (_cameraThread != null)
            {
                try
                {
                    //_timerThread.Dispose();
                }
                catch { }
            }
            if (_blinkStickCtlr != null)
            {
                _blinkStickCtlr.CleanUp();
            }
        }

        /// <summary>
        /// Sets up the camera capture parameters.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>FrameInfoBE.</returns>
        GeneralFrameInfoBE SetupCameraCapture(TargetCamerasBE config)
        {
            _captureLeft = new VideoCapture(config.LeftCamera.USBCameraID);
            if(config.RightCamera.IsEnabled)
            {
                _captureRight = new VideoCapture(config.RightCamera.USBCameraID);
            }

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

            #region Left Camera
            // config camera image size
            _captureLeft.SetCaptureProperty(CapProp.FrameWidth, config.LeftCamera.HorizontalResolution);
            _captureLeft.SetCaptureProperty(CapProp.FrameHeight, config.LeftCamera.VerticalResolution);

            // config camera image properties (only set if value provided in config file)

            if (config.LeftCamera.Exposure.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Exposure, config.LeftCamera.Exposure.Value); }

            if (config.LeftCamera.Brightness.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Brightness, config.LeftCamera.Brightness.Value); }

            if (config.LeftCamera.Contrast.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Contrast, config.LeftCamera.Contrast.Value); }

            if (config.LeftCamera.Saturation.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Gain, config.LeftCamera.Saturation.Value); }

            if (config.LeftCamera.Sharpness.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Sharpness, config.LeftCamera.Sharpness.Value); }

            if (config.LeftCamera.Gain.HasValue)
            { _captureLeft.SetCaptureProperty(CapProp.Gain, config.LeftCamera.Gain.Value); }
            #endregion

            #region Right Camera
            if (config.RightCamera.IsEnabled)
            {
                // config camera image size
                _captureRight.SetCaptureProperty(CapProp.FrameWidth, config.RightCamera.HorizontalResolution);
                _captureRight.SetCaptureProperty(CapProp.FrameHeight, config.RightCamera.VerticalResolution);

                // config camera image properties (only set if value provided in config file)

                if (config.RightCamera.Exposure.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Exposure, config.RightCamera.Exposure.Value); }

                if (config.RightCamera.Brightness.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Brightness, config.RightCamera.Brightness.Value); }

                if (config.RightCamera.Contrast.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Contrast, config.RightCamera.Contrast.Value); }

                if (config.RightCamera.Saturation.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Gain, config.RightCamera.Saturation.Value); }

                if (config.RightCamera.Sharpness.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Sharpness, config.RightCamera.Sharpness.Value); }

                if (config.RightCamera.Gain.HasValue)
                { _captureRight.SetCaptureProperty(CapProp.Gain, config.RightCamera.Gain.Value); }
            }
            #endregion

            // On each startup, read and store camera properties to a text file for debugging
            CapProp property;
            string fileName = @"Camera_" + DateTime.Now.ToString("yyyyMMddHHmmss") + @".txt";
            using (var dest = File.AppendText(Path.Combine(@".\", fileName)))
            {
                for (int propIndex = 0; propIndex <= 41; propIndex++)
                {
                    property = (CapProp)propIndex;
                    dest.WriteLine($"GetCaptureProperty: [{property}][{propIndex}] [{_captureLeft.GetCaptureProperty(property)}]");
                }
            }

            // get some basic information about a camera frame
            using (Mat frame = new Mat())
            {
                _captureLeft.Read(frame);

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
                System.Drawing.Point fpsLabelPoint = new System.Drawing.Point((int)(.78M * frameWidth), (int)(.91M * frameHeight));
                System.Drawing.Point mpfLabelPoint = new System.Drawing.Point((int)(.78M * frameWidth), (int)(.96M * frameHeight));

                System.Drawing.Point offsetLabelPoint = new System.Drawing.Point((int)(.08M * frameWidth), (int)(.91M * frameHeight));

                System.Drawing.Point hiMidYLabelPoint = new System.Drawing.Point((int)(.08M * frameWidth), (int)(.04M * frameHeight));
                System.Drawing.Point estDistanceLabelPoint = new System.Drawing.Point((int)(.08M * frameWidth), (int)(.09M * frameHeight));

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
                    MpfLabelPoint = mpfLabelPoint,
                    OffsetLabelPoint = offsetLabelPoint,
                    EstDistanceLabelPoint = estDistanceLabelPoint,
                    HiMidYLabelPoint = hiMidYLabelPoint
                };
            }
        }

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
        void ProcessCameraFrameV2(GeneralFrameInfoBE frameInfo, ConfigData2BE configData)
        {
            _frameStopWatch.Restart();

            // all in a using block since they are wrappers around unmanaged memory
            // since they are in a event handler this would be a continuous memory leak
            using (Mat currentLeftFrame = new Mat())
            using (Mat currentRightFrame = new Mat())
            {
                // increment  counter
                _fpsLoopCounter++;
                // Manage FPS averaging, calc over 10 frames
                int elapsedMsec = (int)_fpsStopWatch.ElapsedMilliseconds;
                if (elapsedMsec > 0)
                {
                    _fpsMovingAverage.AddSample((int)(1000.0M / elapsedMsec));
                }
                // reset stopwatch to time to start of next frame
                _fpsStopWatch.Restart();

                Mat mergedFrame = new Mat();

                // Read a BGR frame from the camera(s) and store in "currentFrame"
                _captureLeft.Read(currentLeftFrame);
                if (_captureRight != null)
                {
                    _captureRight.Read(currentRightFrame);
                    CvInvoke.HConcat(currentLeftFrame, currentRightFrame, mergedFrame);
                }
                else
                {
                    mergedFrame = currentLeftFrame;
                }


                // add the FPS label to the frame
                CvInvoke.PutText(mergedFrame, $" FPS: {_fpsMovingAverage.Current}", frameInfo.FpsLabelPoint, FontFace.HersheySimplex, 0.7, FPS_COLOR, 2, LineType.AntiAlias);
                CvInvoke.PutText(mergedFrame, $"msPF: {_frameStopWatch.ElapsedMilliseconds}", frameInfo.MpfLabelPoint, FontFace.HersheySimplex, 0.7, FPS_COLOR, 2, LineType.AntiAlias);

                // store a snapshot (clone) of the frame for the mpeg server
                // we need to do this because that runs async in a different thread
                // and our copy of the frame may be gone when it wants to use it
                OpenCVImageSource.CurrentFrame = mergedFrame.Clone();

                mergedFrame.Dispose();
                mergedFrame = null;
            }
        }

        #region Utilites


        #endregion
    }
}
