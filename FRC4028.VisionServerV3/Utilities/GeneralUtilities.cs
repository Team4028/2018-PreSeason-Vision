using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using FRC4028.VisionServerV3.Entities;


namespace FRC4028.VisionServerV3.Utilities
{
    /// <summary>
    /// This class is a collection of General Purpose Utilities
    /// </summary>
    static class GeneralUtilities
    {
        public static bool IsWithin(this double value, double minimum, double maximum)
        {
            return value >= minimum && value <= maximum;
        }

        /// <summary>
        /// loads the configuration data from an external json config file
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>ConfigData2BE.</returns>
        public static ConfigData2BE GetConfigData(string fileName)
        {
            string text;
            using (var streamReader = new StreamReader($".\\{fileName}", Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }

            ConfigData2BE configData = JsonConvert.DeserializeObject<ConfigData2BE>(text);

            return configData;
        }
    }
}
