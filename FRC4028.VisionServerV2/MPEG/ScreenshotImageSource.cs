using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRC4028.VisionServerV2.MPEG
{
    /// <summary>
    /// This class is an ScreenShot Image Source useful for testing the MPEG Server
    /// </summary>
    /// <remarks>
    /// You can only use this when the program is running interactively (ie. Not as a WinSvc!)
    /// </remarks>
    static class ScreenshotImageSource
    {
        /// <summary>
        /// Returns a 
        /// </summary>
        /// <param name="delayTime"></param>
        /// <returns></returns>
        public static IEnumerable<Image> Snapshots(int width, int height)
        {
            Size size = new Size(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);

            Bitmap srcImage = new Bitmap(size.Width, size.Height);
            Graphics srcGraphics = Graphics.FromImage(srcImage);

            bool scaled = (width != size.Width || height != size.Height);

            Bitmap dstImage = srcImage;
            Graphics dstGraphics = srcGraphics;

            if (scaled)
            {
                dstImage = new Bitmap(width, height);
                dstGraphics = Graphics.FromImage(dstImage);
            }

            Rectangle src = new Rectangle(0, 0, size.Width, size.Height);
            Rectangle dst = new Rectangle(0, 0, width, height);
            Size curSize = new Size(32, 32);

            while (true)
            {
                srcGraphics.CopyFromScreen(0, 0, 0, 0, size);

                if (scaled)
                    dstGraphics.DrawImage(srcImage, dst, src, GraphicsUnit.Pixel);

                yield return dstImage;

            }

            srcGraphics.Dispose();
            dstGraphics.Dispose();

            srcImage.Dispose();
            dstImage.Dispose();

            yield break;
        }
    }
}
