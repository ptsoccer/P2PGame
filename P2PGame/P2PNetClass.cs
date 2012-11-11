using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PGame
{
    public enum P2PNotices
    {
        ClientJoinRequest, ClientChat,
        ServerGameInformation, ServerConnectionDenied, ServerChat, ServerPlayerJoined, ServerPlayerKicked,
        PeerConnect, PeerChat, PeerGameData
    }

    #region delegates

    public delegate void ConnectionRejectedHandler(string reason);
    public delegate bool ConnectionRequestHandler(string username, string emulator, IPEndPoint ip);
    public delegate void PlayerJoinedHandler(Peer peer);
    public delegate void JoinedGameHandler(List<Peer> otherPeers);
    public delegate void ChatEventHandler(string username, string message);
    public delegate void PlayerKickedHandler(Peer peer);
    public delegate void PeerConnectedHandler(Peer peer);

    #endregion

    public class P2PNetClass
    {
        public event ConnectionRequestHandler ConnectionRequested;
        public event ConnectionRejectedHandler ConnectionRejected;
        public event PlayerJoinedHandler PlayerJoined;
        public event JoinedGameHandler JoinedGame;
        public event ChatEventHandler ChatEvent;
        public event PlayerJoinedHandler PlayerKicked;
        public event PeerConnectedHandler PeerConnected;

        public string CurrentUser { get; private set; }
        public bool IsServerHost { get; private set; }
        public bool IsInGame { get; private set; }
        public IPEndPoint ServerIP { get; private set; }
        public Peer ServerPeer { get; private set; }

        Queue<P2PMessage> queuedGeneralMessages;
        Queue<P2PMessage> queuedGameMessages;

        List<Peer> peers;
        List<Peer> pendingPeers;
        TcpClient client;
        TcpListener listener;

        public P2PNetClass(int port, string username)
        {
            queuedGeneralMessages = new Queue<P2PMessage>();
            queuedGameMessages = new Queue<P2PMessage>();

            peers = new List<Peer>();
            client = new TcpClient();
            listener = CreateListener(port);

            IsInGame = false;
            IsServerHost = true;
            CurrentUser = username;
        }

        public P2PNetClass(IPAddress ip, int port, string username)
        {
            queuedGeneralMessages = new Queue<P2PMessage>();
            queuedGameMessages = new Queue<P2PMessage>();
            peers = new List<Peer>();
            client = new TcpClient();

            listener = CreateListener(0);

            IsInGame = false;
            IsServerHost = true;
            CurrentUser = username;

            ConnectToServer(ip, port);
        }

        public void CheckEvents()
        {
            if (listener.Pending())
            {
                TcpClient peerClient = listener.AcceptTcpClient();
                Peer peer = new Peer(peerClient);
                peer.isLoggedIn = false;
                peer.isConnected = true;
                pendingPeers.Add(peer);
            }

            PollMessages();
        }

        public void ConnectToServer(IPAddress ip, int port)
        {
            ServerIP = new IPEndPoint(ip, port);
            Peer peer = new Peer(ServerIP, true);
            ServerPeer = peer;
            AddPeer(peer);
            peer.isConnected = true;

            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(((IPEndPoint)listener.LocalEndpoint).Port);
                    writer.Write(CurrentUser);

                    byte[] bytes = memStream.ToArray();
                    ServerPeer.SendData(P2PNotices.ClientJoinRequest, bytes);
                }
            }
        }

        public void ConnectToPeer(IPAddress ip, int port)
        {
            Peer peer = new Peer(new IPEndPoint(ip, port), true);
            AddPeer(peer);
            peer.isConnected = true;

            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(CurrentUser);

                    byte[] bytes = memStream.ToArray();
                    ServerPeer.SendData(P2PNotices.PeerConnect, bytes);
                }
            }
        }

        public TcpListener CreateListener(int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            return listener;
        }

        public void PollMessages()
        {
            foreach (Peer peer in pendingPeers)
            {
                P2PMessage message;
                if (peer.CheckForData(out message))
                {
                    switch (message.messageType)
                    {
                        case P2PNotices.ClientJoinRequest:
                            if (IsServerHost)
                                OnConnectionRequest(peer, message.data);
                            break;
                    }
                }
            }

            foreach (Peer peer in peers)
            {
                P2PMessage message;
                if (peer.CheckForData(out message))
                {
                    switch (message.messageType)
                    {
                        case P2PNotices.ServerGameInformation:
                            if (peer.Equals(ServerPeer))
                                OnServerInformationReceived(message.data);
                            break;
                        //case P2PNotices.ServerPlayerJoined:
                        //    if (peer.ip.Equals(ServerIP))
                        //        OnPlayerJoined(message.data);
                        //    break;
                        case P2PNotices.ServerPlayerJoined:
                            if (peer.Equals(ServerPeer))
                                OnPlayerJoined(message.data);
                            break;
                    }
                }
            }
        }

        private void AddPeer(Peer peer)
        {
            peers.Add(peer);
        }

        private void RemovePeer(Peer peer)
        {
            peers.Remove(peer);
        }

        public Peer GetPeerFromIP(IPEndPoint ip)
        {
            return peers.Find((Peer current) => { return current.Address.Equals(ip); });
        }

        public Peer GetPeerFromName(string name)
        {
            return peers.Find((Peer current) => { return current.name.Equals(name); });
        }

        #region P2P Actions
        //private void SendDataToPeer(P2PNotices type, Peer peer, byte[] bytes)
        //{
        //    peer.SendDataToPeer(type, bytes, client);
        //}

        //public void SendData(P2PNotices type, byte[] bytes)
        //{
        //    foreach (Peer peer in peers)
        //    {
        //        SendDataToPeer(type, peer, bytes);
        //    }
        //}

        public void SendServerInformation(Peer destinationPeer)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(CurrentUser);
                    writer.Write(peers.Count((Peer p) => { return p.isLoggedIn; }));

                    foreach (Peer peer in peers)
                    {
                        if (peer.isLoggedIn)
                        {
                            writer.Write(peer.Address.Address.ToString());
                            writer.Write(peer.listenPort);
                            writer.Write(peer.name);
                        }
                    }

                    destinationPeer.SendData(P2PNotices.ServerGameInformation, memStream.ToArray());
                }
            }
        }
        #endregion

        #region events
        void OnConnectionRequest(Peer connectingPeer, byte[] data)
        {
            // Read peer's info
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(memStream))
                {
                    int port = reader.ReadInt32();
                    string username = reader.ReadString();

                    connectingPeer.name = username;
                    connectingPeer.listenPort = port;
                }
            }

            // Send connecting peers info to everyone else
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(connectingPeer.Address.Address.ToString());
                    writer.Write(connectingPeer.listenPort);
                    writer.Write(connectingPeer.name);

                    foreach (Peer peer in peers)
                    {
                        if (peer.isLoggedIn)
                        {
                            peer.SendData(P2PNotices.ServerPlayerJoined, memStream.ToArray());
                        }
                    }
                }
            }

            // Send everyone else's info to connecting peer
            SendServerInformation(connectingPeer);

            if (PlayerJoined != null)
                PlayerJoined(connectingPeer);

            connectingPeer.isLoggedIn = true;
            pendingPeers.Remove(connectingPeer);
            peers.Add(connectingPeer);
        }


        void OnServerInformationReceived(byte[] data)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(memStream))
                {
                    ServerPeer.name = reader.ReadString();

                    int peerCount = reader.ReadInt32();
                    for (int i = 0; i < peerCount; ++i)
                    {
                        string address = reader.ReadString();
                        int port = reader.ReadInt32();
                        string username = reader.ReadString();

                        Peer peer = new Peer(new IPEndPoint(IPAddress.Parse(address), port), true);
                        peer.name = username;
                        peer.isLoggedIn = true;
                        peer.isConnected = true;
                        peers.Add(peer);
                    }
                }
            }

            if (JoinedGame != null)
                JoinedGame(peers);
        }

        void OnPlayerJoined(byte[] data)
        {
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream))
                {
                    string address = reader.ReadString();
                    int port = reader.ReadInt32();
                    string username = reader.ReadString();

                    Peer peer = new Peer(new IPEndPoint(IPAddress.Parse(address), port), false);
                    pendingPeers.Add(peer);

                    if (PlayerJoined != null)
                        PlayerJoined(peer);
                }
            }
        }
        #endregion
    }
}
