using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace GameServer
{
    enum GameMessage : byte
    {
        Create = 0,
        CreateResponse = 1,
        List = 10,
        ListResponse = 11,
        Join = 20,
        JoinResponse = 21,
        GetGameDetails = 30,
        GetGameDetailsResponse = 31,
        AcceptUser = 40,
        AcceptUserResponse = 41,
        RejectUser = 50,
        RejectUserResponse = 51,
        Start = 52,
        StartResponse = 53,
        SetReady = 60,
        SetReadyResponse = 61,
        PushState = 70,
        PushStateResponse = 71,
        EndGame = 90,
        EndGameResponse = 91,
        NOP = 99,
        Disconnect = 100,
        Replay = 110
    }

    class GameInstance
    {
        private static long StartTicks;
        private Database.Game _game;
        private GamePlayer _masterClient;
        private List<GamePlayer> _slaveClients = new List<GamePlayer>();
        private List<GamePlayer> _allPlayers = new List<GamePlayer>();
        private Dictionary<int, GamePlayer> _endPointMap = new Dictionary<int, GamePlayer>();
        private Dictionary<int, Int32> _lastMessageMap = new Dictionary<int, Int32>();

        private static Database.GameServer _gameDb = new Database.GameServer("Data Source=(local);Initial Catalog=gameserver;Persist Security Info=True;User ID=sa;Password=ce-86944");

        public static Dictionary<Guid, GameInstance> ActiveGames = new Dictionary<Guid, GameInstance>();
        public event EventHandler GameEnded;

        static GameInstance()
        {
            StartTicks = DateTime.Now.Ticks;
        }

        public static GameInstance CreateInstance(UdpClient client, IPEndPoint endPoint, NameValueCollection queryString, byte[] mapData)
        {
            lock (_gameDb)
            {
                var games = (from delGame in _gameDb.Games
                             where
                                 delGame.User == queryString["user"]
                             select delGame);

                foreach (Database.Game delGame in games)
                {
                    delGame.Completed = true;
                    //_gameDb.Games.DeleteOnSubmit(delGame);
                }

                Database.Game game = new Database.Game();
                game.PkGame = Guid.NewGuid();
                game.MapData = System.Text.Encoding.ASCII.GetString(mapData);
                game.User = queryString["user"];

                if (string.IsNullOrEmpty(game.User))
                {
                    game.User = queryString["id"];
                }

                game.Completed = false;
                game.Ready = false;
                game.Date = DateTime.Now;
                game.Width = Convert.ToInt32(queryString["width"]);
                game.Height = Convert.ToInt32(queryString["height"]);

                float latitude = 0, longitude = 0;
                float.TryParse(queryString["latitude"], out latitude);
                game.Latitude = latitude;
                float.TryParse(queryString["longitude"], out longitude);
                game.Longitude = longitude;
                game.GameId = Convert.ToInt32(queryString["gameId"]);

                // TODO: lookup here
                float version = 0;
                float.TryParse(queryString["gameversion"], out version);

                Database.GameDevice device = new Database.GameDevice();
                device.PkGameDevice = Guid.NewGuid();
                device.Game = game;
                device.User = queryString["user"];

                if (string.IsNullOrEmpty(device.User))
                {
                    device.User = queryString["id"];
                }
                device.Latitude = game.Latitude;
                device.Longitude = game.Longitude;
                device.LocalAddress = queryString["address"];
                device.LocalPort = int.Parse(queryString["port"]);
                device.Device = queryString["id"];
                device.Date = DateTime.Now;
                device.Ready = true;
                device.Accepted = true;

                _gameDb.Games.InsertOnSubmit(game);
                _gameDb.GameDevices.InsertOnSubmit(device);

                _gameDb.SubmitChanges();

                Trace.WriteLine("Starting Game: " + game.User);

                GameInstance instance = new GameInstance(game, device, client, endPoint);

                GamePlayer.SendUpdate(GameMessage.CreateResponse, client, endPoint, Encoding.ASCII.GetBytes(game.PkGame.ToString()), 1);
                ActiveGames.Add(game.PkGame, instance);

                Thread gameThread = new Thread(new ParameterizedThreadStart(LaunchGame));
                gameThread.IsBackground = true;
                gameThread.Start(instance);

                return instance;
            }
        }

        public static bool ListGames(UdpClient client, IPEndPoint endPoint, NameValueCollection queryString)
        {
            float latitude = 0;
            float.TryParse(queryString["latitude"], out latitude);
            float longitude = 0;
            float.TryParse(queryString["longitude"], out longitude);
            int gameId = Convert.ToInt32(queryString["gameId"]);

            // TODO: lookup here
            float version = 0;
            float.TryParse(queryString["gameversion"], out version);

            lock (_gameDb)
            {
                var games = (from game in _gameDb.Games
                             where
                                 Math.Abs(game.Latitude - latitude) < .001
                                 &&
                                 Math.Abs(game.Longitude - longitude) < .001
                                 &&
                                 game.Completed == false
                                 &&
                                 game.Ready == false
                                 &&
                                 game.GameId == gameId
                             select game);

                bool hasGame = false;
                using (StringWriter sw = new StringWriter())
                {
                    using (XmlTextWriter tw = new XmlTextWriter(sw))
                    {
                        tw.WriteStartElement("GameCollection");
                        foreach (Database.Game game in games)
                        {
                            if (ActiveGames.ContainsKey(game.PkGame))
                            {
                                hasGame = true;
                                tw.WriteStartElement("Game");
                                tw.WriteElementString("Id", game.PkGame.ToString());
                                tw.WriteElementString("User", game.User.ToString());
                                tw.WriteEndElement();
                            }
                        }
                        tw.WriteEndElement();
                    }
                    GamePlayer.SendUpdate(GameMessage.ListResponse, client, endPoint, Encoding.ASCII.GetBytes(sw.ToString()), 1);
                }
                return (games.Count() > 0);
            }
        }

        public static GameInstance JoinGame(UdpClient client, IPEndPoint endPoint, NameValueCollection queryString)
        {
            lock (_gameDb)
            {
                GameInstance instance = ActiveGames[new Guid(queryString["game"])];

                float latitude = 0, longitude = 0;
                float.TryParse(queryString["latitude"], out latitude);
                float.TryParse(queryString["longitude"], out longitude);

                Database.GameDevice device = new Database.GameDevice();
                device.PkGameDevice = Guid.NewGuid();
                device.Game = instance.Game;
                device.User = queryString["user"];
                device.Latitude = latitude;
                device.Longitude = longitude;
                device.LocalAddress = queryString["address"];
                device.LocalPort = int.Parse(queryString["port"]);
                device.Device = queryString["id"];
                device.Date = DateTime.Now;
                device.Ready = false;
                device.Accepted = false;

                Trace.WriteLine("Joining Game: " + device.User);

                _gameDb.GameDevices.InsertOnSubmit(device);
                _gameDb.SubmitChanges();

                instance.AddPlayer(device, client, endPoint);

                return instance;
            }
            GamePlayer.SendUpdate(GameMessage.JoinResponse, client, endPoint, new byte[0], 1);
        }

        protected ManualResetEvent _waitEvent = new ManualResetEvent(false);

        private static void LaunchGame(object obj)
        {
            GameInstance instance = (GameInstance)obj;
            instance.SendGameInfo();

            while (!instance.Game.Completed)
            {
                if (instance._waitEvent.WaitOne(5 * 1000))
                {
                    GamePacket[] packets;

                    lock (instance._packets)
                    {
                        packets = instance._packets.ToArray();
                    }

                    foreach (GamePacket packet in packets)
                    {
                        if (null != packet)
                        {
                            int hash = packet.EndPoint.ToString().GetHashCode();

                            if (instance._endPointMap.ContainsKey(hash))
                            {
                                Int32 lastUpdate = 0;
                                if (instance._lastMessageMap.ContainsKey(hash))
                                {
                                    lastUpdate = instance._lastMessageMap[hash];
                                }

                                if (packet.TimeStamp > lastUpdate)
                                {
                                    instance._endPointMap[hash].ProcessMessage(packet.Message, packet.Data);
                                }
                            }

                            lock (instance._packets)
                            {
                                instance._packets.Remove(packet);
                            }
                        }
                        else
                        {
                        }
                    }
                }
                else
                {
                    // We got no data for 30 seconds
                }

                if (!instance.CheckConnections())
                {
                    // we have lost contact with our master
                }
            }
        }

        public GameInstance(Database.Game game, Database.GameDevice device, UdpClient client, IPEndPoint endPoint)
        {
            _masterClient = new GamePlayer(device, client, endPoint);
            _masterClient.GotGameUpdate += new EventHandler<GameUpdateEventArgs>(_masterClient_GotGameUpdate);
            _masterClient.ClientDisconnected += new EventHandler(_masterClient_ClientDisconnected);
            _allPlayers.Add(_masterClient);
            _endPointMap.Add(endPoint.ToString().GetHashCode(), _masterClient);

            _game = game;
        }

        void _masterClient_ClientDisconnected(object sender, EventArgs e)
        {
            ActiveGames.Remove(this.Game.PkGame);
            Game.Completed = true;

            // Disconnect all clients
            foreach (GamePlayer player in _slaveClients)
            {
                try
                {
                    player.SendUpdate(GameMessage.Disconnect, Encoding.ASCII.GetBytes("The game has been disconnected"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR _masterClient_ClientDisconnected");
                    Console.WriteLine(ex.ToString());
                }
                try
                {
                    player.Close();
                }
                catch
                {
                }
            }
            _masterClient.SendUpdate(GameMessage.Disconnect, Encoding.ASCII.GetBytes("The game has been disconnected"));

            lock (_gameDb)
            {
                _gameDb.SubmitChanges();
            }
        }

        void addPlayer_ClientDisconnected(object sender, EventArgs e)
        {
            if (!Game.Completed)
            {
                ((GamePlayer)sender).SendUpdate(GameMessage.Disconnect, Encoding.ASCII.GetBytes("The game has been disconnected"));

                Trace.WriteLine("Player Disconnected: " + ((GamePlayer)sender).Device.User);

                _endPointMap.Remove(((GamePlayer)sender).EndPoint.ToString().GetHashCode());
                _allPlayers.Remove((GamePlayer)sender);
                _slaveClients.Remove((GamePlayer)sender);
            }
        }

        void _masterClient_GotGameUpdate(object sender, GameInstance.GameUpdateEventArgs e)
        {
            GamePlayer player = (GamePlayer)sender;
            if (e.Message == GameMessage.EndGame)
            {
                Trace.WriteLine("Ending Game: " + player.Device.User);
                lock (_gameDb)
                {
                    Game.Completed = true;
                    _gameDb.SubmitChanges();
                }

                if (null != GameEnded)
                {
                    GameEnded(this, new EventArgs());
                }

                player.Close();
            }
            else if (e.Message == GameMessage.AcceptUser)
            {
                Int16 queryStringLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(e.GameState, 0));
                string queryString = System.Text.Encoding.ASCII.GetString(e.GameState, 2, queryStringLength);
                NameValueCollection queryBucket = System.Web.HttpUtility.ParseQueryString(queryString);

                string device = queryBucket["id"];

                foreach (GamePlayer slave in _slaveClients)
                {
                    if (string.Equals(slave.Device.Device, device, StringComparison.InvariantCultureIgnoreCase))
                    {
                        slave.Device.Accepted = true;
                        slave.SendUpdate(GameMessage.AcceptUserResponse, new byte[0]);

                        break;
                    }
                }

                SendGameInfo();
            }
            else if (e.Message == GameMessage.RejectUser)
            {
                Int16 queryStringLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(e.GameState, 0));
                string queryString = System.Text.Encoding.ASCII.GetString(e.GameState, 2, queryStringLength);
                NameValueCollection queryBucket = System.Web.HttpUtility.ParseQueryString(queryString);

                string device = queryBucket["id"];

                foreach (GamePlayer slave in _slaveClients)
                {
                    if (string.Equals(slave.Device.Device, device, StringComparison.InvariantCultureIgnoreCase))
                    {
                        slave.SendUpdate(GameMessage.RejectUserResponse, new byte[0]);
                        slave.Close();

                        _slaveClients.Remove(slave);

                        break;
                    }
                }

                SendGameInfo();
            }
            else if (e.Message == GameMessage.Start)
            {
                this.Game.Ready = true;
                SendGameInfo();

                Thread.Sleep(1500);
                player.SendUpdate(GameMessage.StartResponse, new byte[0]);
            }
            else if (e.Message == GameMessage.PushState)
            {
                //Int16 queryStringLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(e.GameState, 0));
                //string queryString = System.Text.Encoding.ASCII.GetString(e.GameState, 2, queryStringLength);
                //NameValueCollection queryBucket = System.Web.HttpUtility.ParseQueryString(queryString);

                //byte[] buffer = new byte[e.GameState.Length + 10];
                //Encoding.ASCII.GetBytes("mobilesrc").CopyTo(buffer, 0);
                //buffer[9] = 1;
                //e.GameState.CopyTo(buffer, 10);

                foreach (GamePlayer slave in _slaveClients)
                {
                    Console.WriteLine("_masterClient_GotGameUpdate: " + slave.Device.User);
                    //slave.SendUpdate(GameMessage.NOP, new byte[1500]);
                    try
                    {
                        slave.SendUpdate(GameMessage.PushStateResponse, e.GameState);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR _masterClient_GotGameUpdate");
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
            else
            {
            }
        }

        void _slaveClient_GotGameUpdate(object sender, GameInstance.GameUpdateEventArgs e)
        {
            GamePlayer player = (GamePlayer)sender;

            if (e.Message == GameMessage.EndGame)
            {
                // the slaves can't end it
            }
            else if (e.Message == GameMessage.SetReady)
            {
                player.Device.Ready = true;
                player.SendUpdate(GameMessage.SetReadyResponse, new byte[0]);
                SendGameInfo();
            }
            else if (e.Message == GameMessage.PushState)
            {
                byte[] buffer = new byte[e.GameState.Length + 10];
                Encoding.ASCII.GetBytes("mobilesrc").CopyTo(buffer, 0);
                buffer[9] = 1;
                e.GameState.CopyTo(buffer, 10);

                //_masterClient.SendUpdate(GameMessage.NOP, new byte[1500]);
                _masterClient.SendUpdate(GameMessage.PushStateResponse, e.GameState);
            }
            else
            {
                // Send everything to the master
                // _masterClient.SendUpdate(e.Message, e.GameState);
            }
        }

        public Database.Game Game
        {
            get { return _game; }
        }

        private class GamePacket
        {
            public Int32 TimeStamp;
            public GameMessage Message;
            public byte[] Data;
            public IPEndPoint EndPoint;
        }

        private List<GamePacket> _packets = new List<GamePacket>();

        public void EnqueuePacket(IPEndPoint endPoint, Int32 timeStamp, GameMessage message, byte[] data)
        {
            lock (_packets)
            {
                GamePacket packet = new GamePacket();
                packet.EndPoint = endPoint;
                packet.TimeStamp = timeStamp;
                packet.Message = message;
                packet.Data = data;

                _packets.Add(packet);
            }
            _waitEvent.Set();
        }

        public void AddPlayer(Database.GameDevice gameDevice, UdpClient client, IPEndPoint endPoint)
        {
            lock (this)
            {
                GamePlayer addPlayer = new GamePlayer(gameDevice, client, endPoint);
                addPlayer.GotGameUpdate += new EventHandler<GameUpdateEventArgs>(_slaveClient_GotGameUpdate);
                addPlayer.ClientDisconnected += new EventHandler(addPlayer_ClientDisconnected);

                _endPointMap.Add(endPoint.ToString().GetHashCode(), addPlayer);
                _slaveClients.Add(addPlayer);
                _allPlayers.Add(addPlayer);
            }

            SendGameInfo();
        }

        public bool CheckConnections()
        {
            try
            {
                foreach (GamePlayer player in _allPlayers.ToArray())
                {
                    if (null != player)
                    {
                        if (!player.CheckConnection())
                        {
                            // we have lost the player
                            player.Close();
                        }
                    }
                }
            }
            catch
            {
            }
            return this.Game.Completed;
        }

        public void SendGameInfo()
        {
            byte[] gameInfo = Encoding.ASCII.GetBytes(GetGameInfo());
            foreach (GamePlayer player in _allPlayers)
            {
                if (player.Device.Accepted)
                {
                    player.SendUpdate(GameMessage.GetGameDetailsResponse, gameInfo);
                }
            }
        }

        public string GetGameInfo()
        {
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter tw = new XmlTextWriter(sw))
                {
                    tw.WriteStartElement("Game");
                    tw.WriteElementString("Id", this._game.PkGame.ToString());
                    tw.WriteElementString("Ready", this._game.Ready.ToString());
                    tw.WriteElementString("Width", this._game.Width.ToString());
                    tw.WriteElementString("Height", this._game.Height.ToString());
                    tw.WriteElementString("MapData", this._game.MapData.ToString());
                    tw.WriteStartElement("Users");

                    foreach (GamePlayer player in _allPlayers)
                    {
                        tw.WriteStartElement("User");
                        tw.WriteElementString("Device", player.Device.Device.ToString());
                        tw.WriteElementString("User", player.Device.User.ToString());
                        tw.WriteElementString("Accepted", player.Device.Accepted.ToString());
                        tw.WriteElementString("Ready", player.Device.Ready.ToString());
                        tw.WriteElementString("Latitude", player.Device.Latitude.ToString());
                        tw.WriteElementString("Longitude", player.Device.Longitude.ToString());

                        string address = player.EndPoint.Address.ToString();

                        if (address == "192.168.1.1")
                        {
                            address = "71.231.186.6";
                        }

                        tw.WriteElementString("Address", address);
                        tw.WriteElementString("LocalAddress", player.Device.LocalAddress);
                        tw.WriteElementString("Port", player.EndPoint.Port.ToString());
                        tw.WriteEndElement();
                    }

                    tw.WriteEndElement();
                    tw.WriteEndElement();
                }
                return sw.ToString();
            }
        }

        internal class GameUpdateEventArgs : EventArgs
        {
            public GameUpdateEventArgs(GameMessage message, byte[] gameState, int length)
            {
                this.Message = message;
                this.GameState = new byte[length];
                Array.Copy(gameState, 0, this.GameState, 0, length);
            }
            public GameMessage Message
            {
                get;
                set;
            }
            public byte[] GameState
            {
                get;
                set;
            }
        }

        internal class GamePlayer
        {
            private Database.GameDevice _device;
            private UdpClient _client;
            private IPEndPoint _endPoint;
            private byte[] _buffer = new byte[4096];
            private IAsyncResult _result = null;

            public event EventHandler ClientDisconnected;
            public event EventHandler<GameUpdateEventArgs> GotGameUpdate;

            public GamePlayer(Database.GameDevice device, UdpClient client, IPEndPoint endPoint)
            {
                _device = device;
                _client = client;
                _endPoint = endPoint;
            }

            public GamePlayer(string deviceName, UdpClient client, IPEndPoint endPoint)
            {
                using (Database.GameServer gameDb = new Database.GameServer("Data Source=(local);Initial Catalog=gameserver;Persist Security Info=True;User ID=sa;Password=ce-86944"))
                {
                    var device = (from gameDevice in gameDb.GameDevices
                                  where gameDevice.Device == deviceName
                                  select gameDevice).First();

                    _device = device;
                    _client = client;
                    _endPoint = endPoint;
                }
            }

            public IPEndPoint EndPoint
            {
                get { return _endPoint; }
            }

            public Database.GameDevice Device
            {
                get { return _device; }
            }

            private int _identifier = 10;
            public void SendUpdate(GameMessage message, byte[] data)
            {
                try
                {
                    LastSendTime = DateTime.Now;
                    Trace.WriteLine(string.Format("Send Messsage: {0} - {1} - {2}", this.Device.User, message.ToString(), data.Length));
                    SendUpdate(message, _client, _endPoint, data, _identifier++);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR SendUpdate");
                    Console.WriteLine(ex.ToString());
                    this.Close();
                }
            }

            public static void SendUpdate(GameMessage message, UdpClient client, IPEndPoint endPoint, byte[] data, Int32 identifier)
            {
                int timeStamp = identifier;// (int)((int)(DateTime.Now.Ticks - StartTicks));
                byte[] timeStampBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(timeStamp));
                byte[] package = new byte[5 + data.Length];
                timeStampBytes.CopyTo(package, 0);
                package[4] = (byte)message;
                data.CopyTo(package, 5);

                client.Send(package, package.Length, endPoint);
            }

            public DateTime LastSendTime = DateTime.MinValue;
            public DateTime LastRecvTime = DateTime.Now;

            public bool CheckConnection()
            {
                if (DateTime.Now.Subtract(LastRecvTime).TotalSeconds > 15)
                {
                    return false;
                }
                if (DateTime.Now.Subtract(LastSendTime).TotalSeconds > 5)
                {
                    SendUpdate(GameMessage.NOP, new byte[0]);
                }
                return true;
            }

            Int32 _lastMessageTime = 0;
            public void ProcessMessage(GameMessage gameMessage, byte[] data)
            {
                Trace.WriteLine(string.Format("Recv Messsage: {0} - {1} - {2}", this.Device.User, gameMessage.ToString(), data.Length));

                LastRecvTime = DateTime.Now;

                Database.GameState state = new Database.GameState();
                state.PkGameState = Guid.NewGuid();
                state.Game = this.Device.Game;
                state.Master = false;
                state.Device = this.Device.Device;

                state.Data = data;
                state.Date = DateTime.Now;
                state.Active = false;
                state.Received = true;

                try
                {
                    lock (_gameDb)
                    {
                        _gameDb.GameStates.InsertOnSubmit(state);
                        _gameDb.SubmitChanges();
                    }
                }
                catch
                {
                }

                // we got our game state
                if (null != GotGameUpdate)
                {
                    GotGameUpdate(this, new GameUpdateEventArgs(gameMessage, data, data.Length));
                }
            }

            /*
            public void BeginReadUpdates()
            {
                // Get messagetype + message length
                try
                {
                    _result = _client.GetStream().BeginRead(_buffer, 0, 3, new AsyncCallback(GotPackageData), null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR BeginReadUpdates");
                    Console.WriteLine(ex.ToString());
                    this.Close();
                }
            }
            */

            public void Close()
            {
                if (null != ClientDisconnected)
                {
                    ClientDisconnected(this, null);
                }
                //_client.Close();
            }

            /*
            private void GotPackageData(IAsyncResult result)
            {
                if (!_client.Connected)
                {
                    return;
                }
                _result = null;
                try
                {
                    if (result.IsCompleted)
                    {
                        int bytesRead = _client.GetStream().EndRead(result);

                        if (bytesRead == 3)
                        {
                            GameMessage gameMessage = (GameMessage)_buffer[0];
                            Int16 messageLength = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(_buffer, 1));
                            Trace.WriteLine("Player Messsage: " + gameMessage.ToString() + " - " + this.Device.User + " - " + messageLength);

                            if (messageLength > 0 && messageLength < _buffer.Length - 3)
                            {
                                int totalBytesRead = 0;
                                do
                                {
                                    bytesRead = _client.GetStream().Read(_buffer, totalBytesRead, messageLength - totalBytesRead);
                                    totalBytesRead += bytesRead;

                                    if (totalBytesRead < messageLength)
                                    {
                                    }
                                }
                                while (totalBytesRead < messageLength);

                                if (messageLength == bytesRead)
                                {
                                    Database.GameState state = new Database.GameState();
                                    state.PkGameState = Guid.NewGuid();
                                    state.Game = this.Device.Game;
                                    state.Master = false;
                                    state.Device = this.Device.Device;

                                    byte[] data = new byte[messageLength];
                                    Array.Copy(_buffer, data, messageLength);

                                    state.Data = data;
                                    state.Date = DateTime.Now;
                                    state.Active = false;
                                    state.Received = true;

                                    try
                                    {
                                        lock (_gameDb)
                                        {
                                            _gameDb.GameStates.InsertOnSubmit(state);
                                            _gameDb.SubmitChanges();
                                        }
                                    }
                                    catch
                                    {
                                    }

                                    // we got our game state
                                    if (null != GotGameUpdate)
                                    {
                                        GotGameUpdate(this, new GameUpdateEventArgs(gameMessage, _buffer, messageLength));
                                    }
                                }
                                else
                                {
                                    int bytesRead2 = _client.GetStream().Read(_buffer, 0, _buffer.Length);
                                }
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                            //this.Close();
                        }
                    }
                    //SendUpdate(GameMessage.NOP, new byte[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR GotPackageData");
                    Console.WriteLine(ex.ToString());
                    this.Close();
                    return;
                }
                if (_client.Connected)
                {
                    BeginReadUpdates();
                }
            }
            */
        }
    }
}
