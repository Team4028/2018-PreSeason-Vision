using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace FRC4028.VisionServerV3.Entitiesxx
{

    /// <summary>
    /// These classes represents the deserialized form of the json config file
    /// </summary>
    public class ConfigDataBE
    {
        [JsonProperty(@"target_camera")]
        public TargetCameraBE TargetCamera { get; set; }

        [JsonProperty(@"mpeg_server")]
        public MPEGServerBE MPEGServer { get; set; }

        [JsonProperty(@"usb_blink_stick")]
        public USBBlinkStickBE USBBlinkStick { get; set; }
    }

    public class TargetCameraBE
    {
        [JsonProperty(@"usb_camera_left")]
        public int USBCameraLeft { get; set; }

        [JsonProperty(@"usb_camera_right")]
        public int? USBCameraRight { get; set; }

        [JsonProperty(@"frame_width")]
        public int HorizontalResolution { get; set; }

        [JsonProperty(@"frame_height")]
        public int VerticalResolution { get; set; }

        [JsonProperty(@"brightness")]
        public int? Brightness { get; set; }

        [JsonProperty(@"contrast")]
        public int? Contrast { get; set; }

        [JsonProperty(@"sharpness")]
        public int? Sharpness { get; set; }

        [JsonProperty(@"saturation")]
        public int? Saturation { get; set; }

        [JsonProperty(@"exposure")]
        public int? Exposure { get; set; }

        [JsonProperty(@"gain")]
        public int? Gain { get; set; }

        [JsonProperty(@"target_fps")]
        public int TargetFPS { get; set; }
    }

    public class MPEGServerBE
    {
        [JsonProperty(@"is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty(@"tcp_port")]
        public int TCPPort { get; set; }

        [JsonProperty(@"image_width")]
        public int ImageWidth { get; set; }

        [JsonProperty(@"image_height")]
        public int ImageHeight { get; set; }
    }

    public class USBBlinkStickBE
    {
        [JsonProperty(@"is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty(@"on_target_threshold")]
        public int OnTargetThreshold { get; set; }

        [JsonProperty(@"heartbeat_interval_msec")]
        public int HeartbeatIntervalMsec { get; set; }
    }
}
