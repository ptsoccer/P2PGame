using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace P2PGame
{
    public partial class P2PClient : Form
    {
        P2PNetClass p2pNet = new P2PNetClass();

        private P2PClient()
        {
            InitializeComponent();

            p2pNet.ChatEvent += new ChatEventHandler(p2pNet_ChatEvent);
            p2pNet.PlayerJoined += new PlayerJoinedHandler(p2pNet_PlayerJoined);
            p2pNet.PlayerKicked += new PlayerJoinedHandler(p2pNet_PlayerKicked);
            p2pNet.PeerConnected += new PeerConnectedHandler(p2pNet_PeerConnected);
        }

        public P2PClient(int hostPort) : this()
        {
            string username = "Tits";
            p2pNet.StartServer(27888, username);
            p2pNet.ConnectionRequested += new ConnectionRequestHandler(server_ConnectionRequested);

            ListViewItem item = new ListViewItem();
            item.Text = username;
            item.SubItems.Add("0");
            lstPeers.Items.Add(item);
        }

        public P2PClient(System.Net.IPEndPoint serverIP) : this()
        {
            p2pNet.Connect(serverIP.Address, serverIP.Port, System.DateTime.Now.ToString(), "Balls");

            p2pNet.JoinedGame += new JoinedGameHandler(p2pNet_JoinedGame);
        }

        void p2pNet_PlayerJoined(Peer peer)
        {
            ListViewItem item = new ListViewItem();
            item.Text = peer.name;
            item.SubItems.Add("0");
            lstPeers.Items.Add(item);

            AddToGameChat(peer.name + " joins", Color.FromArgb(255, 50, 50, 125));
        }

        void p2pNet_PlayerKicked(Peer peer)
        {
            AddToGameChat(peer.name + " was kicked", Color.Red);

            foreach (ListViewItem item in lstPeers.Items)
            {
                if (item.Text == peer.name)
                {
                    lstPeers.Items.Remove(item);
                    break;
                }
            }
        }

        void p2pNet_PeerConnected(Peer peer)
        {
            AddToGameChat("Connected to peer " + peer.name, Color.DarkGreen);
        }

        void p2pNet_ChatEvent(string username, string message)
        {
            AddToGameChat(string.Format("{0}: {1}", username, message), Color.Black);
        }

        void p2pNet_JoinedGame(List<Peer> otherPeers)
        {
            ListViewItem item;

            foreach (Peer peer in otherPeers)
            {
                item = new ListViewItem();
                item.Text = peer.name;
                item.SubItems.Add("0");
                lstPeers.Items.Add(item);
            }

            item = new ListViewItem();
            item.Text = p2pNet.CurrentUser.name;
            item.SubItems.Add("0");
            lstPeers.Items.Add(item);
        }

        bool server_ConnectionRequested(string username, string emulator, System.Net.IPEndPoint ip)
        {
            return true;
        }

        public void AddToGameChat(string message, System.Drawing.Color color)
        {
            txtGameChat.SelectionStart = txtGameChat.TextLength;
            txtGameChat.SelectionColor = color;
            txtGameChat.SelectedText = message + '\n';

            if (!txtGameChat.Focused)
            {
                txtGameChat.ScrollToCaret();
            }
        }

        private void tmrPollMessages_Tick(object sender, EventArgs e)
        {
            p2pNet.PollGeneralMessages();
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtMessage.Text != "")
                {
                    p2pNet.SendText(txtMessage.Text);

                    txtMessage.Clear();
                }

                e.SuppressKeyPress = true;
            }
        }

        private void btnKick_Click(object sender, EventArgs e)
        {
            if (p2pNet.IsServerHost && lstPeers.SelectedItems.Count > 0)
            {
                string playerToKick = lstPeers.SelectedItems[0].Text;
                p2pNet.KickPlayer(p2pNet.GetPeerFromName(playerToKick));
            }
        }
    }
}
