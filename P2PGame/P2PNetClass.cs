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
        ClientJoinRequest,
        ServerGameInformation, ServerPlayerJoined, ServerStartGame,
        PeerConnect, PeerGameData
    }

    #region delegates

    public delegate void PlayerJoinedHandler(P2PNetClass netClass, string username);
    public delegate void JoinedGameHandler(P2PNetClass netClass, List<Peer> otherPeers);
    public delegate void PeerConnectedHandler(P2PNetClass netClass, Peer peer);
    public delegate void GameStartedHandler(P2PNetClass netClass, int seed);

    #endregion

    public class P2PNetClass
    {
        public event PlayerJoinedHandler PlayerJoined;
        public event JoinedGameHandler JoinedGame;
        public event PeerConnectedHandler PeerConnected;
        public event GameStartedHandler GameStarted;

        public string CurrentUser { get; private set; }
        public bool IsServerHost { get; private set; }
        public bool IsInGame { get; private set; }
        public IPEndPoint ServerIP { get; private set; }
        public Peer ServerPeer { get; private set; }

        Queue<P2PMessage> queuedGeneralMessages;
        Queue<P2PMessage> queuedGameMessages;

        List<Peer> peers;
        List<Peer> pendingPeers;
        List<Peer> pendingPeersToRemove;
        List<Peer> peersToAdd;

        List<string> expectedPeers;
        TcpClient client;
        TcpListener listener;

        public P2PNetClass(int port, string username)
        {
            queuedGeneralMessages = new Queue<P2PMessage>();
            queuedGameMessages = new Queue<P2PMessage>();

            peers = new List<Peer>();
            pendingPeers = new List<Peer>();
            pendingPeersToRemove = new List<Peer>();
            peersToAdd = new List<Peer>();
            expectedPeers = new List<string>();

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
            pendingPeers = new List<Peer>();
            pendingPeersToRemove = new List<Peer>();
            peersToAdd = new List<Peer>();
            expectedPeers = new List<string>();

            client = new TcpClient();

            listener = CreateListener(0);

            IsInGame = false;
            IsServerHost = false;
            CurrentUser = username;

            ConnectToServer(ip, port);
        }

        public void CheckEvents()
        {
            while (listener.Pending())
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
                while (peer.CheckForData(out message))
                {
                    switch (message.messageType)
                    {
                        case P2PNotices.ClientJoinRequest:
                            if (IsServerHost)
                                OnConnectionRequest(peer, message.data);
                            break;
                        case P2PNotices.PeerConnect:
                            if (!IsServerHost)
                                OnPeerConnected(peer, message.data);
                            break;
                    }
                }
            }

            if (!IsServerHost && !IsInGame)
            {
                P2PMessage message;
                while (ServerPeer.CheckForData(out message))
                {
                    switch (message.messageType)
                    {
                        case P2PNotices.ServerGameInformation:
                            OnServerInformationReceived(message.data);
                            break;
                        case P2PNotices.ServerPlayerJoined:
                            OnPlayerJoined(message.data);
                            break;
                        case P2PNotices.ServerStartGame:
                            OnGameStarted(message.data);
                            break;
                    }
                }
            }

            pendingPeers.RemoveAll((Peer p) => { return pendingPeersToRemove.Contains(p); });
            peers.AddRange(peersToAdd);

            pendingPeersToRemove.Clear();
            peersToAdd.Clear();
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

        public void SendData(P2PNotices type, byte[] bytes)
        {
            foreach (Peer peer in peers)
            {
                peer.SendData(type, bytes);
            }
        }

        public void StartGame(int seed)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(seed);
                    SendData(P2PNotices.ServerStartGame, memStream.ToArray());
                }
            }
        }

        public void SendServerInformation(Peer destinationPeer)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(memStream))
                {
                    writer.Write(CurrentUser);
                    writer.Write(peers.Count((Peer p) => { return p.isLoggedIn; }) +
                        peersToAdd.Count((Peer p) => { return p.isLoggedIn; }));

                    foreach (Peer peer in peers)
                    {
                        if (peer.isLoggedIn)
                        {
                            writer.Write(peer.Address.Address.ToString());
                            writer.Write(peer.listenPort);
                            writer.Write(peer.name);
                        }
                    }

                    foreach (Peer peer in peersToAdd)
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

                    foreach (Peer peer in peersToAdd)
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
                PlayerJoined(this, connectingPeer.name);

            if (PeerConnected != null)
                PeerConnected(this, connectingPeer);

            connectingPeer.isLoggedIn = true;
            pendingPeersToRemove.Add(connectingPeer);
            peersToAdd.Add(connectingPeer);
        }


        void OnServerInformationReceived(byte[] data)
        {
            using (System.IO.MemoryStream readStream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(readStream))
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

                        using (System.IO.MemoryStream writeStream = new System.IO.MemoryStream())
                        {
                            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(writeStream))
                            {
                                writer.Write(CurrentUser);
                                peer.SendData(P2PNotices.PeerConnect, writeStream.ToArray());
                            }
                        }

                        peer.isLoggedIn = true;
                        peer.isConnected = true;
                        peersToAdd.Add(peer);
                    }
                }
            }

            if (JoinedGame != null)
                JoinedGame(this, peers.Union(peersToAdd).ToList());
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

                    Peer peer = pendingPeers.FirstOrDefault((Peer p) => { return p.name == username; });

                    if (peer == null)
                    {
                        expectedPeers.Add(username);

                        if (PlayerJoined != null)
                            PlayerJoined(this, username);
                    }
                    else
                    {
                        pendingPeersToRemove.Add(peer);
                        peersToAdd.Add(peer);

                        if (PeerConnected != null)
                            PeerConnected(this, peer);
                    }
                }
            }
        }

        void OnPeerConnected(Peer peer, byte[] data)
        {
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream))
                {
                    string username = reader.ReadString();
                    peer.name = username;

                    if (expectedPeers.Contains(username))
                    {
                        expectedPeers.Remove(username);
                        pendingPeersToRemove.Add(peer);
                        peersToAdd.Add(peer);

                        if (PeerConnected != null)
                            PeerConnected(this, peer);
                    }
                }
            }
        }

        private void OnGameStarted(byte[] data)
        {
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(data))
            {
                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream))
                {
                    int seed = reader.ReadInt32();

                    if (GameStarted != null)
                        GameStarted(this, seed);
                }
            }
        }
        #endregion
    }
}
