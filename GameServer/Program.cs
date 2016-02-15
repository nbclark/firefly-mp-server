using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Xml;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Specialized;
using System.Diagnostics;

namespace GameServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using (ConsoleTraceListener tl = new ConsoleTraceListener())
            {
                Trace.Listeners.Add(tl);

                Thread tcpThread = new Thread(new ThreadStart(TcpThreadHandler));
                tcpThread.IsBackground = true;
                //tcpThread.Start();

                UdpClient udpClient = new UdpClient(80);

                while (true)
                {
                    IPEndPoint endPoint = null;
                    try
                    {
                        byte[] bytes = udpClient.Receive(ref endPoint);
                        ProcessMessage(udpClient, bytes, endPoint);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            GameInstance.GamePlayer.SendUpdate(GameMessage.Disconnect, udpClient, endPoint, Encoding.ASCII.GetBytes(ex.Message), Int16.MaxValue);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private static void TcpThreadHandler()
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 80);
            listener.Start();

            while (true)
            {
                TcpClient socket = listener.AcceptTcpClient();
            }
        }

        private static Database.GameServer _gameDb = new Database.GameServer("Data Source=(local);Initial Catalog=gameserver;Persist Security Info=True;User ID=sa;Password=ce-86944");

        static string GetGameInfo()
        {
            Guid gameGuid = new Guid("D52C23ED-2E5C-499B-A6EE-AF8EF5B2B8AB");
            gameGuid = new Guid("323C0BA8-B364-4CD6-B385-B9B29A829F68");

            var games = (from gamesx in _gameDb.Games
                         where gamesx.PkGame == gameGuid
                         select gamesx);

            var devices = (from devicesx in _gameDb.GameDevices
                           where devicesx.IdGame == gameGuid
                           select devicesx);

            Database.Game game = games.First();

            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter tw = new XmlTextWriter(sw))
                {
                    tw.WriteStartElement("Game");
                    tw.WriteElementString("Id", game.PkGame.ToString());
                    tw.WriteElementString("Ready", game.Ready.ToString());
                    tw.WriteElementString("Width", game.Width.ToString());
                    tw.WriteElementString("Height", game.Height.ToString());
                    tw.WriteElementString("MapData", game.MapData.ToString());
                    tw.WriteStartElement("Users");

                    foreach (Database.GameDevice device in devices)
                    {
                        tw.WriteStartElement("User");
                        tw.WriteElementString("Device", device.Device.ToString());
                        tw.WriteElementString("User", device.User.ToString());
                        tw.WriteElementString("Accepted", device.Accepted.ToString());
                        tw.WriteElementString("Ready", device.Ready.ToString());
                        tw.WriteElementString("Latitude", device.Latitude.ToString());
                        tw.WriteElementString("Longitude", device.Longitude.ToString());

                        string address = "";

                        if (address == "192.168.1.1")
                        {
                            address = "71.231.186.6";
                        }

                        tw.WriteElementString("Address", address);
                        tw.WriteElementString("LocalAddress", device.LocalAddress);
                        tw.WriteElementString("Port", "80");
                        tw.WriteEndElement();
                    }

                    tw.WriteEndElement();
                    tw.WriteEndElement();
                }
                return sw.ToString();
            }
        }

        static void ReplayGame(object args)
        {
            object[] argsArr = (object[])args;
            UdpClient client = (UdpClient)argsArr[0];
            IPEndPoint endPoint = (IPEndPoint)argsArr[1];

            Guid gameGuid = new Guid("D52C23ED-2E5C-499B-A6EE-AF8EF5B2B8AB");
            gameGuid = new Guid("323C0BA8-B364-4CD6-B385-B9B29A829F68");

            var gameStates = (from gameState in _gameDb.GameStates
                         where
                             (gameState.IdGame == gameGuid)
                             && gameState.Device.Contains("_master")
                             && gameState.Data.Length > 2
                             orderby gameState.Date ascending
                              select gameState);

            DateTime lasttime = DateTime.Now;

            int identifier = 10;

            byte[] gameInfo = Encoding.ASCII.GetBytes(GetGameInfo());
            Thread.Sleep(2000);
            GameServer.GameInstance.GamePlayer.SendUpdate(GameMessage.GetGameDetailsResponse, client, endPoint, gameInfo, identifier++);
            Thread.Sleep(5000);
            GameServer.GameInstance.GamePlayer.SendUpdate(GameMessage.StartResponse, client, endPoint, new byte[0], identifier++);
            Thread.Sleep(3000);

            int i = 0;
            foreach (Database.GameState state in gameStates)
            {
                if (i++ > 5)
                {
                    if (lasttime < state.Date)
                    {
                        Thread.Sleep((int)(state.Date - lasttime).TotalMilliseconds);
                    }
                    lasttime = state.Date;

                    GameServer.GameInstance.GamePlayer.SendUpdate(GameMessage.PushStateResponse, client, endPoint, state.Data.ToArray(), identifier++);
                }
            }
        }

        static Dictionary<int, GameInstance> _mappings = new Dictionary<int, GameInstance>();

        static void ProcessMessage(UdpClient client, byte[] bytes, IPEndPoint endPoint)
        {
            // Send this - TIME (4-bytes)|MessageType|MessageData
            int messageOffset = 0;
            Int32 timeStamp = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes, messageOffset));
            messageOffset += 4;
            GameMessage gameMessage = (GameMessage)bytes[messageOffset];
            messageOffset += 1;

            byte[] data = new byte[bytes.Length - messageOffset];
            Array.Copy(bytes, messageOffset, data, 0, data.Length);

            int hash = endPoint.ToString().GetHashCode();

            if (_mappings.ContainsKey(hash) && !_mappings[hash].Game.Completed)
            {
                GameInstance instance = _mappings[hash];
                instance.EnqueuePacket(endPoint, timeStamp, gameMessage, data);
            }
            else
            {
                // We aren't a game member yet
                int offset = 0;
                Int16 queryStringLength = 0;

                if (data.Length >= 2)
                {
                    queryStringLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, offset));
                    offset += 2;
                }

                if (gameMessage == GameMessage.Replay)
                {
                    Thread thread = new Thread(new ParameterizedThreadStart(ReplayGame));
                    thread.Start(new object[] { client, endPoint });
                }
                else if (gameMessage == GameMessage.Create)
                {
                    // Parse like a query string (for compatability with GAE version)
                    string queryString = System.Text.Encoding.ASCII.GetString(data, offset, queryStringLength);
                    offset += queryStringLength;

                    Trace.WriteLine(string.Format("Received Create: {0}", queryString));

                    Int16 mapDataLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, offset));
                    offset += 2;

                    // Create the game and farm it out
                    if (offset + mapDataLength <= data.Length && mapDataLength > 0)
                    {
                        byte[] mapData = new byte[mapDataLength+1];
                        Array.Copy(data, offset, mapData, 0, mapDataLength);

                        GameInstance instance = GameInstance.CreateInstance(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString), mapData);
                        instance.GameEnded += new EventHandler(instance_GameEnded);

                        lock (_mappings)
                        {
                            _mappings.Add(hash, instance);
                        }
                    }

                    // We had something bad
                }
                else if (gameMessage == GameMessage.Join)
                {
                    // Parse like a query string (for compatability with GAE version)
                    string queryString = System.Text.Encoding.ASCII.GetString(data, offset, queryStringLength);
                    offset += queryStringLength;

                    Trace.WriteLine(string.Format("Received Join: {0}", queryString));

                    // Create the game and farm it out
                    GameInstance instance = GameInstance.JoinGame(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString));

                    lock (_mappings)
                    {
                        _mappings.Add(hash, instance);
                    }
                }
                else if (gameMessage == GameMessage.List)
                {
                    // Parse like a query string (for compatability with GAE version)
                    string queryString = System.Text.Encoding.ASCII.GetString(data, offset, queryStringLength);
                    offset += queryStringLength;

                    Trace.WriteLine(string.Format("Received List: {0}", queryString));
                    GameInstance.ListGames(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString));
                }
                else if (gameMessage == GameMessage.NOP)
                {
                    Trace.WriteLine(string.Format("Received NOP"));
                    // If we get a NOP, just ignore it

                    //GameInstance.GamePlayer.SendUpdate(GameMessage.NOP, client, endPoint, new byte[0], 1);
                }
            }
        }

        static void instance_GameEnded(object sender, EventArgs e)
        {
            GameInstance instance = (GameInstance)sender;
            lock (_mappings)
            {
                foreach (int hash in _mappings.Keys.ToArray())
                {
                    if (_mappings[hash] == instance)
                    {
                        _mappings.Remove(hash);
                    }
                }
            }
        }
        /*
        static void InitializeConnection(object obj)
        {
            TcpClient client = (TcpClient)obj;

            try
            {
                GameMessage gameMessage;
                byte[] message = ReceiveNegotiationMessage(client, out gameMessage);

                if (null != message)
                {
                    int offset = 0;

                    Int16 queryStringLength = 0;

                    if (message.Length >= 2)
                    {
                        queryStringLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(message, offset));
                        offset += 2;
                    }

                    if (gameMessage == GameMessage.Create)
                    {
                        // Parse like a query string (for compatability with GAE version)
                        string queryString = System.Text.Encoding.ASCII.GetString(message, offset, queryStringLength);
                        offset += queryStringLength;

                        Trace.WriteLine(string.Format("Received Create: {0}", queryString));

                        Int16 mapDataLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(message, offset));
                        offset += 2;

                        // Create the game and farm it out
                        if (offset + mapDataLength <= message.Length && mapDataLength > 0)
                        {
                            byte[] mapData = new byte[mapDataLength+1];
                            Array.Copy(message, offset, mapData, 0, mapDataLength);

                            GameInstance.CreateInstance(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString), mapData);
                            return;
                        }

                        // We had something bad
                    }
                    else if (gameMessage == GameMessage.Join)
                    {
                        // Parse like a query string (for compatability with GAE version)
                        string queryString = System.Text.Encoding.ASCII.GetString(message, offset, queryStringLength);
                        offset += queryStringLength;

                        Trace.WriteLine(string.Format("Received Join: {0}", queryString));

                        // Create the game and farm it out
                        GameInstance.JoinGame(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString));
                        return;
                    }
                    else if (gameMessage == GameMessage.List)
                    {
                        // Parse like a query string (for compatability with GAE version)
                        string queryString = System.Text.Encoding.ASCII.GetString(message, offset, queryStringLength);
                        offset += queryStringLength;

                        Trace.WriteLine(string.Format("Received List: {0}", queryString));

                        if (GameInstance.ListGames(client, endPoint, System.Web.HttpUtility.ParseQueryString(queryString)))
                        {
                            // Write back the games
                            InitializeConnection(client);
                            return;
                        }
                    }
                    else if (gameMessage == GameMessage.NOP)
                    {
                        Trace.WriteLine(string.Format("Received NOP"));
                        // If we get a NOP, just ignore it
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            client.Close();
        }

        static void SendUpdate(TcpClient client, byte[] data)
        {
            byte[] length = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((Int16)data.Length));

            if (length.Length == 2)
            {
                byte[] package = new byte[2 + data.Length];
                length.CopyTo(package, 0);
                data.CopyTo(package, 2);

                client.GetStream().Write(package, 0, package.Length);
            }
            else
            {
            }
        }

        // Create, list, join all need to start with mobilesrc
        // mobilesrc|messageLength|package
        static byte[] ReceiveNegotiationMessage(TcpClient client, out GameMessage message)
        {
            message = GameMessage.AcceptUser;
            byte[] buffer = new byte[12];
            int bytesRead = 0;
            if (buffer.Length == (bytesRead = client.GetStream().Read(buffer, 0, 12)))
            {
                string szToken = System.Text.Encoding.ASCII.GetString(buffer, 0, 9);

                if (string.Equals(szToken, "mobilesrc", StringComparison.InvariantCultureIgnoreCase))
                {
                    message = (GameMessage)buffer[9];
                    Int16 messageLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 10));

                    if (messageLength < 2048)
                    {
                        byte[] receivedBuffer = new byte[messageLength];
                        if (messageLength == (bytesRead = client.GetStream().Read(receivedBuffer, 0, messageLength)))
                        {
                            return receivedBuffer;
                        }
                    }
                }
                else
                {
                    // Dump what's in there
                    while (-1 != client.GetStream().ReadByte());
                }
            }
            return null;
        }
        */
    }
}
