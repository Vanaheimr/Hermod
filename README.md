Hermod
=================

A .NET library for simplified networking, HTTP/REST and hypermedia transfer.


#### TCPServer Usage

    var _TCPServer = new TCPServer(new IPPort(2000),
                                   NewTCPConnection => {
                                       NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
                                       NewConnection.Close();
                                   },
                                   Autostart: true);



#### UDPServer Usage

    var _UDPServer = new UDPServer(new IPPort(5000),
                             NewUDPPacket => {
                                 Console.WriteLine("Incoming udp packet from: " + NewUDPPacket.RemoteHost + ":" + NewUDPPacket.RemotePort);
                             },
                             Autostart: true);


#### HTTPServer Usage

    // This service uses a custom HTTPService defined within IRESTService.cs
    var _HTTPServer2 = new HTTPServer<IRESTService>(IPPort.HTTP, Autostart: true) {
                               ServerName = "Customized Hermod Demo"
                           };

