using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRC4028.VisionServerV2.Entities
{
    /// <summary>
    /// This Business Entity holds general information about all frames
    /// </summary>
    class GeneralFrameInfoBE
    {
        public int Height { get; set; }
        public int Width { get; set; }

        public Point CenterPoint { get; set; }
        public Point CenterPointHorizLeft { get; set; }
        public Point CenterPointHorizRight { get; set; }
        public Point CenterPointVertTop { get; set; }
        public Point CenterPointVertBottom { get; set; }
        public Point FpsLabelPoint { get; set; }
        public Point MpfLabelPoint { get; set; }
        public Point OffsetLabelPoint { get; set; }
        public Point HiMidYLabelPoint { get; set; }

        public Point EstDistanceLabelPoint { get; set; }
    }
}
