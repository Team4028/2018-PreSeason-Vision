using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRC4028.VisionServerV2.Entities
{
    /// <summary>
    /// This class represents am image in the frame w/ an Area property so it is easy to sort
    /// </summary>
    class FoundObjectBE
    {
        public FoundObjectBE(VectorOfPoint contour, Rectangle boundingRectangle, double area)
        {
            Contour = contour;
            Area = area;
            BoundingRectangle = boundingRectangle;
        }

        public VectorOfPoint Contour { get; set; }
        public double Area { get; set; }

        public Rectangle BoundingRectangle { get; set; }
    }

}
