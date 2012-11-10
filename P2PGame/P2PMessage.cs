using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace P2PGame
{
    public class P2PMessage
    {
        public IPEndPoint sender;
        public short messageLength;
        public P2PNotices messageType;
        public byte[] data;

        public bool Empty()
        {
            if (messageLength == 0 && messageNumber == 0 && messageType == 0 && data == null)
            {
                return true;
            }

            return false;
        }

        public byte[] Serialize()
        {
            // 5 = messageNumber + messageLength + messageType
            byte[] message = new byte[5 + data.Length];
            byte[] buffer;

            buffer = BitConverter.GetBytes(messageNumber);
            buffer.CopyTo(message, 0);

            buffer = BitConverter.GetBytes(messageLength);
            buffer.CopyTo(message, 2);

            message[4] = (byte)messageType;

            buffer = data;
            buffer.CopyTo(message, 5);

            return message;
        }

        public static P2PMessage Deserialize(byte[] data, int index, out int outIndex)
        {
            try
            {
                P2PMessage message = new P2PMessage();

                message.messageNumber = BitConverter.ToUInt16(data, index);
                message.messageLength = BitConverter.ToInt16(data, index + 2);
                message.messageType = (P2PNotices)data[index + 4];

                message.data = new byte[message.messageLength - 1];
                Array.Copy(data, index + 5, message.data, 0, message.messageLength - 1);

                outIndex = index + 4 + message.messageLength;
                return message;
            }
            catch (Exception)
            {
                outIndex = index;
                return null;
            }
        }

        public static P2PMessage Deserialize(byte[] data, int index)
        {
            int buf;
            return Deserialize(data, index, out buf);
        }

        public static P2PMessage Deserialize(byte[] data)
        {
            int buf;
            return Deserialize(data, 0, out buf);
        }
    }
}