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

        const uint PunchThroughConst = 0x4e10d00d;

        public IPEndPoint ServerIP { get; private set; }
        public Peer CurrentUser { get; private set; }
        public bool IsServerHost { get; private set; }
        public bool IsInGame { get; private set; }

        Queue<P2PMessage> queuedGeneralMessages;
        Queue<P2PMessage> queuedGameMessages;

        List<Peer> peers;
        TcpClient client;

        public P2PNetClass()
        {
            queuedGeneralMessages = new Queue<P2PMessage>();
            queuedGameMessages = new Queue<P2PMessage>();

            peers = new List<Peer>();
            client = new TcpClient();

            IsInGame = false;
        }

        public IPEndPoint GetLocalIP()
        {
            IPAddress ip = Dns.GetHostEntry((Dns.GetHostName())).AddressList[0];
            int port = ((IPEndPoint)client.Client.LocalEndPoint).Port;
            return new IPEndPoint(ip, port);
        }

        public bool Connect(IPAddress ip, int port, string username)
        {
            client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            CurrentUser = new Peer(new IPEndPoint(IPAddress.Any, 0), GetLocalIP());
            CurrentUser.name = username;

            ServerIP = new IPEndPoint(ip, port);
            Peer peer = new Peer(ServerIP, new IPEndPoint(IPAddress.Any, 0));
            AddPeer(peer);
            peers[0].isConnected = true;

            List<byte> loginData = new List<byte>();

            return true;
        }

        public bool StartServer(int port, string username)
        {
            try
            {
                ServerIP = new IPEndPoint(IPAddress.Any, 0);
                client = new TcpClient(new IPEndPoint(IPAddress.Any, port));
                IsServerHost = true;

                CurrentUser = new Peer(ServerIP, GetLocalIP());
                CurrentUser.name = username;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        

        

        private void CheckPeerConnectionStatus()
        {
            foreach (Peer peer in peers)
            {
                if (!peer.isConnected)
                {
                    SendNatPunchThrough(peer);
                }
            }
        }

        private void QueueMessages()
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 0);
            CheckPeerConnectionStatus();

            while (client.Available > 0)
            {
                byte[] data = ReadBytes(ref ip);
                if (data == null) return;

                Peer peer = GetPeerFromIP(ip);

                if (peer != null)
                {
                    if (!peer.isConnected)
                    {
                        if (data.Length == 4 && BitConverter.ToUInt32(data, 0) == PunchThroughConst)
                        {
                            peer.isConnected = true;

                            if (PeerConnected != null)
                                PeerConnected(peer);
                        }
                    }
                    else
                    {
                        P2PMessage[] messages = GetMessagesFromBytes(data, ip);
                        if (messages == null) return;

                        messages = peer.GetRelevantMessagesFromMessages(messages);

                        foreach (P2PMessage message in messages)
                        {
                            if (message.messageType == P2PNotices.PeerGameData || message.messageType == P2PNotices.PeerCacheData)
                            {
                                queuedGameMessages.Enqueue(message);
                            }
                            else
                            {
                                queuedGeneralMessages.Enqueue(message);
                            }
                        }
                    }
                }
                else
                {
                    if (IsServerHost)
                        OnConnectionRequest(data, ip);
                }
            }
        }

        private P2PMessage GetNextGameMessage()
        {
            if (client == null)
                return new P2PMessage();

            QueueMessages();

            if (queuedGameMessages.Count > 0)
            {
                return queuedGameMessages.Dequeue();
            }

            return new P2PMessage();
        }

        private P2PMessage GetNextGeneralMessage()
        {
            lock (p2pLock)
            {
                if (client == null) 
                    return new P2PMessage();

                QueueMessages();

                if (queuedGeneralMessages.Count > 0)
                {
                    return queuedGeneralMessages.Dequeue();
                }

                return new P2PMessage();
            }
        }

        public void PollGeneralMessages()
        {
            P2PMessage msg = GetNextGeneralMessage();

            while (!msg.Empty())
            {
                switch (msg.messageType)
                {
                    case P2PNotices.ServerConnectionDenied:
                        if (msg.sender.Equals(ServerIP))
                            OnConnectionRejected(msg.data);
                        break;
                    case P2PNotices.ServerGameInformation:
                        if (msg.sender.Equals(ServerIP))
                            OnServerInformationReceived(msg.data);
                        break;
                    case P2PNotices.ServerChat:
                        if (msg.sender.Equals(ServerIP))
                            OnChatEvent(msg.data);
                        break;
                    case P2PNotices.ServerPlayerJoined:
                        if (msg.sender.Equals(ServerIP))
                            OnPlayerJoined(msg.data);
                        break;
                    case P2PNotices.ServerPlayerKicked:
                        if (msg.sender.Equals(ServerIP))
                            OnPlayerKicked(msg.data);
                        break;


                    case P2PNotices.ClientChat:
                        if (IsServerHost)
                            ProcessChatRequest(msg);
                        break;

                }

                msg = GetNextGeneralMessage();
            }
        }

        //private void AddPeer(IPEndPoint ip)
        //{
        //    peers.Add(new Peer(ip));
        //}

        private void AddPeer(Peer peer)
        {
            peers.Add(peer);
        }

        //private void RemovePeer(IPEndPoint ip)
        //{
        //    peers.Remove(new Peer(ip));
        //}

        private void RemovePeer(Peer peer)
        {
            peers.Remove(peer);
        }

        public Peer GetPeerFromIP(IPEndPoint ip)
        {
            return peers.Find((Peer current) => { return current.externalIP.Equals(ip); });
        }

        public Peer GetPeerFromName(string name)
        {
            return peers.Find((Peer current) => { return current.name.Equals(name); });
        }

        #region P2P Actions
        private void SendDataToPeer(P2PNotices type, Peer peer, byte[] bytes)
        {
            lock (p2pLock)
            {
                peer.SendDataToPeer(type, bytes, client);
            }
        }

        public void SendData(P2PNotices type, byte[] bytes)
        {
            if (client == null) return;

            foreach (Peer peer in peers)
            {
                SendDataToPeer(type, peer, bytes);
            }
        }

        public void DenyJoinRequest(string reason, Peer peer)
        {
            SendDataToPeer(P2PNotices.ServerConnectionDenied, peer, p2pText.GetBytes(reason));
        }

        public void SendNatPunchThrough(Peer peer)
        {
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(PunchThroughConst));
            data.AddRange(CurrentUser.Serialize(p2pText));
            client.Send(data.ToArray(), data.Count, peer.externalIP);
        }

        public void SendServerInformation(Peer destinationPeer)
        {
            List<byte> bytes = new List<byte>();

            bytes.Add((byte)peers.Count);

            bytes.AddRange(CurrentUser.Serialize(p2pText));

            foreach (Peer current in peers)
            {
                if (!current.Equals(destinationPeer))
                {
                    bytes.AddRange(current.Serialize(p2pText));
                }
            }

            SendDataToPeer(P2PNotices.ServerGameInformation, destinationPeer, bytes.ToArray());
        }

        public void KickPlayer(Peer peer)
        {
            if (IsServerHost && peer != null)
            {
                SendData(P2PNotices.ServerPlayerKicked, p2pText.GetBytes(peer.name));
                RemovePeer(peer);

                if (PlayerKicked != null)
                    PlayerKicked(peer);
            }
        }

        public void SendText(string text)
        {
            if (IsInGame)
            {
                SendData(P2PNotices.PeerChat, p2pText.GetBytes(text));
            }
            else
            {
                if (IsServerHost)
                {
                    List<byte> data = new List<byte>();

                    byte[] bytes = p2pText.GetBytes(CurrentUser.name);
                    data.Add((byte)bytes.Length);
                    data.AddRange(bytes);

                    bytes = p2pText.GetBytes(text);
                    data.AddRange(BitConverter.GetBytes(bytes.Length));
                    data.AddRange(bytes);

                    SendData(P2PNotices.ServerChat, data.ToArray());

                    if (ChatEvent != null)
                        ChatEvent(CurrentUser.name, text);
                }
                else
                {
                    SendDataToPeer(P2PNotices.ClientChat, peers[0], p2pText.GetBytes(text));
                }
            }
        }
        #endregion

        #region events
        void OnConnectionRequest(byte[] data, IPEndPoint ip)
        {
            try
            {
                int versionLength = (byte)data[0];
                string version = p2pText.GetString(data, 1, versionLength);

                int emulatorLength = (byte)data[1 + versionLength];
                string emulator = p2pText.GetString(data, 2 + versionLength, emulatorLength);

                Peer newPeer = Peer.Deserialize(data, p2pText, 2 + versionLength + emulatorLength);
                newPeer.externalIP = ip;
                AddPeer(newPeer);

                if (version != P2PVersion)
                {
                    DenyJoinRequest("P2P versions not the same", newPeer);
                    RemovePeer(newPeer);
                    return;
                }

                if (ConnectionRequested == null || !ConnectionRequested(newPeer.name, emulator, ip))
                {
                    RemovePeer(newPeer);
                }
                else
                {
                    SendServerInformation(newPeer);

                    byte[] bytes = newPeer.Serialize(p2pText);

                    foreach (Peer peer in peers)
                    {
                        if (!peer.Equals(CurrentUser) && !peer.Equals(newPeer))
                        {
                            SendDataToPeer(P2PNotices.ServerPlayerJoined, peer, bytes);
                        }
                    }

                    if (IsServerHost)
                        newPeer.isConnected = true;

                    if (PlayerJoined != null)
                        PlayerJoined(newPeer);
                }
            }
            catch (DecoderFallbackException)
            {
                return;
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
        }

        void OnConnectionRejected(byte[] data)
        {
            if (ConnectionRequested != null)
                ConnectionRejected(p2pText.GetString(data));
        }

        void OnServerInformationReceived(byte[] data)
        {
            int numberOfUsers = data[0];

            Peer peer;
            int currentIndex = 1;
            peer = Peer.Deserialize(data, p2pText, 1, out currentIndex);

            peers[0].name = peer.name;
            peers[0].internalIP = peer.internalIP;

            for (int index = 1; index < numberOfUsers; ++index)
            {
                peer = Peer.Deserialize(data, p2pText, currentIndex, out currentIndex);
                AddPeer(peer);
            }

            if (JoinedGame != null)
                JoinedGame(peers);

            for (int index = 1; index < peers.Count; ++index)
            {
                SendNatPunchThrough(peers[index]);
            }
        }

        void ProcessChatRequest(P2PMessage message)
        {
            Peer peer = GetPeerFromIP(message.sender);

            byte[] username = p2pText.GetBytes(peer.name);

            List<byte> bytes = new List<byte>();
            bytes.Add((byte)username.Length);
            bytes.AddRange(username);
            bytes.AddRange(BitConverter.GetBytes(message.data.Length));
            bytes.AddRange(message.data);

            SendData(P2PNotices.ServerChat, bytes.ToArray());

            if (ChatEvent != null)
                ChatEvent(peer.name, p2pText.GetString(message.data));
        }

        void OnChatEvent(byte[] data)
        {
            int userLength = data[0];
            string username = p2pText.GetString(data, 1, userLength);

            int messageLength = BitConverter.ToInt32(data, 1 + userLength);
            string message = p2pText.GetString(data, 5 + userLength, messageLength);

            ChatEvent(username, message);
        }

        void OnPlayerJoined(byte[] data)
        {
            Peer peer = Peer.Deserialize(data, p2pText);
            AddPeer(peer);

            if (PlayerJoined != null)
                PlayerJoined(peer);

            if (!IsServerHost)
                SendNatPunchThrough(peer);
        }

        private void OnPlayerKicked(byte[] data)
        {
            string username = p2pText.GetString(data);

            Peer peer;
            if (username != CurrentUser.name)
            {
                peer = GetPeerFromName(username);

                peers.Remove(peer);
            }
            else
            {
                peers.Clear();
                peer = CurrentUser;
            }

            if (PlayerKicked != null)
                PlayerKicked(peer);
        }
        #endregion
    }
}
