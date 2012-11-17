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
        public Queue<List<GameEvent>> frameEvents;
    }

    class PeerState
    {
        public int currentState;

        public List<string> otherPeers;
        public Random gameStateGenerator;

        // Events queued to be sent to other peers (will be sent on network tick)
        public List<GameEvent> localPendingEvents;

        public Queue<List<GameEvent>> localFrameEvents;
        public Queue<int> localStates;

        public Dictionary<string, PeerFrameState> peerFrameStates;
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
        static Random ran = new Random(1);

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
                peerState.localFrameEvents = new Queue<List<GameEvent>>();
                peerState.localStates = new Queue<int>();
                peerState.localPendingEvents = new List<GameEvent>();
                peerState.peerFrameStates = new Dictionary<string, PeerFrameState>();
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

                        peerState.localStates.Enqueue(peerState.currentState);
                        peerState.localPendingEvents.Add(new GameEvent(GameEventType.GameState, peerState.currentState));

                        if (i > 6)
                        {
                            int max = ran.Next(0, 5);
                            for (int j = 0; j < max; ++j)
                            {
                                peerState.localPendingEvents.Add(new GameEvent(GameEventType.GameEvent, ran.Next()));
                            }
                        }

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
                    if (peerStates[currentPeer.CurrentUser].peerFrameStates[otherPeerName].frameEvents.Count == 0)
                    {
                        Console.WriteLine("Peer {0} waiting on peer {1}", currentPeer.CurrentUser, otherPeerName);
                        shouldExecuteFrame = false;
                    }
                }
            }

            if (shouldExecuteFrame)
            {
                Console.WriteLine("Going to frame {0}", ++currentFrame);

                // Assume peers sent their state, nothing bad happens if they don't though
                foreach (P2PNetClass currentPeer in peers)
                {
                    PeerState currentPeerState = peerStates[currentPeer.CurrentUser];
                    int expectedState = currentPeerState.localStates.Dequeue();

                    foreach (GameEvent gameEvent in currentPeerState.localFrameEvents.Dequeue())
                    {

                        switch (gameEvent.mType)
                        {
                            case GameEventType.GameState:
                                int peerState = (int)gameEvent.mEventData;
                                if (peerState != expectedState)
                                {
                                    Console.WriteLine("Peer {0}'s expected state isn't the same as its local state",
                                        currentPeer.CurrentUser);
                                }

                                break;
                            case GameEventType.GameEvent:
                                int peerGameEvent = (int)gameEvent.mEventData;
                                currentPeerState.currentState += peerGameEvent;

                                break;
                        }
                    }

                    for (int i = 0; i < currentPeerState.otherPeers.Count; ++i)
                    {
                        string otherPeerName = currentPeerState.otherPeers[i];
                        var otherPeerQueue = currentPeerState.peerFrameStates[otherPeerName].frameEvents;

                        foreach (GameEvent gameEvent in otherPeerQueue.Dequeue())
                        {

                            switch (gameEvent.mType)
                            {
                                case GameEventType.GameState:
                                    int peerState = (int)gameEvent.mEventData;
                                    if (peerState != expectedState)
                                    {
                                        Console.WriteLine("Peer {0} state differs from peer {1}, dropping",
                                            currentPeer.CurrentUser, otherPeerName);
                                        currentPeerState.otherPeers.RemoveAt(i);
                                        currentPeer.DisconnectPeer(otherPeerName);
                                        --i;
                                    }

                                    break;
                                case GameEventType.GameEvent:
                                    int peerGameEvent = (int)gameEvent.mEventData;
                                    currentPeerState.currentState += peerGameEvent;

                                    break;
                            }
                        }
                    }
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
                                    events.Add(new GameEvent(eventType, state));
                                    break;
                                case GameEventType.GameEvent:
                                    int gameEvent = reader.ReadInt32();
                                    events.Add(new GameEvent(eventType, gameEvent));
                                    break;
                            }
                        }
                    }
                    catch (System.IO.EndOfStreamException) { }

                    currentPeerState.peerFrameStates[peer.name].frameEvents.Enqueue(events);
                }
            }
        }

        static byte[] CreateGameData(PeerState peerState)
        {
            List<GameEvent> pendingEvents = peerState.localPendingEvents;
            peerState.localFrameEvents.Enqueue(new List<GameEvent>(pendingEvents));

            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    foreach (GameEvent gameEvent in peerState.localPendingEvents)
                    {
                        writer.Write((int)gameEvent.mType);
                        switch (gameEvent.mType)
                        {
                            case GameEventType.GameState:
                                writer.Write((int)gameEvent.mEventData);
                                break;
                            case GameEventType.GameEvent:
                                writer.Write((int)gameEvent.mEventData);
                                break;
                        }
                    }

                    peerState.localPendingEvents.Clear();
                    return memStream.ToArray();
                }
            }
        }

        static void PlayerJoined(P2PNetClass netClass, string username)
        {
            peerStates[netClass.CurrentUser].peerFrameStates[username] = new PeerFrameState();
            peerStates[netClass.CurrentUser].peerFrameStates[username].frameEvents = new Queue<List<GameEvent>>();
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
                peerStates[netClass.CurrentUser].peerFrameStates[peer.name] = new PeerFrameState();
                peerStates[netClass.CurrentUser].peerFrameStates[peer.name].frameEvents = new Queue<List<GameEvent>>();
                peerStates[netClass.CurrentUser].otherPeers.Add(peer.name);
            }

            Console.WriteLine(string.Format("{0}: joined game with {1}", netClass.CurrentUser,
                string.Join(",", otherPeers.ConvertAll((Peer p) => { return p.name; }).ToArray())));
        }
    }
}
