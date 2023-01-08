# UDP


## UDPServer Usage

    var udpServer = new UDPServer(new IPPort(5000),
                                   NewUDPPacket => {
                                       Console.WriteLine("Incoming udp packet from: " + NewUDPPacket.RemoteHost + ":" + NewUDPPacket.RemotePort);
                                   },
                                   Autostart: true);


