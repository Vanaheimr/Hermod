/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

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
        /// The default remote TCP port to connect to.
        /// </summary>
        public static readonly IPPort    DefaultRemotePort               = IPPort.Parse(502);

        /// <summary>
        /// The default remote TLS port to connect to.
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
        /// The remote URL of the Modbus device to connect to.
        /// </summary>
        public URL                                   RemoteURL                     { get; }

        /// <summary>
        /// An optional description of this Modbus/TCP client.
        /// </summary>
        public String?                               Description                   { get; set; }

        /// <summary>
        /// The remote SSL/TLS certificate validator.
        /// </summary>
        public RemoteCertificateValidationCallback?  RemoteCertificateValidator    { get; }

        /// <summary>
        /// A delegate to select a TLS client certificate.
        /// </summary>
        public LocalCertificateSelectionCallback?    ClientCertificateSelector     { get; }

        /// <summary>
        /// The SSL/TLS client certificate to use of HTTP authentication.
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

        /// <summary>
        /// The IP Address to connect to.
        /// </summary>
        public IIPAddress?                           RemoteIPAddress               { get; private set; }

        /// <summary>
        /// The TCP port to connect to.
        /// </summary>
        public IPPort?                               RemotePort                    { get; private set; }


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

        #region Constructor(s)

        #region ModbusTCPMaster(RemoteURL, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the Modbus/TCP device to connect to.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(URL                                   RemoteURL,
                               String?                               Description                  = null,
                               RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining                = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)
        {

            this.RemoteURL                   = RemoteURL;
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

            this.RemotePort                  = RemoteURL.Port         ?? (RemoteURL.Protocol == URLProtocols.modbus
                                                                             ? DefaultRemotePort
                                                                             : DefaultSecurePort);

            if (this.ClientCertificateSelector is null && this.ClientCert is not null)
                this.ClientCertificateSelector = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => this.ClientCert;

            this.internalInvocationId        = 15;

        }

        #endregion

        #region ModbusTCPMaster(RemoteIPAddress, RemotePort = null, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteIPAddress">The remote IP address to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(IIPAddress                            RemoteIPAddress,
                               IPPort?                               RemotePort                   = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining                = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

            : this(URL.Parse("http://" + RemoteIPAddress + (RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : "")),
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

        #region ModbusTCPMaster(RemoteSocket, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteSocket">The remote IP socket to connect to.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(IPSocket                              RemoteSocket,
                               String?                               Description                  = null,
                               RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining                = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

            : this(URL.Parse("http://" + RemoteSocket.IPAddress + ":" + RemoteSocket.Port),
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

        #region ModbusTCPMaster(RemoteHost, ...)

        /// <summary>
        /// Create a new Modbus/TCP client.
        /// </summary>
        /// <param name="RemoteHost">The remote hostname to connect to.</param>
        /// <param name="RemotePort">An optional remote TCP port to connect to.</param>
        /// <param name="Description">An optional description of this Modbus/TCP client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="ClientCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of Modbus/TLS authentication.</param>
        /// <param name="PreferIPv4">Prefer IPv4 instead of IPv6.</param>
        /// <param name="TLSProtocol">The TLS protocol to use.</param>
        /// <param name="RequestTimeout">An optional request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for Modbus/TCP request.</param>
        /// <param name="UseRequestPipelining">Whether to pipeline multiple Modbus/TCP request through a single TCP/TLS connection.</param>
        /// <param name="Logger">A Modbus/TCP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public ModbusTCPClient(HTTPHostname                          RemoteHost,
                               IPPort?                               RemotePort                   = null,
                               String?                               Description                  = null,
                               RemoteCertificateValidationCallback?  RemoteCertificateValidator   = null,
                               LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                               X509Certificate?                      ClientCert                   = null,
                               SslProtocols?                         TLSProtocol                  = null,
                               Boolean?                              PreferIPv4                   = null,
                               TimeSpan?                             RequestTimeout               = null,
                               TransmissionRetryDelayDelegate?       TransmissionRetryDelay       = null,
                               UInt16?                               MaxNumberOfRetries           = DefaultMaxNumberOfRetries,
                               Boolean                               UseRequestPipelining         = false,
                               ModbusTCPClientLogger?                Logger                       = null,
                               DNSClient?                            DNSClient                    = null)

            : this(URL.Parse("http://" + RemoteHost + (RemotePort.HasValue ? ":" + RemotePort.Value.ToString() : "")),
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


        #region ReadCoils           (StartAddress, NumberOfCoils)

        /// <summary>
        /// Read multiple coils.
        /// </summary>
        /// <param name="StartAddress">The start address for reading data.</param>
        /// <param name="NumberOfCoils">The number of coils to read.</param>
        public async Task<ReadCoilsResponse>

            ReadCoils(UInt16  StartAddress,
                      UInt16  NumberOfCoils)

        {

            var readCoilsRequest  = new ReadCoilsRequest(this,
                                                         NextInvocationId,
                                                         StartAddress,
                                                         NumberOfCoils);

            var response          = new ReadCoilsResponse(readCoilsRequest, await WriteAsyncData(readCoilsRequest));

            return response;

        }

        #endregion

        #region ReadDiscreteInputs  (StartAddress, NumberOfInputs)

        /// <summary>
        /// Read discrete inputs.
        /// </summary>
        /// <param name="StartAddress">The start address for reading the data.</param>
        /// <param name="NumberOfInputs">The number of input inputs to read.</param>
        public async Task<ModbusTCPResponse<ReadDiscreteInputsRequest>>

            ReadDiscreteInputs(UInt16  StartAddress,
                               UInt16  NumberOfInputs)

        {

            var readDiscreteInputsRequest  = new ReadDiscreteInputsRequest(this,
                                                                           NextInvocationId,
                                                                           StartAddress,
                                                                           NumberOfInputs);

            var response                   = await WriteAsyncData(readDiscreteInputsRequest);

            return null; // response;

        }

        #endregion

        #region ReadHoldingRegisters(StartAddress, NumberOfRegisters)

        /// <summary>
        /// Read holding registers.
        /// </summary>
        /// <param name="StartAddress">Address from where the data read begins.</param>
        /// <param name="NumberOfRegisters">The number of input registers to read.</param>
        public async Task<ModbusTCPResponse<ReadHoldingRegistersRequest>>

            ReadHoldingRegisters(UInt16  StartAddress,
                                 UInt16  NumberOfRegisters)

        {

            var readHoldingRegistersRequest  = new ReadHoldingRegistersRequest(this,
                                                                               NextInvocationId,
                                                                               StartAddress,
                                                                               NumberOfRegisters);

            var response                     = await WriteAsyncData(readHoldingRegistersRequest);

            return null; // response;

        }

        #endregion

        #region ReadInputRegisters  (StartAddress, NumberOfInputRegisters)

        /// <summary>
        /// Read input registers.
        /// </summary>
        /// <param name="StartAddress">The start address for reading the data.</param>
        /// <param name="NumberOfInputRegisters">Length of data.</param>
        public async Task<ModbusTCPResponse<ReadInputRegistersRequest>>

            ReadInputRegisters(UInt16  StartAddress,
                               UInt16  NumberOfInputRegisters)

        {

            var readInputRegistersRequest  = new ReadInputRegistersRequest(this,
                                                                           NextInvocationId,
                                                                           StartAddress,
                                                                           NumberOfInputRegisters);

            var response                   = await WriteAsyncData(readInputRegistersRequest);

            return null; // response;

        }

        #endregion


        #region WriteSingleCoils         (StartAddress, OnOff)

        /// <summary>
        /// Write single coil in slave synchronous.
        /// </summary>
        /// <param name="StartAddress">The start address for writing the data.</param>
        /// <param name="OnOff">Specifys if the coil should be switched on or off.</param>
        public async Task<Byte[]> WriteSingleCoils(UInt16   StartAddress,
                                                   Boolean  OnOff)
        {

            var header  = ModbusProtocol.CreateWriteHeader(NextInvocationId,
                                                           StartAddress,
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

        #region WriteMultipleCoils       (StartAddress, NumberOfBits, Values)

        /// <summary>
        /// Write multiple coils in slave synchronous.
        /// </summary>
        /// <param name="StartAddress">The start address for writing the data.</param>
        /// <param name="NumberOfBits">Specifys number of bits.</param>
        /// <param name="Values">Contains the bit information in byte format.</param>
        public async Task<Byte[]> WriteMultipleCoils(UInt16  StartAddress,
                                                     UInt16  NumberOfBits,
                                                     Byte[]  Values)
        {

            var numberOfBytes  = Convert.ToByte(Values.Length);

            var header         = ModbusProtocol.CreateWriteHeader(
                                     NextInvocationId,
                                     StartAddress,
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

        #region WriteMultipleCoils       (StartAddress, Coils)

        /// <summary>
        /// Send multiple coils to a unit/device.
        /// </summary>
        /// <param name="StartAddress">The start address for writing the data.</param>
        /// <param name="Coils">The array of coils.</param>
        public async Task<ModbusTCPResponse> WriteMultipleCoils(UInt16            StartAddress,
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
                                     StartAddress,
                                     (UInt16) numberOfBits,
                                     (Byte)  (numberOfBytes + 2),
                                     FunctionCode.WriteMultipleCoils
                                 );

            header.Write(coils,
                         13,
                         numberOfBytes);


            var response = new ModbusTCPResponse(null,
                                                 await WriteAsyncData(header));

            return response;

        }

        #endregion

        #region WriteSingleRegister      (StartAddress, Values)

        /// <summary>
        /// Write single register in slave synchronous.
        /// </summary>
        /// <param name="StartAddress">Address to where the data is written.</param>
        /// <param name="Values">Contains the register information.</param>
        public async Task<Byte[]> WriteSingleRegister(UInt16  StartAddress,
                                                      Byte[]  Values)
        {

            var header    = ModbusProtocol.CreateWriteHeader(
                                NextInvocationId,
                                StartAddress,
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

        #region WriteMultipleRegister    (StartAddress, Values)

        /// <summary>
        /// Write multiple registers in slave synchronous.
        /// </summary>
        /// <param name="StartAddress">Address to where the data is written.</param>
        /// <param name="Values">Contains the register information.</param>
        public async Task<Byte[]> WriteMultipleRegister(UInt16  StartAddress,
                                                        Byte[]  Values)
        {

            var numBytes = Convert.ToUInt16(Values.Length);

            if (numBytes % 2 > 0)
                numBytes++;

            var header    = ModbusProtocol.CreateWriteHeader(
                                NextInvocationId,
                                StartAddress,
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

        #region ReadWriteMultipleRegister(StartAddress, Values)

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

            var _FinalIPEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,
                                                             502);

            TCPSocket = new Socket(AddressFamily.InterNetwork,
                                   SocketType.   Stream,
                                   ProtocolType. Tcp);

            if (TCPSocket is not null)
            {

                TCPSocket.SendTimeout    = (Int32) RequestTimeout.TotalMilliseconds;
                TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.TotalMilliseconds;
                TCPSocket.Connect(_FinalIPEndPoint);
                TCPSocket.ReceiveTimeout = (Int32) RequestTimeout.TotalMilliseconds;

                TCPStream = new MyNetworkStream(TCPSocket, true) {
                    ReadTimeout = (Int32) RequestTimeout.TotalMilliseconds
                };

                Request.LocalSocket   = IPSocket.FromIPEndPoint(TCPSocket.LocalEndPoint!);
                Request.RemoteSocket  = IPSocket.FromIPEndPoint(TCPSocket.RemoteEndPoint!);

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
                //            catch (Exception)
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

    }

}
