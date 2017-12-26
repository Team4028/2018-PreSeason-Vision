using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BlinkStickDotNet;

using FRC4028.VisionServerV2.Entities;

namespace FRC4028.VisionServerV2.StatusLED
{
    /*
     * This class is a wrapper around interfacing to a BlickStick device (LED attached to USB port)
     *
     * Target
     *  w/i Center DeadBand     Green
     *  In FOV                  Blue
     *  Not in FOV              Red
     *  
     * Framerate
     *  w/i 5% of target
     *  too slow
     *  
     * https://arvydas.github.io/BlinkStickDotNet/namespace_blink_stick_dot_net.html
     * 
     * BlinkStick Options
     *   SetColor/TurnOff
     *   Blink                
     *   Morph                Transition from current color to new color
     *   Pulse    
     */
    class BlinkStickController
    {
        // global working values
        BlinkStick _blinkStick;
        USBBlinkStickBE _configData;
        int _targetFPS;

        // enums and constants
        enum BLINKSTICK_MODE
        {
            // Control regular LEDs (common cathode). This is the default for BlinkStick Pro.
            Normal = 0,
            // Control regular LEDs in inverse mode (common anode), for example IKEA DIODER
            Inverse = 1,
            // Control individually addressable LEDs, for example any WS2812, Adafruit NeoPixels and smart pixels.
            WS2812 = 2
        }

        // http://www.cloford.com/resources/colours/500col.htm
        RgbColor Green = RgbColor.FromRgb(0, 201, 87);      // w/i Center DeadBand 
        RgbColor Blue = RgbColor.FromRgb(30, 144, 255);     // In FOV
        RgbColor Red = RgbColor.FromRgb(255, 131, 250);     // Not in FOV  

        const decimal FPS_DEADBAND = 10.0M; // 10% error   20 FPS => 18 FPS

        /// <summary>
        /// Initializes a new instance of the <see cref="BlinkStickController"/> class.
        /// </summary>
        public BlinkStickController(USBBlinkStickBE configData, int targetFPS)
        {
            _configData = configData;
            _targetFPS = targetFPS;

            _blinkStick = BlinkStick.FindFirst();

            if (_blinkStick != null && _blinkStick.OpenDevice())
            {

                _blinkStick.SetMode((int)BLINKSTICK_MODE.Normal);

                // if we find a blinkstick, force it off initially
                _blinkStick.TurnOff();
            }
        }

        /// <summary>
        /// Toggles the led.
        /// </summary>
        /// <param name="targetInfo">The target information.</param>
        public void ToggleLED(TargetInfoBE targetInfo)
        {
            if (this.IsAvailable)
            {
                // determine if we should single or double flash the LED
                int repeatCount = 1;

                // calc error % (10% = 10)
                decimal fpsErrorPercent = Math.Abs((decimal)((_targetFPS - targetInfo.FramesPerSec) / _targetFPS) * 100);

                if (fpsErrorPercent > FPS_DEADBAND)
                {
                    repeatCount = 2;
                }

                // decide what color to use
                RgbColor colorToUse = null;

                if(!targetInfo.IsTargetInFOV)
                {
                    colorToUse = Red;
                }
                else if (targetInfo.Delta_X <= _configData.OnTargetThreshold)
                {
                    colorToUse = Green;
                }
                else
                {
                    colorToUse = Blue;
                }

                // blink the LED
                _blinkStick.Blink(colorToUse, repeatCount, 250);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is available.
        /// </summary>
        /// <value><c>true</c> if this instance is available; otherwise, <c>false</c>.</value>
        public bool IsAvailable
        {
            get { return (_blinkStick != null && _blinkStick.Connected); }
        }
    }
}
