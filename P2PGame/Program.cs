using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace P2PGame
{
    struct FrameState
    {
        public int frameNumber;
        public int state;
    }

    class PeerFrameState
    {
        public Queue<int> stateQueue;
    }

    class PeerState
    {
        public int currentState;

        public List<string> otherPeers;
        public Random gameStateGenerator;

        public Queue<int> localStateQueue;
        public Queue<int> localGameEvents;

        public Dictionary<string, PeerFrameState> peerFrameStates;
        public Dictionary<string, Queue<int>> peerGameEvents;
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 

        enum GameEventTypes
        {
            GameState, GameEvent
        }

        static P2PNetClass server;
        static List<P2PNetClass> peers = new List<P2PNetClass>();
        static Dictionary<string, PeerState> peerStates;
        static Random ran = new Random();

        const int NUM_PEERS = 6;

        static int currentFrame = 0;

        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            peerStates = new Dictionary<string, PeerState>();

            server = new P2PNetClass(50000, "server");

            peers.Add(server);

            List<P2PNetClass> clients = new List<P2PNetClass>();
            for (int i = 0; i < NUM_PEERS - 1; ++i)
            {
                P2PNetClass peer = new P2PNetClass(System.Net.IPAddress.Loopback, 50000, "client" + (i + 1));

                peers.Add(peer);
            }

            foreach (P2PNetClass peer in peers)
            {
                peerStates[peer.CurrentUser] = new PeerState();
                peerStates[peer.CurrentUser].peerFrameStates = new Dictionary<string, PeerFrameState>();
                peerStates[peer.CurrentUser].peerGameEvents = new Dictionary<string, Queue<int>>();
                peerStates[peer.CurrentUser].localStateQueue = new Queue<int>();
                peerStates[peer.CurrentUser].localGameEvents = new Queue<int>();
                peerStates[peer.CurrentUser].otherPeers = new List<string>();
                peer.JoinedGame += new JoinedGameHandler(JoinGame);
                peer.PeerConnected += new PeerConnectedHandler(PeerConnected);
                peer.PlayerJoined += new PlayerJoinedHandler(PlayerJoined);
                peer.GameStarted += new GameStartedHandler(GameStarted);
                peer.GameDataArrived += new GameDataHandler(GameDataArrived);
            }

            for (int i = 0; ; ++i)
            {
                foreach (P2PNetClass peer in peers)
                {
                    peer.CheckEvents();
                }

                if (i == 3)
                    server.StartGame(1);
                else if (i > 5)
                {
                    foreach (P2PNetClass peer in peers)
                    {
                        if (ran.Next(0, 100) >= 0)
                        {
                            int gameEvent = ran.Next();
                            peerStates[peer.CurrentUser].localGameEvents.Enqueue(gameEvent);
                            peerStates[peer.CurrentUser].localStateQueue.Enqueue(peerStates[peer.CurrentUser].currentState);
                            peer.SendData(P2PNotices.PeerGameData,
                                CreateGameData(peerStates[peer.CurrentUser].currentState, gameEvent));
                        }
                        else
                        {
                            Console.WriteLine("Skipping sending state for {0}", peer.CurrentUser);
                        }
                    }

                    if (i > 6)
                        CheckGameStates();
                }

                System.Threading.Thread.Sleep(500);
            }
        }

        private static bool CheckGameStates()
        {
            bool shouldExecuteFrame = true;

            foreach (P2PNetClass currentPeer in peers)
            {
                foreach (string otherPeerName in peerStates[currentPeer.CurrentUser].otherPeers)
                {
                    if (peerStates[currentPeer.CurrentUser].peerFrameStates[otherPeerName].stateQueue.Count == 0)
                    {
                        Console.WriteLine("Peer {0} waiting on peer {1}", currentPeer.CurrentUser, otherPeerName);
                        shouldExecuteFrame = false;
                    }
                }
            }

            if (shouldExecuteFrame)
            {
                foreach (P2PNetClass currentPeer in peers)
                {
                    PeerState currentPeerState = peerStates[currentPeer.CurrentUser];
                    int expectedState = currentPeerState.localStateQueue.Dequeue();

                    for (int i = 0; i < currentPeerState.otherPeers.Count; ++i)
                    {
                        string otherPeerName = currentPeerState.otherPeers[i];

                        int peerState = currentPeerState.peerFrameStates[otherPeerName].
                            stateQueue.Dequeue();

                        if (peerState != expectedState)
                        {
                            Console.WriteLine("Peer {0} state differs from peer {1}, dropping",
                                currentPeer.CurrentUser, otherPeerName);
                            currentPeerState.otherPeers.RemoveAt(i);
                            currentPeer.DisconnectPeer(otherPeerName);
                            --i;
                        }
                    }
                }

                Console.WriteLine("Going to frame {0}", ++currentFrame);

                foreach (P2PNetClass currentPeer in peers)
                {
                    PeerState currentPeerState = peerStates[currentPeer.CurrentUser];

                    for (int i = 0; i < currentPeerState.otherPeers.Count; ++i)
                    {
                        string otherPeerName = currentPeerState.otherPeers[i];

                        int peerGameEvent = currentPeerState.peerGameEvents[otherPeerName].Dequeue();
                        currentPeerState.currentState += peerGameEvent;
                    }

                    currentPeerState.currentState += currentPeerState.localGameEvents.Dequeue();
                }
            }

            return shouldExecuteFrame;
        }

        static void GameDataArrived(P2PNetClass netClass, Peer peer, byte[] data)
        {
            ParseGameData(netClass, peer, data);
        }

        static void GameStarted(P2PNetClass netClass, int seed)
        {
            peerStates[netClass.CurrentUser].gameStateGenerator = new Random(seed);
            peerStates[netClass.CurrentUser].currentState = seed;
            Console.WriteLine("{0}: Game started with seed {1}", netClass.CurrentUser, seed);
        }

        static void ParseGameData(P2PNetClass netClass, Peer peer, byte[] data)
        {
            PeerState currentPeerState = peerStates[netClass.CurrentUser];
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(memStream))
                {
                    try
                    {
                        while (true)
                        {
                            GameEventTypes eventType = (GameEventTypes)reader.ReadInt32();

                            switch (eventType)
                            {
                                case GameEventTypes.GameState:
                                    currentPeerState.peerFrameStates[peer.name].
                                        stateQueue.Enqueue(reader.ReadInt32());
                                    break;
                                case GameEventTypes.GameEvent:
                                    currentPeerState.peerGameEvents[peer.name].Enqueue(reader.ReadInt32());
                                    break;
                            }
                        }
                    }
                    catch (System.IO.EndOfStreamException) { }
                }
            }
        }

        static byte[] CreateGameData(int currentState, int gameEvent)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write((int)GameEventTypes.GameState);
                    writer.Write(currentState);
                    writer.Write((int)GameEventTypes.GameEvent);
                    writer.Write(gameEvent);

                    return memStream.ToArray();
                }
            }
        }

        static void PlayerJoined(P2PNetClass netClass, string username)
        {
            peerStates[netClass.CurrentUser].peerGameEvents[username] = new Queue<int>();
            peerStates[netClass.CurrentUser].peerFrameStates[username] = new PeerFrameState();
            peerStates[netClass.CurrentUser].peerFrameStates[username].stateQueue = new Queue<int>();
            peerStates[netClass.CurrentUser].otherPeers.Add(username);
            Console.WriteLine(string.Format("{0}: {1} joined game", netClass.CurrentUser, username));
        }

        static void PeerConnected(P2PNetClass netClass, Peer peer)
        {
            Console.WriteLine(string.Format("{0}: {1} connected", netClass.CurrentUser, peer.name));
        }

        static void JoinGame(P2PNetClass netClass, List<Peer> otherPeers)
        {
            peerStates[netClass.CurrentUser].otherPeers = new List<string>();

            foreach (Peer peer in otherPeers)
            {
                peerStates[netClass.CurrentUser].peerGameEvents[peer.name] = new Queue<int>();
                peerStates[netClass.CurrentUser].peerFrameStates[peer.name] = new PeerFrameState();
                peerStates[netClass.CurrentUser].peerFrameStates[peer.name].stateQueue = new Queue<int>();
                peerStates[netClass.CurrentUser].otherPeers.Add(peer.name);
            }

            Console.WriteLine(string.Format("{0}: joined game with {1}", netClass.CurrentUser,
                string.Join(",", otherPeers.ConvertAll((Peer p) => { return p.name; }).ToArray())));
        }

        static int GetGameStateFromBytes(byte[] data)
        {
            return BitConverter.ToInt32(data, 0);
        }
    }
}
