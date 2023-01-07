# Hermod

Hermod is a .NET library for simplified advanced networking tasks.

- TCP server / client
- UDP server / client
- DNS client
- HTTP server / client
- WebSocket server / client
- ModbusTCP server / client
- ModbusUDP server / client
- SMTP client with PGP support


#### TCPServer Usage

    var tcpServer = new TCPServer(new IPPort(2000),
                                   NewTCPConnection => {
                                       NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
                                       NewConnection.Close();
                                   },
                                   Autostart: true);



#### UDPServer Usage

    var udpServer = new UDPServer(new IPPort(5000),
                                   NewUDPPacket => {
                                       Console.WriteLine("Incoming udp packet from: " + NewUDPPacket.RemoteHost + ":" + NewUDPPacket.RemotePort);
                                   },
                                   Autostart: true);

