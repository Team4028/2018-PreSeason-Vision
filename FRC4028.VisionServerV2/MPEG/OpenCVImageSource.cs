using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

namespace FRC4028.VisionServerV2.MPEG
{
    /// <summary>
    /// This class is an OpenCV Image Source for the MPEG Server
    /// </summary>
    static class OpenCVImageSource
    {
        static Mat _currentFrame;
        static ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim();

        /// <summary>
        /// Returns a series of of Snapshots with the specified width and height.
        /// </summary>
        /// <param name="mpegWidth">Width of the MPEG.</param>
        /// <param name="mpegHeight">Height of the MPEG.</param>
        /// <returns>IEnumerable&lt;Image&gt;.</returns>
        public static IEnumerable<Image> Snapshots(int mpegWidth, int mpegHeight)
        {
            while (true)
            {
                using (Mat currentFrame = CurrentFrame.Clone())
                {
                    using (Image<Bgr, byte> origImage = new Image<Bgr, Byte>(currentFrame.Bitmap))
                    {
                        Image<Bgr, byte> workingImage = origImage;
                        {
                            if (mpegWidth != currentFrame.Width || mpegHeight != currentFrame.Height)
                            {
                                workingImage = origImage.Resize(mpegWidth, mpegHeight, Inter.Linear);//this is image with resize
                            }

                            using (Image finalImage = new Bitmap(workingImage.Bitmap))
                            {
                                workingImage.Dispose();

                                yield return finalImage;
                            }
                        }
                    }
                }
            }

            yield break;
        }

        public static Mat CurrentFrame
        {
            set
            {
                _rwl.EnterWriteLock();
                try
                {
                    _currentFrame = value;
                }
                finally
                {
                    // Ensure that the lock is released.
                    _rwl.ExitWriteLock();
                }
            }
            private get
            {
                _rwl.EnterReadLock();
                try
                {
                    return _currentFrame;
                }
                finally
                {
                    // Ensure that the lock is released.
                    _rwl.ExitReadLock();
                }
            }
        }

    }
}
