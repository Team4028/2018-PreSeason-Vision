using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SimpleTCP;

namespace FRC4028.SocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            const int PORT = 6060;
            const string SOCKET_SERVER_HOST_ADDR = "10.40.28.78"; // "127.0.0.1";
            const int POLLING_INTERVAL_IN_MSEC = 20;

            try
            {
                System.Console.Write(@"Press any key to start client.");
                System.Console.ReadLine();

                SimpleTcpClient client = new SimpleTcpClient().Connect(SOCKET_SERVER_HOST_ADDR, PORT);
                System.Console.WriteLine($"Connected to server: [{SOCKET_SERVER_HOST_ADDR}] on port: [{PORT}] ");

                int ctr = 0;

                System.Threading.Timer timerThread = new System.Threading.Timer((o) =>
                {
                    PollForImageData(client, ref ctr);
                }, null, 0, POLLING_INTERVAL_IN_MSEC);


                System.Console.Write(@"Press any key to stop client.");
                System.Console.ReadLine();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: [{ex.Message}]");
            }
        }

        static void PollForImageData(SimpleTcpClient client, ref int ctr)
        {
            ctr++;

            System.Console.Write($"Send [{ctr}]> ");

            string requestMsg = $"<get_variables>{ctr}</get_variables>";

            var replyMsg = client.WriteLineAndGetReply(requestMsg, TimeSpan.FromSeconds(10));

            if (replyMsg != null)
            {
                System.Console.WriteLine(replyMsg.MessageString);
            }
            else
            {
                System.Console.WriteLine(@"Null response");
            }
        }
    }
}
