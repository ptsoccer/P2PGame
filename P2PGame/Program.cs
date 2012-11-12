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
        /// 

        static P2PNetClass server;
        static List<P2PNetClass> clients = new List<P2PNetClass>();

        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());

            server = new P2PNetClass(50000, "server");
            server.JoinedGame += new JoinedGameHandler(JoinGame);
            server.PeerConnected += new PeerConnectedHandler(PeerConnected);
            server.PlayerJoined += new PlayerJoinedHandler(PlayerJoined);
            server.GameStarted += new GameStartedHandler(GameStarted);

            List<P2PNetClass> clients = new List<P2PNetClass>();
            for (int i = 0; i < 5; ++i)
            {
                clients.Add(new P2PNetClass(System.Net.IPAddress.Loopback, 50000, "client" + (i + 1)));
                clients[i].JoinedGame += new JoinedGameHandler(JoinGame);
                clients[i].PeerConnected += new PeerConnectedHandler(PeerConnected);
                clients[i].PlayerJoined += new PlayerJoinedHandler(PlayerJoined);
                clients[i].GameStarted += new GameStartedHandler(GameStarted);
            }

            for (int i = 0;; ++i)
            {
                
                server.CheckEvents();

                foreach (P2PNetClass client in clients)
                {
                    client.CheckEvents();
                }

                if (i == 3)
                    server.StartGame(1);
                else if (i > 5)
                {

                }

                System.Threading.Thread.Sleep(1);
            }
        }

        static void GameStarted(P2PNetClass netClass, int seed)
        {
            Console.WriteLine(string.Format("{0}: Game started with seed {1}", netClass.CurrentUser, seed));
        }

        static void PlayerJoined(P2PNetClass netClass, string username)
        {
            Console.WriteLine(string.Format("{0}: {1} joined game", netClass.CurrentUser, username));
        }

        static void PeerConnected(P2PNetClass netClass, Peer peer)
        {
            Console.WriteLine(string.Format("{0}: {1} connected", netClass.CurrentUser, peer.name));
        }

        static void JoinGame(P2PNetClass netClass, List<Peer> otherPeers)
        {
            Console.WriteLine(string.Format("{0}: joined game with {1}", netClass.CurrentUser,
                string.Join(",", otherPeers.ConvertAll((Peer p) => { return p.name; }).ToArray())));
        }
    }
}
