using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace P2PGame
{
    enum GameEventType
    {
        GameState, GameEvent
    }

    struct FrameState
    {
        public int frameNumber;
        public int state;
    }

    struct GameEvent
    {
        public GameEventType mType;
        public object mEventData;

        public GameEvent(GameEventType type, object eventData)
        {
            mType = type;
            mEventData = eventData;
        }
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

        // Events queued to be sent to other peers (will be sent on network tick)
        public Queue<GameEvent> queuedEvents;

        public Dictionary<string, PeerFrameState> peerFrameStates;
        public Dictionary<string, Queue<int>> peerGameEvents;
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 

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
                PeerState peerState = peerStates[peer.CurrentUser] = new PeerState();
                peerState.peerFrameStates = new Dictionary<string, PeerFrameState>();
                peerState.peerGameEvents = new Dictionary<string, Queue<int>>();
                peerState.localStateQueue = new Queue<int>();
                peerState.localGameEvents = new Queue<int>();
                peerState.queuedEvents = new Queue<GameEvent>();
                peerState.otherPeers = new List<string>();
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
                    if (i > 6)
                        CheckGameStates();

                    foreach (P2PNetClass peer in peers)
                    {
                        PeerState peerState = peerStates[peer.CurrentUser];
                        if (i > 6)
                        {
                            //ModifyGameStates(peers);
                            int gameEvent = ran.Next();
                            peerState.queuedEvents.Enqueue(new GameEvent(GameEventType.GameEvent, gameEvent));
                            peerState.localGameEvents.Enqueue(gameEvent);
                        }

                        peerState.localStateQueue.Enqueue(peerState.currentState);
                        peer.SendData(P2PNotices.PeerGameData, CreateGameData(peerState));
                    }
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
                        var otherPeerQueue = currentPeerState.peerGameEvents[otherPeerName];

                        while (otherPeerQueue.Count > 0)
                        {
                            int peerGameEvent = currentPeerState.peerGameEvents[otherPeerName].Dequeue();
                            currentPeerState.currentState += peerGameEvent;
                        }
                    }

                    while (currentPeerState.localGameEvents.Count > 0)
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

        private static void ModifyGameStates(List<P2PNetClass> peers)
        {
            
        }

        static void ParseGameData(P2PNetClass netClass, Peer peer, byte[] data)
        {
            PeerState currentPeerState = peerStates[netClass.CurrentUser];
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(memStream))
                {
                    List<GameEvent> events = new List<GameEvent>();

                    try
                    {
                        while (true)
                        {
                            GameEventType eventType = (GameEventType)reader.ReadInt32();

                            switch (eventType)
                            {
                                case GameEventType.GameState:
                                    int state = reader.ReadInt32();
                                    currentPeerState.peerFrameStates[peer.name].
                                        stateQueue.Enqueue(state);
                                    events.Add(new GameEvent(eventType, state));
                                    break;
                                case GameEventType.GameEvent:
                                    int gameEvent = reader.ReadInt32();
                                    currentPeerState.peerGameEvents[peer.name].Enqueue(gameEvent);
                                    events.Add(new GameEvent(eventType, gameEvent));
                                    break;
                            }
                        }
                    }
                    catch (System.IO.EndOfStreamException) { }
                }
            }
        }

        static byte[] CreateGameData(PeerState peerState)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write((int)GameEventType.GameState);
                    writer.Write(peerState.currentState);

                    if (peerState.queuedEvents.Count > 0)
                    {
                        writer.Write((int)GameEventType.GameEvent);
                        writer.Write((int)peerState.queuedEvents.Dequeue().mEventData);
                    }

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
