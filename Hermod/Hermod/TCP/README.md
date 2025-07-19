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
    var echoServer  = EchoTestServer.StartNew(8080);
    var echoClient  = await EchoTestClient.ConnectNew(8080);

    var t1 = await echoClient.SendText("Hello from client 1.1!");
    var t2 = await echoClient.SendBinary(Encoding.UTF8.GetBytes("Hello from client 1.2!"));
```
