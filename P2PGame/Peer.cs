using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PGame
{
    public class Peer : IEquatable<Peer>
    {
        public string name;

        public bool isConnected;
        public bool isLoggedIn;
        public int playerNumber;
        public int listenPort;

        private TcpClient peerClient;
        private NetworkStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;

        public Peer(TcpClient client)
        {
            peerClient = client;
            stream = peerClient.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public Peer(IPEndPoint ip, bool shouldAttemptConnect)
        {
            peerClient = new TcpClient();

            if (shouldAttemptConnect)
            {
                peerClient.Connect(ip);
                stream = peerClient.GetStream();
                reader = new BinaryReader(stream);
                writer = new BinaryWriter(stream);
            }
        }

        public IPEndPoint Address 
        {
            get { return (IPEndPoint) peerClient.Client.RemoteEndPoint; }
        }

        public void Disconnect()
        {
            if (peerClient != null)
                peerClient.Close();
        }

        public bool Equals(Peer compare)
        {
            if (compare.Address.Equals(Address))
            {
                return true;
            }

            return false;
        }

        public void SendData(P2PNotices type, byte[] bytes)
        {
            P2PMessage message = new P2PMessage();
            message.messageType = type;
            message.data = bytes;

            writer.Write((ushort)type);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        public bool CheckForData(out P2PMessage outMessage)
        {
            if (stream.DataAvailable || reader.PeekChar() != -1)
            {
                if (!stream.DataAvailable && reader.PeekChar() != -1)
                    System.Diagnostics.Debug.WriteLine("Data available because it was buffered");

                P2PMessage message = new P2PMessage();
                message.messageType = (P2PNotices)reader.ReadUInt16();
                int messageLength = reader.ReadInt32();

                int count = 0;
                message.data = new byte[messageLength];
                while (count < messageLength)
                {
                    count += reader.Read(message.data, count, messageLength - count);
                }

                outMessage = message;
                return true;
            }

            outMessage = null;
            return false;
        }
    }
}