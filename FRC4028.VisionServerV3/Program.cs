using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Topshelf;

namespace FRC4028.VisionServerV3
{
    /// <summary>
    /// Windows Service Host for the Vision Server
    /// </summary>
    /// <remarks>
    /// Install using the cmd line:  FRC4028.VisionServerV3.exe install 
    /// </remarks>
    class Program
    {
        static void Main(string[] args)
        {
            // set up the host using the HostFactory.Run the runner.
            HostFactory.Run(x =>
            {
                x.Service<VisionServer>(s =>
                {
                    s.ConstructUsing(name => new VisionServer());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("FRC Team 4028 Vision Data Server V3");
                x.SetDisplayName("BeakSquad Vision V2");
                x.SetServiceName("4028VisionServerV2");
                x.StartAutomatically();     // Start the service automatically
            });
        }
    }
}
