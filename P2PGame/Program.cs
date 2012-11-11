using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace P2PGame
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());

            P2PNetClass server = new P2PNetClass(50000, "server");
            P2PNetClass client = new P2PNetClass(System.Net.IPAddress.Parse("192.168.1.104"), 50000, "client");
            server.CheckEvents();

            P2PNetClass client2 = new P2PNetClass(System.Net.IPAddress.Parse("192.168.1.104"), 50000, "client2");
            server.CheckEvents();
            
            client2.CheckEvents();
        }
    }
}
