using SharedLibrary;
using System;
using System.ServiceModel;

namespace ChatServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding tcpBinding = new NetTcpBinding();

            ServiceHost serviceHost = new ServiceHost(typeof(ChatService));

            serviceHost.AddServiceEndpoint(
                typeof(IChannelService),
                tcpBinding,
                "net.tcp://localhost:8100/DataService"
            );

            serviceHost.AddServiceEndpoint(
                typeof(IDuplexChatService),
                tcpBinding,
                "net.tcp://localhost:8200/DuplexService"
             );

            serviceHost.Open();
            Console.WriteLine("========================================");
            Console.WriteLine("          CHAT SERVER RUNNING");
            Console.WriteLine("========================================");
            Console.WriteLine("Polling Service : net.tcp://localhost:8100/DataService");
            Console.WriteLine("Duplex Service  : net.tcp://localhost:8200/DuplexService");
            Console.WriteLine("========================================");
            Console.WriteLine("Press ENTER to stop the server.");
            Console.ReadLine();

            serviceHost.Close();
        }
    }
}