using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BlinkStickDotNet;

using FRC4028.VisionServerV3.Entities;

namespace FRC4028.VisionServerV3.StatusLED
{
    /*
     * This class is a wrapper around interfacing to a BlinkStick device (LED attached to USB port)
     *
     * Target
     *  w/i Center DeadBand     Green
     *  In FOV                  Blue
     *  Not in FOV              Red
     *  
     * Framerate
     *  w/i 25% of target
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
        
        // define 8x3 arrays for each color so we can turn all 8 LEDs on
        static byte[] LEDS_RAINBOW = new byte[3 * 8]
               {0, 0, 255,    //GRB for led0
			    0, 128, 0,    //GRB for led1
			    128, 0, 0,    //...
			    128, 255, 0,
                0, 255, 128,
                128, 0, 128,
                0, 128, 255,
                128, 0, 0    //GRB for led7
                };

        static byte[] LEDS_RED = new byte[3 * 8]
               {0, 255, 0,    //GRB for led0
				0, 255, 0,    //GRB for led1
				0, 255, 0,    //...
				0, 255, 0,
                0, 255, 0,
                0, 255, 0,
                0, 255, 0,
                0, 255, 0    //GRB for led7
               };

        static byte[] LEDS_BLUE = new byte[3 * 8]
               {0, 0, 255,    //GRB for led0
				0, 0, 255,    //GRB for led1
				0, 0, 255,    //...
				0, 0, 255,
                0, 0, 255,
                0, 0, 255,
                0, 0, 255,
                0, 0, 255    //GRB for led7
                };

        static byte[] LEDS_GREEN = new byte[3 * 8]
               {255, 0, 0,    //GRB for led0
				255, 0, 0,    //GRB for led1
				255, 0, 0,    //...
				255, 0, 0,
                255, 0, 0,
                255, 0, 0,
                255, 0, 0,
                255, 0, 0    //GRB for led7
               };

        static byte[] LEDS_OFF = new byte[3 * 8]
               {0, 0, 0,    //GRB for led0
				0, 0, 0,    //GRB for led1
				0, 0, 0,    //...
				0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0    //GRB for led7
               };

        const decimal FPS_DEADBAND = 25.0M; // 25% error   25 FPS => 20 FPS

        /// <summary>
        /// Initializes a new instance of the <see cref="BlinkStickController"/> class.
        /// </summary>
        public BlinkStickController(USBBlinkStickBE configData)
        {
            _configData = configData;

            _blinkStick = BlinkStick.FindFirst();

            if (_blinkStick != null && _blinkStick.OpenDevice())
            {

                _blinkStick.SetMode((int)BLINKSTICK_MODE.WS2812);

                // if we find a blinkstick, force it off initially
                _blinkStick.TurnOff();
            }
        }

        /// <summary>
        /// Toggles the led.
        /// </summary>
        /// <param name="targetInfo">The target information.</param>
        public void ToggleLED()
        {
            if (this.IsAvailable)
            {
                try
                {
                    // determine if we should single or double flash the LED
                    int repeatCount = 1;

                    // calc error % (10% = 10)
                    //decimal fpsErrorPercent = Math.Abs((decimal)((_targetFPS - targetInfo.FramesPerSec) / _targetFPS) * 100);

                    // target is centered w/i the threshold
                    _blinkStick.SetColors(0, LEDS_GREEN);
                    _blinkStick.WaitThread(500);
                    _blinkStick.SetColors(0, LEDS_OFF);
                }
                catch
                {
                    // swallow exception likely caused if blinkstick is unplugged
                    _blinkStick = null;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is available.
        /// </summary>
        /// <value><c>true</c> if this instance is available; otherwise, <c>false</c>.</value>
        public bool IsAvailable
        {
            get
            {
                return (_blinkStick != null && _blinkStick.Connected);
            }
        }

        /// <summary>
        /// Cleans up.
        /// </summary>
        public void CleanUp()
        {
            if(IsAvailable)
            {
                _blinkStick.TurnOff();
                _blinkStick.Dispose();
            }
        }
    }
}
