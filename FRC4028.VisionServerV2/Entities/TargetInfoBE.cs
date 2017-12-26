using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRC4028.VisionServerV2.Entities
{
    /// <summary>
    /// This Business Entity holds information about the target seen in a frame
    /// </summary>
    class TargetInfoBE
    {
        public bool IsTargetInFOV { get; set; }

        public int SW_X { get; set; }
        public int SW_Y { get; set; }
        public int SE_X { get; set; }
        public int SE_Y { get; set; }
        public int HighMiddleY { get; set; }


        public int Delta_X { get; set; }
        public int Delta_Y { get; set; }

        public int Estimated_Distance_Inches { get; set; }

        public int FramesPerSec { get; set; }

        public long FrameCounter { get; set; }

        public long PerFrameMsec { get; set; }

        public int BatteryChargeLevel { get; set; }

        public int CpuUsage { get; set; }
    }
}
