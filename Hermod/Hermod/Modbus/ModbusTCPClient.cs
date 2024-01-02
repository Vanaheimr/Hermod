/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Modbus.Toolbox;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    public delegate Task ReadHoldingRegistersClientRequestDelegate (DateTime                       Timestamp,
                                                                    ModbusTCPClient                Sender,
                                                                    IPSocket                       RemoteSocket,
                                                                    String                         ConnectionId,
                                                                    ReadHoldingRegistersRequest    ReadHoldingRegistersRequest);

    public delegate Task ReadHoldingRegistersClientResponseDelegate(DateTime                       Timestamp,
                                                                    ModbusTCPClient                Sender,
                                                                    IPSocket                       RemoteSocket,
                                                                    String                         ConnectionId,
                                                                    ReadHoldingRegistersRequest    ReadHoldingRegistersRequest,
                                                                    ReadHoldingRegistersResponse   ReadHoldingRegistersResponse,
                                                                    TimeSpan                       Runtime);



    /// <summary>
    /// The Modbus/TCP Client.
    /// </summary>
    /// <seealso cref="https://modbus.org/docs/Modbus_Messaging_Implementation_Guide_V1_0b.pdf"/>
    /// <seealso cref="https://modbus.org/docs/MB-TCP-Security-v21_2018-07-24.pdf"/>
    public class ModbusTCPClient : IModbusClient
    {

        #region Data

        private Socket?           TCPSocket;
        private MyNetworkStream?  TCPStream;
        private SslStream?        TLSStream;
        private Stream?           HTTPStream;
        private Int32             internalInvocationId;


        /// <summary>
        /// The default description.
        /// </summary>
        public const           String    DefaultDescription              = "GraphDefined Modbus/TCP Client";

        /// <summary>
        /// The default remote TCP port to connect to (502).
        /// </summary>
        public static readonly IPPort    DefaultRemotePort               = IPPort.Parse(502);

        /// <summary>
        /// The default remote TLS port to connect to (802).
        /// </summary>
        public static readonly IPPort    DefaultSecurePort               = IPPort.Parse(802);

        /// <summary>
        /// The default timeout for upstream queries.
        /// </summary>
        public static readonly TimeSpan  DefaultRequestTimeout           = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default delay between transmission retries.
        /// </summary>
        public static readonly TimeSpan  DefaultTransmissionRetryDelay   = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The default number of maximum transmission retries.
        /// </summary>
        public const           UInt16    DefaultMaxNumberOfRetries       = 3;

        #endregion

        #region Properties

        /// <summary>
        /// The remote hostname of the Modbus device to connect to.
        /// </summary>
        public HTTPHostname?                         RemoteHostname                { get; }

        /// <summary>
        /// The remote IP address of the Modbus device to connect to.
        /// </summary>
        public IIPAddress                            RemoteIPAddress               { get; }

        /// <summary>
        /// The remote TCP port of the Modbus device to connect to.
        /// </summary>
        public IPPort                                RemoteTCPPort                 { get; }

        /// <summary>
        /// The remote TCP socket of the Modbus device to connect to.
        /// </summary>
        public IPSocket?                             RemoteTCPSocket               { get; }

        /// <summary>
        /// The remote Modbus unit/device address.
        /// </summary>
        public Byte                                  UnitAddress                   { get; }


        public Int16                                 StartingAddressOffset         { get; }

        /// <summary>
        /// An optional description of this Modbus/TCP client.
        /// </summary>
        public String?                               Description                   { get; set; }

        /// <summary>
        /// The remote TLS certificate validator.
        /// </summary>
        public RemoteCertificateValidationHandler?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionHandler?    ClientCertificateSelector     { get; }

        /// <summary>
        /// The TLS client certificate to use of HTTP authentication.
        /// </summary>
        public X509Certificate?                      ClientCert                    { get; }

        /// <summary>
        /// The TLS protocol to use.
        /// </summary>
        public SslProtocols                          TLSProtocol                   { get; }

        /// <summary>
        /// Prefer IPv4 instead of IPv6.
        /// </summary>
        public Boolean                               PreferIPv4                    { get; }

        /// <summary>
        /// The timeout for requests.
        /// </summary>
        public TimeSpan                              RequestTimeout                { get; set; }

        /// <summary>
        /// The delay between transmission retries.
        /// </summary>
        public TransmissionRetryDelayDelegate        TransmissionRetryDelay        { get; }

        /// <summary>
        /// The maximum number of retries when communicationg with a remote Modbus device to connect to.
        /// </summary>
        public UInt16                                MaxNumberOfRetries            { get; }

        /// <summary>
        /// Whether to pipeline multiple Modbus request through a single TCP connection.
        /// </summary>
        public Boolean                               UseRequestPipelining          { get; }

        /// <summary>
        /// The Modbus/TCP client logger.
        /// </summary>
        public ModbusTCPClientLogger?                Logger                        { get; set; }

        /// <summary>
        /// The DNS client defines which DNS servers to use.
        /// </summary>
        public DNSClient                             DNSClient                     { get; }


        #region Available

        public Int32? Available

            => TCPSocket?.Available;

        #endregion

        #region Connected

        public Boolean? Connected

            => TCPSocket?.Connected;

        #endregion

        #region (Default)LingerState

        public LingerOption                          DefaultLingerState            { get; set; }

        public LingerOption? LingerState
        {

            get
            {
                return TCPSocket?.LingerState;
            }

            set
            {
                if (TCPSocket is not null &&
                    value     is not null)
                {
                    TCPSocket.LingerState = value;
                }
            }

        }

        #endregion

        #region (Default)NoDelay

        public Boolean                               DefaultNoDelay                { get; set; }

        public Boolean? NoDelay
        {
            get
            {
                return TCPSocket?.NoDelay;
            }
            set
            {
                if (TCPSocket is not null &&
                    value     is not null)
                {
                    TCPSocket.NoDelay = value.Value;
                }
            }
        }

        #endregion

        #region (Default)TTL

        public Byte                                  DefaultTTL                    { get; set; }

        public Byte? TTL
        {
            get
            {
                return (Byte?) TCPSocket?.Ttl;
            }
            set
            {
                if (TCPSocket is not null &&
                    value     is not null)
                {
                    TCPSocket.Ttl = value.Value;
                }
            }
        }

        #endregion


        #region NextInvocationId

        public UInt16 NextInvocationId
        {
            get
            {

                Interlocked.CompareExchange(ref internalInvocationId, 0, UInt16.MaxValue);
                Interlocked.Increment      (ref internalInvocationId);

                return (UInt16) internalInvocationId;

            }
        }

        #endregion

        #endregion

        #region Events

        public event ReadHoldingRegistersClientRequestDelegate?   OnReadHoldingRegistersRequest;

        public event ReadHoldingRegistersClientResponseDelegate?  OnReadHoldingRegistersResponse;

        #endregion

        #region Constructor(s)

        #region ModbusTCPClient(RemoteHostname,  RemoteTCPPort = null, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteHostname">The remote hostname to connect to.</param>
        /// <param name="RemoteTCPPort">An optional remote TCP port to connect to.</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(HTTPHostname                          RemoteHostname,
                               IPPort?                               RemoteTCPPort                = null,
                               Byte?                                 UnitAddress                  = null,
                               Int16?                                StartingAddressOffset        = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationHandler?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining         = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

        {

            this.RemoteHostname              = RemoteHostname;
            this.RemoteTCPPort               = RemoteTCPPort          ?? (TLSProtocol.HasValue
                                                                             ? DefaultSecurePort
                                                                             : DefaultRemotePort);
            this.UnitAddress                 = UnitAddress            ?? 1;
            this.StartingAddressOffset       = StartingAddressOffset  ?? 0;
            this.Description                 = Description;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.ClientCertificateSelector   = ClientCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.TLSProtocol                 = TLSProtocol            ?? SslProtocols.Tls12;
            this.PreferIPv4                  = PreferIPv4             ?? false;
            this.RequestTimeout              = RequestTimeout         ?? DefaultRequestTimeout;
            this.TransmissionRetryDelay      = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries          = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;
            this.UseRequestPipelining        = UseRequestPipelining;
            this.Logger                      = Logger;
            this.DNSClient                   = DNSClient              ?? new DNSClient();

            if (this.ClientCertificateSelector is null && this.ClientCert is not null)
                this.ClientCertificateSelector = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => this.ClientCert;

#pragma warning disable SCS0005 // Weak random number generator.
            this.internalInvocationId        = Random.Shared.Next(1000);
#pragma warning restore SCS0005 // Weak random number generator.

        }

        #endregion

        #region ModbusTCPClient(RemoteIPAddress, RemoteTCPPort = null, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemoteTCPPort">An optional remote TCP port to connect to.</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(IIPAddress                            RemoteIPAddress,
                               IPPort?                               RemoteTCPPort                = null,
                               Byte?                                 UnitAddress                  = null,
                               Int16?                                StartingAddressOffset        = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationHandler?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining         = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

        {

            this.RemoteIPAddress             = RemoteIPAddress;
            this.RemoteTCPPort               = RemoteTCPPort          ?? (TLSProtocol.HasValue
                                                                             ? DefaultSecurePort
                                                                             : DefaultRemotePort);
            this.UnitAddress                 = UnitAddress            ?? 1;
            this.StartingAddressOffset       = StartingAddressOffset  ?? 0;
            this.Description                 = Description;
            this.RemoteCertificateValidator  = RemoteCertificateValidator;
            this.ClientCertificateSelector   = ClientCertificateSelector;
            this.ClientCert                  = ClientCert;
            this.TLSProtocol                 = TLSProtocol            ?? SslProtocols.Tls12;
            this.PreferIPv4                  = PreferIPv4             ?? false;
            this.RequestTimeout              = RequestTimeout         ?? DefaultRequestTimeout;
            this.TransmissionRetryDelay      = TransmissionRetryDelay ?? (retryCounter => TimeSpan.FromSeconds(retryCounter * retryCounter * DefaultTransmissionRetryDelay.TotalSeconds));
            this.MaxNumberOfRetries          = MaxNumberOfRetries     ?? DefaultMaxNumberOfRetries;
            this.UseRequestPipelining        = UseRequestPipelining;
            this.Logger                      = Logger;
            this.DNSClient                   = DNSClient              ?? new DNSClient();

            if (this.ClientCertificateSelector is null && this.ClientCert is not null)
                this.ClientCertificateSelector = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => this.ClientCert;

#pragma warning disable SCS0005 // Weak random number generator.
            this.internalInvocationId        = Random.Shared.Next(1000);
#pragma warning restore SCS0005 // Weak random number generator.

        }

        #endregion

        #region ModbusTCPClient(RemoteURL, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the Modbus/TCP device to connect to.</param>
        /// <param name="UnitAddress">An optional remote Modbus unit/device address.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(URL                                   RemoteURL,
                               Byte?                                 UnitAddress                  = null,
                               Int16?                                StartingAddressOffset        = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationHandler?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionHandler?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining         = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

            : this(RemoteURL.Hostname,
                   RemoteURL.Port,
                   UnitAddress,
                   StartingAddressOffset,
                   Description,
                   RemoteCertificateValidator,
                   ClientCertificateSelector,
                   ClientCert,
                   TLSProtocol,
                   PreferIPv4,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   UseRequestPipelining,
                   Logger,
                   DNSClient)

        { }

        #endregion

        #endregion


        #region ReadCoils           (StartingAddress, NumberOfCoils)

        /// <summary>
        /// Read multiple coils.
        /// </summary>
        /// <param name="StartingAddress">The starting address for reading data.</param>
        /// <param name="NumberOfCoils">The number of coils to read.</param>
        public async Task<ReadCoilsResponse>

            ReadCoils(UInt16  StartingAddress,
                      UInt16  NumberOfCoils)

        {

            var request  = new ReadCoilsRequest(this,
                                                NextInvocationId,
                                                (UInt16) (StartingAddress + StartingAddressOffset),
                                                NumberOfCoils,
                                                UnitAddress);

            var response = new ReadCoilsResponse(request,
                                                 await WriteAsyncData(request));

            return response;

        }

        #endregion

        #region ReadDiscreteInputs  (StartingAddress, NumberOfInputs)

        /// <summary>
        /// Read discrete inputs.
        /// </summary>
        /// <param name="StartingAddress">The starting address for reading the data.</param>
        /// <param name="NumberOfInputs">The number of input inputs to read.</param>
        public async Task<ReadDiscreteInputsResponse>

            ReadDiscreteInputs(UInt16  StartingAddress,
                               UInt16  NumberOfInputs)

        {

            var request  = new ReadDiscreteInputsRequest(this,
                                                         NextInvocationId,
                                                         (UInt16) (StartingAddress + StartingAddressOffset),
                                                         NumberOfInputs,
                                                         UnitAddress);

            var response = new ReadDiscreteInputsResponse(request,
                                                          await WriteAsyncData(request));

            return response;

        }

        #endregion

        #region ReadHoldingRegisters(StartingAddress, NumberOfRegisters)

        /// <summary>
        /// Read holding registers.
        /// </summary>
        /// <param name="StartingAddress">Address from where the data read begins.</param>
        /// <param name="NumberOfRegisters">The number of input registers to read.</param>
        public async Task<ReadHoldingRegistersResponse>

            ReadHoldingRegisters(UInt16  StartingAddress,
                                 UInt16  NumberOfRegisters)

        {

            var request  = new ReadHoldingRegistersRequest(this,
                                                           NextInvocationId,
                                                           (UInt16) (StartingAddress + StartingAddressOffset),
                                                           NumberOfRegisters,
                                                           UnitAddress);

            #region Send OnReadHoldingRegistersRequest event

            var startTime = Timestamp.Now;

            //Counters.ReadHoldingRegisters.IncRequests_OK();

            try
            {

                if (OnReadHoldingRegistersRequest is not null)
                    await Task.WhenAll(OnReadHoldingRegistersRequest.GetInvocationList().
                                       Cast<ReadHoldingRegistersClientRequestDelegate>().
                                       Select(e => e(startTime,
                                                     this,
                                                     RemoteTCPSocket ?? IPSocket.AnyV4(RemoteTCPPort),
                                                     "",
                                                     request))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(ModbusTCPClient) + "." + nameof(OnReadHoldingRegistersRequest));
            }

            #endregion


            ReadHoldingRegistersResponse? response = null;

            try
            {

                response = new ReadHoldingRegistersResponse(request,
                                                            await WriteAsyncData(request));

                //Counters.ReadHoldingRegisters.IncResponses_OK();

            }
            catch (Exception e)
            {
                //Counters.ReadHoldingRegisters.IncResponses_Error();
            }

            response ??= new ReadHoldingRegistersResponse(request,
                                                          request.TransactionId,
                                                          Array.Empty<UInt16>(),
                                                          request.ProtocolId,
                                                          request.UnitId);


            #region Send OnReadHoldingRegistersResponse event

            var endtime = Timestamp.Now;

            try
            {

                if (OnReadHoldingRegistersResponse is not null)
                    await Task.WhenAll(OnReadHoldingRegistersResponse.GetInvocationList().
                                       Cast<ReadHoldingRegistersClientResponseDelegate>().
                                       Select(e => e(endtime,
                                                     this,
                                                     RemoteTCPSocket ?? IPSocket.AnyV4(RemoteTCPPort),
                                                     "",
                                                     request,
                                                     response,
                                                     endtime - startTime))).
                                       ConfigureAwait(false);

            }
            catch (Exception e)
            {
                DebugX.LogException(e, nameof(ModbusTCPClient) + "." + nameof(OnReadHoldingRegistersResponse));
            }

            #endregion

            return response;

        }

        #endregion

        #region ReadInputRegisters  (StartingAddress, NumberOfInputRegisters)

        /// <summary>
        /// Read input registers.
        /// </summary>
        /// <param name="StartingAddress">The starting address for reading the data.</param>
        /// <param name="NumberOfInputRegisters">Length of data.</param>
        public async Task<ReadInputRegistersResponse>

            ReadInputRegisters(UInt16  StartingAddress,
                               UInt16  NumberOfInputRegisters)

        {

            var request  = new ReadInputRegistersRequest(this,
                                                         NextInvocationId,
                                                         (UInt16) (StartingAddress + StartingAddressOffset),
                                                         NumberOfInputRegisters,
                                                         UnitAddress);

            var response = new ReadInputRegistersResponse(request,
                                                          await WriteAsyncData(request));

            return response;

        }

        #endregion


        #region WriteSingleCoils         (StartingAddress, OnOff)

        /// <summary>
        /// Write single coil in slave synchronous.
        /// </summary>
        /// <param name="StartingAddress">The starting address for writing the data.</param>
        /// <param name="OnOff">Specifys if the coil should be switched on or off.</param>
        public async Task<Byte[]> WriteSingleCoils(UInt16   StartingAddress,
                                                   Boolean  OnOff)
        {

            var header  = ModbusProtocol.CreateWriteHeader(NextInvocationId,
                                                           StartingAddress,
                                                           1,
                                                           1,
                                                           FunctionCode.WriteSingleCoil);

            if (OnOff == true)
            {
                header.Seek(10, SeekOrigin.Begin);
                header.WriteByte(255);
            }

            var response = await WriteAsyncData(header);

            return response;

        }

        #endregion

        #region WriteMultipleCoils       (StartingAddress, NumberOfBits, Values)

        /// <summary>
        /// Write multiple coils in slave synchronous.
        /// </summary>
        /// <param name="StartingAddress">The starting address for writing the data.</param>
        /// <param name="NumberOfBits">Specifys number of bits.</param>
        /// <param name="Values">Contains the bit information in byte format.</param>
        public async Task<Byte[]> WriteMultipleCoils(UInt16  StartingAddress,
                                                     UInt16  NumberOfBits,
                                                     Byte[]  Values)
        {

            var numberOfBytes  = Convert.ToByte(Values.Length);

            var header         = ModbusProtocol.CreateWriteHeader(
                                     NextInvocationId,
                                     StartingAddress,
                                     NumberOfBits,
                                     (byte) (numberOfBytes + 2),
                                     FunctionCode.WriteMultipleCoils
                                 );

            header.Write(Values,
                         13,
                         numberOfBytes);

            var response = await WriteAsyncData(header);

            return response;

        }

        #endregion

        #region WriteMultipleCoils       (StartingAddress, Coils)

        /// <summary>
        /// Send multiple coils to a unit/device.
        /// </summary>
        /// <param name="StartingAddress">The starting address for writing the data.</param>
        /// <param name="Coils">The array of coils.</param>
        public async Task<ModbusTCPResponse> WriteMultipleCoils(UInt16            StartingAddress,
                                                                params Boolean[]  Coils)
        {

            var numberOfBits   = Coils.Length;
            var numberOfBytes  = Convert.ToByte(Coils.Length);
            var coils          = new Byte[numberOfBytes];
            var bitPosition    = 0;

            for (var i=0; i<Coils.Length; i++)
            {
                if (Coils[i])
                    coils[bitPosition / 8] |= (Byte) (1 << (bitPosition % 8));
            }

            var header         = ModbusProtocol.CreateWriteHeader(
                                     NextInvocationId,
                                     StartingAddress,
                                     (UInt16) numberOfBits,
                                     (Byte)  (numberOfBytes + 2),
                                     FunctionCode.WriteMultipleCoils
                                 );

            header.Write(coils,
                         13,
                         numberOfBytes);


            var response = new ModbusTCPResponse(null,
                                                 null,
                                                 await WriteAsyncData(header));

            return response;

        }

        #endregion

        #region WriteSingleRegister      (StartingAddress, Values)

        /// <summary>
        /// Write single register in slave synchronous.
        /// </summary>
        /// <param name="StartingAddress">Address to where the data is written.</param>
        /// <param name="Values">Contains the register information.</param>
        public async Task<Byte[]> WriteSingleRegister(UInt16  StartingAddress,
                                                      Byte[]  Values)
        {

            var header    = ModbusProtocol.CreateWriteHeader(
                                NextInvocationId,
                                StartingAddress,
                                1,
                                1,
                                FunctionCode.WriteSingleRegister
                            );

            header.Write(Values,
                         10,
                         2);

            var response  = await WriteAsyncData(header);

            return response;

        }

        #endregion

        #region WriteMultipleRegister    (StartingAddress, Values)

        /// <summary>
        /// Write multiple registers in slave synchronous.
        /// </summary>
        /// <param name="StartingAddress">Address to where the data is written.</param>
        /// <param name="Values">Contains the register information.</param>
        public async Task<Byte[]> WriteMultipleRegister(UInt16  StartingAddress,
                                                        Byte[]  Values)
        {

            var numBytes = Convert.ToUInt16(Values.Length);

            if (numBytes % 2 > 0)
                numBytes++;

            var header    = ModbusProtocol.CreateWriteHeader(
                                NextInvocationId,
                                StartingAddress,
                                Convert.ToUInt16(numBytes / 2),
                                Convert.ToByte  (numBytes + 2),
                                FunctionCode.WriteMultipleRegister
                            );

            header.Write(Values,
                         13);

            var response  = await WriteAsyncData(header);

            return response;

        }

        #endregion

        #region ReadWriteMultipleRegister(StartingAddress, Values)

        /// <summary>
        /// Read/Write multiple registers in slave synchronous. The result is given in the response function.
        /// </summary>
        /// <param name="StartReadAddress">Address from where the data read begins.</param>
        /// <param name="NumberOfInputs">Length of data.</param>
        /// <param name="StartWriteAddress">Address to where the data is written.</param>
        /// <param name="Values">Contains the register information.</param>
        public async Task<Byte[]> ReadWriteMultipleRegister(UInt16  StartReadAddress,
                                                            UInt16  NumberOfInputs,
                                                            UInt16  StartWriteAddress,
                                                            Byte[]  Values)
        {

            var numberOfBytes  = Convert.ToUInt16(Values.Length);
            if (numberOfBytes % 2 > 0) numberOfBytes++;

            var header         = ModbusProtocol.CreateReadWriteHeader(
                                     NextInvocationId,
                                     StartReadAddress,
                                     NumberOfInputs,
                                     StartWriteAddress,
                                     Convert.ToUInt16(numberOfBytes / 2)
                                 );

            header.Write(Values,
                         17);

            var response       = await WriteAsyncData(header);

            return response;

        }

        #endregion



        // Read Holding Registers

        #region ReadInt16(StartingAddress, out Value)

        public Boolean ReadInt16(UInt16 StartingAddress, out Int16 Value)
        {
            return TryRead(StartingAddress, 1, out Value, array => BitConverter.ToInt16(array.Reverse(3, 2), 0));
        }

        #endregion

        #region ReadInt32(StartingAddress, out Value)

        public Boolean ReadInt32(UInt16 StartingAddress, out Int32 Value)
        {
            return TryRead(StartingAddress, 2, out Value, array => BitConverter.ToInt32(array.Reverse(3, 4), 0));
        }

        #endregion


        #region TryReadSingle(StartingAddress, out Value)

        public Boolean TryReadSingle(UInt16 StartingAddress, out Single Value)
        {
            return TryRead(StartingAddress, 2, out Value, array => BitConverter.ToSingle(array.Reverse(3, 4), 0));
        }

        #endregion

        #region ReadSingle(StartingAddress)

        public Single ReadSingle(UInt16 StartingAddress)
        {
            return Read<Single>(StartingAddress, 2, array => BitConverter.ToSingle(array.Reverse(3, 4), 0));
        }

        #endregion

        #region TryReadSingles(StartingAddress, Num, out Value)

        public Boolean TryReadSingles(UInt16 StartingAddress, Int32 Num, out Single[] Values)
        {
            return TryRead(StartingAddress, (ushort)(2*Num), out Values, array => MultiConverters.NetworkBytesToHostSingle(array));
        }

        #endregion

        #region ReadSingles(StartingAddress, Num)

        public Single[] ReadSingles(UInt16 StartingAddress, Int32 Num)
        {
            return Read<Single[]>(StartingAddress, (ushort)(2 * Num), array => MultiConverters.NetworkBytesToHostSingle(array));
        }

        #endregion


        #region TryReadDateTime32(StartingAddress, out Value)

        public Boolean TryReadDateTime32(UInt16 StartingAddress, out DateTime Value)
        {
            return TryRead(StartingAddress, 2, out Value, array => ByteExtensions.UNIXTime.AddSeconds(BitConverter.ToInt32(array.Reverse(3, 4), 0)), OnError: DateTime.MinValue);
        }

        #endregion

        #region ReadDateTime32(StartingAddress)

        public DateTime ReadDateTime32(UInt16 StartingAddress)
        {
            return Read<DateTime>(StartingAddress, 2, array => ByteExtensions.UNIXTime.AddSeconds(BitConverter.ToInt32(array.Reverse(3, 4), 0)), OnError: DateTime.MinValue);
        }

        #endregion

        #region ReadDateTime64(StartingAddress, out Value)

        public Boolean ReadDateTime64(UInt16 StartingAddress, out DateTime Value)
        {
            return TryRead(StartingAddress, 4, out Value, array => ByteExtensions.UNIXTime.AddSeconds(BitConverter.ToInt64(array.Reverse(3, 8), 0)), OnError: DateTime.MinValue);
        }

        #endregion


        #region TryReadString(StartingAddress, NumberOfRegisters, out Value)

        public Boolean TryReadString(UInt16       StartingAddress,
                                     UInt16       NumberOfRegisters,
                                     out String?  Text)
        {

            var response = ReadHoldingRegisters(StartingAddress, NumberOfRegisters).Result;

            Text = Encoding.UTF8.GetString(response.EntirePDU.Skip(9).TakeWhile(b => b != 0x00).ToArray());

            return Text.Length > 0;


            return TryRead(StartingAddress,
                           NumberOfRegisters,
                           out Text,
                           array => Encoding.UTF8.GetString(array.Skip(3).TakeWhile(b => b != 0x00).ToArray()));

        }

        #endregion


        #region TryRead<T>(StartingAddress, NumberOfRegisters, out Value, BitConverter, OnError = default)

        public Boolean TryRead<T>(UInt16           StartingAddress,
                                  UInt16           NumberOfRegisters,
                                  out T?           Value,
                                  Func<Byte[], T>  BitConverter,
                                  T?               OnError   = default)
        {

            if (GetByteArray(StartingAddress,
                             NumberOfRegisters,
                             out var byteArray))
            {
                Value = BitConverter(byteArray);
                return true;
            }

            Value = OnError;
            return false;

        }

        #endregion

        #region Read<T>   (StartingAddress, NumberOfRegisters,            BitConverter, OnError = default)

        public T? Read<T>(UInt16           StartingAddress,
                          UInt16           NumberOfRegisters,
                          Func<Byte[], T>  BitConverter,
                          T?               OnError   = default)
        {

            if (TryRead(StartingAddress,
                     NumberOfRegisters,
                     out var value,
                     BitConverter,
                     OnError))
            {
                return value;
            }

            return OnError;

        }

        #endregion


        #region GetByteArray(StartingAddress, NumberOfRegisters, out ByteArray)

        public Boolean GetByteArray(UInt16 StartingAddress, UInt16 NumberOfRegisters, out Byte[] ByteArray)
        {

            return GetByteArray(new ModbusPacket(NumberOfRegisters,
                                                 UnitAddress,
                                                 FunctionCode.ReadHoldingRegisters,
                                                 StartingAddress),
                                out ByteArray);

        }

        #endregion

        #region GetByteArray(ModbusPacket, out ByteArray)

        public Boolean GetByteArray(ModbusPacket ModbusPacket, out Byte[] ByteArray)
        {

            ByteArray = Array.Empty<Byte>();
            Boolean _CRC = false;
            var retries = 0;

            do
            {

                //if (retries > 0)
                //{
                //    if (SerialPort != null)
                //    {
                //        SerialPort.Close();
                //        SerialPort.Open();
                //    }
                //    Debug.Print("Retry: " + retries);
                //}

                //ByteArray = RequestDelegate(ModbusPacket);
                //if (SerialPort != null)
                //    _CRC = CRC16.CheckCRC16(ByteArray);
                //else
                //    _CRC = true;
                //retries++;

            } while (!_CRC || retries > 5);

            if (_CRC)
                return true;

            ByteArray = Array.Empty<Byte>();
            return false;

        }

        #endregion





        // Write asynchronous data
        private async Task<Byte[]> WriteAsyncData(MemoryStream write_data)
        {

            //if ((tcpAsyCl != null) && (tcpAsyCl.Connected))
            //{
            //    try
            //    {
            //        tcpAsyCl.BeginSend(write_data, 0, write_data.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
            //        tcpAsyCl.BeginReceive(tcpAsyClBuffer, 0, tcpAsyClBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), tcpAsyCl);
            //    }
            //    catch (SystemException)
            //    {
            //        CallException(id, write_data[7], excExceptionConnectionLost);
            //    }
            //}
            //else CallException(id, write_data[7], excExceptionConnectionLost);

            await Task.Delay(500);

            return new Byte[0];

        }

        private async Task<Byte[]> WriteAsyncData(ModbusTCPRequest Request)
        {

            var sw = new Stopwatch();

            if (TCPSocket is null)
            {

                TCPSocket = new Socket(AddressFamily.InterNetwork,
                                       SocketType.   Stream,
                                       ProtocolType. Tcp) {

                    SendTimeout    = (Int32) RequestTimeout.TotalMilliseconds,
                    ReceiveTimeout = (Int32) RequestTimeout.TotalMilliseconds

                };

                TCPSocket.Connect(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(RemoteIPAddress.ToString()),
                                                            RemoteTCPPort.ToInt32()));

            }

            if (TCPSocket is not null && TCPStream is null)
            {

                TCPStream = new MyNetworkStream(TCPSocket, true) {
                    ReadTimeout = (Int32) RequestTimeout.TotalMilliseconds
                };

                Request.LocalSocket   = IPSocket.FromIPEndPoint(TCPSocket.LocalEndPoint)  ?? IPSocket.Zero;
                Request.RemoteSocket  = IPSocket.FromIPEndPoint(TCPSocket.RemoteEndPoint) ?? IPSocket.Zero;

            }

            #region Create TCP connection (possibly also do DNS lookups)

            //Boolean restart;

            //do
            //{

            //    restart = false;

            //    if (TCPSocket is null)
            //    {

            //        System.Net.IPEndPoint? _FinalIPEndPoint = null;

            //        if (RemoteIPAddress is null)
            //        {

            //            #region Localhost

            //            if (IPAddress.IsIPv4Localhost(RemoteURL.Hostname))
            //                RemoteIPAddress = IPv4Address.Localhost;

            //            else if (IPAddress.IsIPv6Localhost(RemoteURL.Hostname))
            //                RemoteIPAddress = IPv6Address.Localhost;

            //            else if (IPAddress.IsIPv4(RemoteURL.Hostname.Name))
            //                RemoteIPAddress = IPv4Address.Parse(RemoteURL.Hostname.Name);

            //            else if (IPAddress.IsIPv6(RemoteURL.Hostname.Name))
            //                RemoteIPAddress = IPv6Address.Parse(RemoteURL.Hostname.Name);

            //            #endregion

            //            #region DNS lookup...

            //            if (RemoteIPAddress is null)
            //            {

            //                var IPv4AddressLookupTask = DNSClient.
            //                                                Query<A>(RemoteURL.Hostname.Name).
            //                                                ContinueWith(query => query.Result.Select(ARecord    => ARecord.IPv4Address));

            //                var IPv6AddressLookupTask = DNSClient.
            //                                                Query<AAAA>(RemoteURL.Hostname.Name).
            //                                                ContinueWith(query => query.Result.Select(AAAARecord => AAAARecord.IPv6Address));

            //                await Task.WhenAll(IPv4AddressLookupTask,
            //                                    IPv6AddressLookupTask).
            //                            ConfigureAwait(false);

            //                if (PreferIPv4)
            //                {
            //                    if (IPv6AddressLookupTask.Result.Any())
            //                        RemoteIPAddress = IPv6AddressLookupTask.Result.First();

            //                    if (IPv4AddressLookupTask.Result.Any())
            //                        RemoteIPAddress = IPv4AddressLookupTask.Result.First();
            //                }
            //                else
            //                {
            //                    if (IPv4AddressLookupTask.Result.Any())
            //                        RemoteIPAddress = IPv4AddressLookupTask.Result.First();

            //                    if (IPv6AddressLookupTask.Result.Any())
            //                        RemoteIPAddress = IPv6AddressLookupTask.Result.First();
            //                }

            //            }

            //            #endregion

            //        }

            //        if (RemoteIPAddress is not null && RemotePort is not null)
            //            _FinalIPEndPoint = new System.Net.IPEndPoint(new System.Net.IPAddress(RemoteIPAddress.GetBytes()),
            //                                                            RemotePort.Value.ToInt32());
            //        else
            //            throw new Exception("DNS lookup failed!");


            //        sw.Start();

            //        //TCPClient = new TcpClient();
            //        //TCPClient.Connect(_FinalIPEndPoint);
            //        //TCPClient.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

            //        if (RemoteIPAddress.IsIPv4)
            //            TCPSocket = new Socket(AddressFamily.InterNetwork,
            //                                    SocketType.Stream,
            //                                    ProtocolType.Tcp);

            //        if (RemoteIPAddress.IsIPv6)
            //            TCPSocket = new Socket(AddressFamily.InterNetworkV6,
            //                                    SocketType.Stream,
            //                                    ProtocolType.Tcp);

            //        if (TCPSocket is not null)
            //        {
            //            TCPSocket.SendTimeout    = (Int32) RequestTimeout.Value.TotalMilliseconds;
            //            TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
            //            TCPSocket.Connect(_FinalIPEndPoint);
            //            TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;
            //        }

            //    }

            //    TCPStream = TCPSocket is not null
            //                    ? new MyNetworkStream(TCPSocket, true) {
            //                            ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds
            //                        }
            //                    : null;

                #endregion

            #region Create (Crypto-)Stream

                //    if (RemoteURL.Protocol == URLProtocols.https &&
                //        RemoteCertificateValidator is not null   &&
                //        TCPStream                  is not null)
                //    {

                //        if (TLSStream is null)
                //        {

                //            TLSStream = new SslStream(TCPStream,
                //                                      false,
                //                                      RemoteCertificateValidator,
                //                                      ClientCertificateSelector,
                //                                      EncryptionPolicy.RequireEncryption)
                //            {

                //                ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds

                //            };

                //            HTTPStream = TLSStream;

                //            try
                //            {

                //                await TLSStream.AuthenticateAsClientAsync(RemoteURL.Hostname.Name,
                //                                                          null,  // new X509CertificateCollection(new X509Certificate[] { ClientCert })
                //                                                          this.TLSProtocol,
                //                                                          false);// true);

                //            }
                //            catch
                //            {
                //                TCPSocket = null;
                //                restart   = true;
                //            }

                //        }

                //    }

                //    else
                //    {
                //        TLSStream  = null;
                //        HTTPStream = TCPStream;
                //    }

                //    HTTPStream.ReadTimeout = (Int32) RequestTimeout.Value.TotalMilliseconds;

                //}
                //while (restart);

                #endregion


            TCPStream?.Write(Request.EntirePDU);


            #region Wait timeout for the server to react!


            while (!TCPStream.DataAvailable)
            {

                if (sw.ElapsedMilliseconds >= RequestTimeout.TotalMilliseconds)
                    throw new HTTPTimeoutException(sw.Elapsed);

                Thread.Sleep(1);

            }

            //DebugX.LogT("ModbusTCP Client (" + Request.HTTPMethod + " " + Request.URL + ") got first response after " + sw.ElapsedMilliseconds + "ms!");

            #endregion


            HTTPStream = TCPStream;

            var responseBuffer = new Byte[65536];

            //do
            //{

                var bytesRead = await HTTPStream.ReadAsync(responseBuffer);

            //} while (HeaderEndsAt == 0 &&
            //             sw.ElapsedMilliseconds < HTTPStream.ReadTimeout);


            Array.Resize(ref responseBuffer,
                         bytesRead);


            //if (tcpSynCl.Connected)
            //{
            //    try
            //    {
            //        tcpSynCl.Send(write_data, 0, write_data.Length, SocketFlags.None);
            //        int result = tcpSynCl.Receive(tcpSynClBuffer, 0, tcpSynClBuffer.Length, SocketFlags.None);

            //        byte function = tcpSynClBuffer[7];
            //        byte[] data;

            //        if (result == 0) CallException(id, write_data[7], excExceptionConnectionLost);

            //        // ------------------------------------------------------------
            //        // Response data is slave exception
            //        if (function > excExceptionOffset)
            //        {
            //            function -= excExceptionOffset;
            //            CallException(id, function, tcpSynClBuffer[8]);
            //            return null;
            //        }
            //        // ------------------------------------------------------------
            //        // Write response data
            //        else if ((function >= fctWriteSingleCoil) && (function != fctReadWriteMultipleRegister))
            //        {
            //            data = new byte[2];
            //            Array.Copy(tcpSynClBuffer, 10, data, 0, 2);
            //        }
            //        // ------------------------------------------------------------
            //        // Read response data
            //        else
            //        {
            //            data = new byte[tcpSynClBuffer[8]];
            //            Array.Copy(tcpSynClBuffer, 9, data, 0, tcpSynClBuffer[8]);
            //        }
            //        return data;
            //    }
            //    catch (SystemException)
            //    {
            //        CallException(id, write_data[7], excExceptionConnectionLost);
            //    }
            //}
            //else CallException(id, write_data[7], excExceptionConnectionLost);

            await Task.Delay(500);

            return responseBuffer;

        }



        public void Close()
        {
            TCPStream?.Close();
            TCPSocket?.Close();
        }


        public void Dispose()
        {

            TCPStream?.Close();
            TCPSocket?.Close();

            TCPStream?.Dispose();
            TCPSocket?.Dispose();

        }


    }

}
