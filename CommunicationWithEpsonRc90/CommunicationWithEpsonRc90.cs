using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DXHTCP;

namespace CommunicationWithEpsonRc90
{
    public class CommunicationWithEpsonRc90
    {
        string ip = "192.168.100.42";
        int localport = 6000;
        int remoteport = 8234;
        DXHTCPClient TCPClient;
        public CommunicationWithEpsonRc90()
        {
            TCPClient = new DXHTCPClient();
            TCPClient.Received += TCPClient_Received;
            TCPClient.ConnectStateChanged += TCPClient_ConnectedChanged;
            if (TCPClient.ConnectState == "Connected" || TCPClient.ConnectState == "Connecting")
            {
                TCPClient.Close();
            }
            else
            {
                TCPClient.LocalIPPort = localport;
                TCPClient.RemoteIPAddress = ip;
                TCPClient.RemoteIPPort = remoteport;
                TCPClient.NewLine = false;
                try
                {
                    TCPClient.StartTCPConnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("BeginConnect:" + ex.Message);
                }
            }
        }
        private void TCPClient_Received(object sender, string e)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH/mm/ss") + "Received:" + e);
        }
        private void TCPClient_ConnectedChanged(object sender, string e)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH/mm/ss") + "ConnectState:" + e);
        }
        public void Send(string str)
        {
            {
                if (TCPClient != null)
                {
                    string ss = TCPClient.TCPSend(str, false);
                    //Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH/mm/ss") + "SendState:" + ss);
                }
            }
        }
    }
}
