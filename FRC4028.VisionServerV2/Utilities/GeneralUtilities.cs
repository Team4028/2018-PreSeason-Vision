using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using FRC4028.VisionServerV2.Entities;


namespace FRC4028.VisionServerV2.Utilities
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
        /// Gets the computer perf information.
        /// </summary>
        /// <returns>System.Int32.</returns>
        public static (int batteryChargeLevel, int currentCPUUsage) GetComputerPerfInfo(System.Management.ManagementClass wmi, PerformanceCounter cpuPerfCtr)
        {
            // get battery info
            var allBatteries = wmi.GetInstances();
            int batteryChargeLevel = 0;

            if (allBatteries.Count != 1)
            {
                batteryChargeLevel = allBatteries.Count;
            }
            else
            {
                foreach (var battery in allBatteries)
                {
                    batteryChargeLevel = Int32.Parse(Convert.ToString(battery["EstimatedChargeRemaining"]));
                }
            }

            // get cpu usage info
            int cpuUsage = 0;
            cpuUsage = (int)cpuPerfCtr.NextValue();

            return (batteryChargeLevel, cpuUsage);
        }

        /// <summary>
        /// loads the configuration data from an external json config file
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>ConfigDataBE.</returns>
        public static ConfigDataBE GetConfigData(string fileName)
        {
            string text;
            using (var streamReader = new StreamReader($".\\{fileName}", Encoding.UTF8))
            {
                text = streamReader.ReadToEnd();
            }

            ConfigDataBE configData = JsonConvert.DeserializeObject<ConfigDataBE>(text);

            return configData;
        }

        /// <summary>
        /// See if the target IPv4 Address is reachable via a TCP/IP Ping
        /// </summary>
        /// <param name="ipv4Addr">The ipv4 addr.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentException">... Ping Test, you must supply a non-blank IPv4 address!</exception>
        public static bool PingTest(string ipv4Addr)
        {
            if (string.IsNullOrEmpty(ipv4Addr))
            {
                throw new ArgumentException("... Ping Test, you must supply a non-blank IPv4 address!");
            }

            Ping pingSender = new Ping();

            PingReply reply = pingSender.Send(ipv4Addr);
            if (reply.Status == IPStatus.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
