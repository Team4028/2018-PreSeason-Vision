using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace FRC4028.VisionServerV2.Entities
{

    /// <summary>
    /// These classes represents the deserialized form of the json config file
    /// </summary>
    public class ConfigDataBE
    {
        [JsonProperty(@"target_camera")]
        public TargetCameraBE TargetCamera { get; set; }

        [JsonProperty(@"target_color_bounds")]
        public TargetColorBoundsBE TargetColorBounds { get; set; }

        [JsonProperty(@"target_ratios")]
        public TargetRatiosBE TargetRatios { get; set; }

        [JsonProperty(@"vision_data_server")]
        public VisionDataServerBE VisionDataServer { get; set; }

        [JsonProperty(@"network_tables_client")]
        public NetworkTablesClientBE NetworkTablesClient { get; set; }

        [JsonProperty(@"dist_est_polynominal_coefficients")]
        public DistanceEstPolynomialCoefficientsBE DistanceEstPolynomialCoefficients { get; set; }

        [JsonProperty(@"mpeg_server")]
        public MPEGServerBE MPEGServer { get; set; }

        [JsonProperty(@"usb_blink_stick")]
        public USBBlinkStickBE USBBlinkStick { get; set; }
    }

    public class TargetCameraBE
    {
        [JsonProperty(@"usb_camera")]
        public int USBCamera { get; set; }

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

    public class TargetColorBoundsBE
    {
        [JsonProperty(@"lower")]
        public HSVColorBE LowerBound { get; set; }

        [JsonProperty(@"upper")]
        public HSVColorBE UpperBound { get; set; }
    }

    public class HSVColorBE
    {
        [JsonProperty(@"h")]
        public int Hue { get; set; }
        [JsonProperty(@"s")]
        public int Saturation { get; set; }
        [JsonProperty(@"v")]
        public int Value { get; set; }
    }

    public class TargetRatiosBE
    {
        [JsonProperty(@"h2wRatio_min")]
        public decimal HeightToWidthRatioMin { get; set; }

        [JsonProperty(@"h2wRatio_max")]
        public decimal HeightToWidthRatioMax { get; set; }

        [JsonProperty(@"areaRatio_min")]
        public decimal AreaRatioMin { get; set; }


        [JsonProperty(@"areaRatio_max")]
        public decimal AreaRatioMax { get; set; }
    }

    public class VisionDataServerBE
    {
        [JsonProperty(@"is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty(@"tcp_port")]
        public int TCPPort { get; set; }

        [JsonProperty(@"msg_format")]
        public string MessageFormat { get; set; }
    }

    public class NetworkTablesClientBE
    {
        [JsonProperty(@"is_enabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty(@"server_ip_addr")]
        public string ServerIPv4Address { get; set; }

        [JsonProperty(@"tcp_port")]
        public int? TCPPort { get; set; }

        [JsonProperty(@"table_name")]
        public string TableName { get; set; }
    }

    public class DistanceEstPolynomialCoefficientsBE
    {
        [JsonProperty(@"a3")]
        public decimal A3 { get; set; }

        [JsonProperty(@"a2")]
        public decimal A2 { get; set; }

        [JsonProperty(@"a1")]
        public decimal A1 { get; set; }

        [JsonProperty(@"a0")]
        public decimal A0 { get; set; }
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
