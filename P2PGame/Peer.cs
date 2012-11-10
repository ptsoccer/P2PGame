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
        public event ConnectionRequestHandler ConnectionRequest;

        public IPEndPoint ip;
        public string name;

        public bool isConnected;
        public int playerNumber;

        private TcpClient peerClient;
        private NetworkStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;

        public Peer(IPEndPoint ip)
        {
            this.ip = ip;
        }

        public void Connect()
        {
            peerClient.Connect(ip);
            stream = peerClient.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        public bool Equals(Peer compare)
        {
            if (compare.playerNumber.Equals(playerNumber))
            {
                return true;
            }

            return false;
        }

        public byte[] Serialize(System.Text.Encoding encoding)
        {
            List<byte> bytes = new List<byte>();

            byte[] username = encoding.GetBytes(name);
            bytes.Add((byte)username.Length);
            bytes.AddRange(username);

            bytes.AddRange(ip.Address.GetAddressBytes());
            bytes.AddRange(BitConverter.GetBytes(ip.Port));

            return bytes.ToArray();
        }

        public static Peer Deserialize(byte[] data, int index, out int outIndex)
        {
            try
            {
                int currentIndex = index;
                int nameLength = data[currentIndex++];
                string name = System.Text.ASCIIEncoding.ASCII.GetString(data, currentIndex, nameLength);
                currentIndex += nameLength;

                byte[] addressBytes = new byte[4];
                Array.Copy(data, currentIndex, addressBytes, 0, 4);
                IPAddress address = new IPAddress(addressBytes);
                currentIndex += 4;

                int port = BitConverter.ToUInt16(data, currentIndex);
                currentIndex += 2;

                IPEndPoint ip = new IPEndPoint(address, port);

                Peer peer = new Peer(ip);
                peer.name = name;

                outIndex = index + currentIndex;

                return peer;
            }
            catch (Exception)
            {
                outIndex = index;
                return null;
            }
        }

        public static Peer Deserialize(byte[] data, int index)
        {
            int buf;
            return Deserialize(data, index, out buf);
        }

        public static Peer Deserialize(byte[] data)
        {
            int buf;
            return Deserialize(data, 0, out buf);
        }

        public void SendDataToPeer(P2PNotices type, byte[] bytes)
        {
            P2PMessage message = new P2PMessage();
            message.messageType = type;
            message.messageLength = (short)(bytes.Length + 1);
            message.sender = new IPEndPoint(IPAddress.Any, 0);
            message.data = bytes;

            List<byte> sendBytes = new List<byte>();
            sendBytes.AddRange(message.Serialize());
            stream.Write(sendBytes.ToArray(), 0, sendBytes.Count);
        }

        public bool CheckForData(out P2PMessage outMessage)
        {
            if (stream.DataAvailable)
            {
                int newIndex;
                P2PMessage message = P2PMessage.Deserialize(bytes, 0, out newIndex);
            }
        }

        private P2PMessage GetMessageFromBytes(byte[] bytes)
        {
            P2PMessage message = new P2PMessage();
            messages[index] = P2PMessage.Deserialize(bytes, currentIndex, out currentIndex);
            messages[index].sender = ip;
        }
    }
}