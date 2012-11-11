using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace P2PGame
{
    public class P2PMessage
    {
        public P2PNotices messageType;
        public byte[] data;
    }
}