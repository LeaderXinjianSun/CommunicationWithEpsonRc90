using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationWithEpsonRc90
{
    class Program
    {
        static void Main(string[] args)
        {
            CommunicationWithEpsonRc90 communicationWithEpsonRc90 = new CommunicationWithEpsonRc90();
            while (true)
            {
                //string ss = Console.ReadLine();
                System.Threading.Thread.Sleep(100);
                communicationWithEpsonRc90.Send(DateTime.Now.ToString("yyyy/MM/dd HH:/mm/ss " + "\r\n"));
            }
            
        }
    }
}
