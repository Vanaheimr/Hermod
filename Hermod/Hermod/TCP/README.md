# Hermod TCP

## TCPServer Usage

    var tcpServer = new TCPServer(new IPPort(2000),
                                   NewTCPConnection => {
                                       NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
                                       NewConnection.Close();
                                   },
                                   AutoStart: true);


