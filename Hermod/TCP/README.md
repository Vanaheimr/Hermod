# Hermod TCP

## TCPServer Usage

```
    var tcpServer = new TCPServer(new IPPort(2000),
                                   NewTCPConnection => {
                                       NewConnection.WriteToResponseStream("Hello world!" + Environment.NewLine + Environment.NewLine);
                                       NewConnection.Close();
                                   },
                                   AutoStart: true);
```


## TCP EchoTest Server Usage
```
    var echoServer  = await TCPEchoTestServer.StartNew(TCPPort: IPPort.Parse(8080));

    // Sends back whatever a client sends, until the client closes its side.
```
