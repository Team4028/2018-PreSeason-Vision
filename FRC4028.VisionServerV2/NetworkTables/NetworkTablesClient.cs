using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetworkTables;

using FRC4028.VisionServerV2.Entities;
using FRC4028.VisionServerV2.Utilities;

namespace FRC4028.VisionServerV2.NetworkTables
{
    /// <summary>
    /// This class is a wrapper around talking to a (FRC Robot) NetworkTables Server
    /// </summary>
    class NetworkTablesClient
    {
        // constants
        const int DEFAULT_PORT = 1735;
        const int RECONNECT_INTERVAL_IN_SEC = 10;

        // class level working variables
        System.Threading.Timer _connectThread;
        bool _isConnected = false;

        NetworkTablesClientBE _config;
        NetworkTable _visionDataTable;

        internal NetworkTablesClient(NetworkTablesClientBE config)
        {
            // store a copy of the config
            _config = config;

            _config.TCPPort = _config.TCPPort.HasValue ? config.TCPPort.Value : DEFAULT_PORT;

           // setup a reoccuring timer to reconnect to the roborio if it disconnects
           _connectThread = new System.Threading.Timer((o) =>
            {
                ConnectToRoboRIO(_config);
            }, null, 0, (int)(1000 * RECONNECT_INTERVAL_IN_SEC));
        }

        private void ConnectToRoboRIO(NetworkTablesClientBE config)
        {
            try
            {
                if (_visionDataTable == null || !_visionDataTable.IsConnected)
                {
                    // try to ping the networkTables server node
                    if (GeneralUtilities.PingTest(_config.ServerIPv4Address))
                    {
                        // set the address of the Server Node (ie this is the RoboRIO)
                        NetworkTable.SetIPAddress(_config.ServerIPv4Address);

                        // set the port we are going to use
                        NetworkTable.SetPort(_config.TCPPort.Value);

                        // Set Client Mode (roborio is the server node)
                        NetworkTable.SetClientMode();

                        // Initialize the client
                        NetworkTable.Initialize();

                        // try and connect to the specific table
                        _visionDataTable = NetworkTable.GetTable(_config.TableName);

                        // need to pause to let infrastructure settle
                        System.Threading.Thread.Sleep(5000);

                        // did we connect ok?
                        _isConnected = _visionDataTable.IsConnected;
                    }
                    else
                    {
                        _isConnected = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
            }
        }

        /// <summary>
        /// Publishes the specified target information to the configured network table.
        /// </summary>
        /// <param name="targetInfo">The target information.</param>
        internal void Publish(TargetInfoBE targetInfo)
        {
            if(_isConnected)
            {
                try
                {
                    _visionDataTable.PutBoolean("IsTargetInFOV", targetInfo.IsTargetInFOV);
                    //_visionDataTable.PutNumber("SouthWestX", targetInfo.SW_X);
                    //_visionDataTable.PutNumber("SouthWestY", targetInfo.SW_Y);
                    //_visionDataTable.PutNumber("SouthEastX", targetInfo.SE_X);
                    //_visionDataTable.PutNumber("SouthEastY", targetInfo.SE_Y);
                    //_visionDataTable.PutNumber("HiMidY", targetInfo.HighMidY);
                    _visionDataTable.PutNumber("DeltaX", targetInfo.Delta_X);
                    _visionDataTable.PutNumber("DeltaY", targetInfo.Delta_Y);
                    _visionDataTable.PutNumber("EstDistInches", targetInfo.Estimated_Distance_Inches);
                    _visionDataTable.PutNumber("FPS", targetInfo.FramesPerSec);
                    _visionDataTable.PutNumber("FrameCtr", targetInfo.FrameCounter);
                    _visionDataTable.PutNumber("FrameMSec", targetInfo.PerFrameMsec);
                    _visionDataTable.PutNumber("CPU", targetInfo.CpuUsage);
                    //_visionDataTable.PutNumber("Battery", targetInfo.BatteryChargeLevel);

                    /*
                        + "<IS_VALID>" + _targetInfo.IsTargetInFOV + "</IS_VALID>"
                        + "<SW_X>" + _targetInfo.SW_X + "</SW_X>"
                        + "<SW_Y>" + _targetInfo.SW_Y + "</SW_Y>"
                        + "<SE_X>" + _targetInfo.SE_X + "</SE_X>"
                        + "<SE_Y>" + _targetInfo.SE_Y + "</SE_Y>"
                        + "<HI_MID_Y>" + _targetInfo.HighMidY + "</HI_MID_Y>"
                        + "<FPS>" + _targetInfo.FramesPerSec + "</FPS>"
                        + "<FRAMECTR>" + _targetInfo.FrameCounter + "</FRAMECTR>"
                        + "<FRAMEMS>" + _targetInfo.PerFrameMsec + "</FRAMEMS>"
                        + "<CPU>" + _targetInfo.CpuUsage + "</CPU>"
                        + "<BATTERY>" + _targetInfo.BatteryChargeLevel + "</BATTERY>"
                     */
                }
                catch(Exception ex)
                {
                    _visionDataTable = null;
                    _isConnected = false;
                }
            }
        }
    }
}
