using System;
using System.Net;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using System.Diagnostics;
using System.Management;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    public class DNSQuery
    {

        #region Data

        public String                    DomainName;
        public Boolean                   RecursionDesired { get; set; }

        public DNSResourceRecordTypes[]  QueryTypes;
        public DNSQueryClasses           QueryClass;

        #endregion

        #region Constructor(s)

        #region DNSQuery(DomainName)

        public DNSQuery(String DomainName)
            : this(DomainName, DNSResourceRecordTypes.A)
        { }

        #endregion

        #region DNSQuery(DomainName, params DNSResourceRecordType)

        public DNSQuery(String                           DomainName,
                        params DNSResourceRecordTypes[]  DNSResourceRecordType)
        {

            if (DNSResourceRecordType == null || DNSResourceRecordType.Length == 0)
                QueryTypes = new DNSResourceRecordTypes[1] { DNSResourceRecordTypes.A };

            else
                QueryTypes = DNSResourceRecordType;

            if (QueryTypes.Length > 2305) // Just because of the numbers ;)
                throw new ArgumentException("Too many DNSResourceRecordTypes!");

            this.DomainName        = DomainName;
            this.RecursionDesired  = true;
            this.QueryClass        = DNSQueryClasses.IN;

        }

        #endregion

        #endregion


        #region Serialize()

        public Byte[] Serialize()
        {

            var DNSPacket = new Byte[512];

            #region DNS Query Packet Header

            var TransactionId = new Random().Next(55555);

            // TransactionId (2 Bytes)
            DNSPacket[ 0] = (Byte) (TransactionId >> 8);
            DNSPacket[ 1] = (Byte) (TransactionId & Byte.MaxValue);

            // Flags (2 Bytes)
            DNSPacket[ 2] = 0x00; // Set OpCode to Regular Query

            if (RecursionDesired)
                DNSPacket[ 2] |= 1;

            DNSPacket[ 3] = 0x00;

            // Number of queries (2 Bytes)
            DNSPacket[ 4] = (Byte) (QueryTypes.Length >> 8);
            DNSPacket[ 5] = (Byte) (QueryTypes.Length & Byte.MaxValue);

            // Number of answer resource records (2 Bytes)
            DNSPacket[ 6] = 0x00;
            DNSPacket[ 7] = 0x00;

            // Number of authority resource records (2 Bytes)
            DNSPacket[ 8] = 0x00;
            DNSPacket[ 9] = 0x00;

            // Number of additional resource records (2 Bytes)
            DNSPacket[10] = 0x00;
            DNSPacket[11] = 0x00;

            var PacketPosition = 12;

            #endregion

            #region Fill Question Section

            foreach (var QueryType in QueryTypes)
            {

                foreach (var DomainNameTokens in DomainName.Split(new Char[] { '.' }))
                {

                    // Set Length label for domainname segment
                    DNSPacket[PacketPosition++] = (Byte) (DomainNameTokens.Length & Byte.MaxValue);

                    foreach (var Char in Encoding.ASCII.GetBytes(DomainNameTokens))
                        DNSPacket[PacketPosition++] = Char;

                }

                // End-of-DomainName marker
                DNSPacket[PacketPosition++] = 0x00;

                // Set Query type
                DNSPacket[PacketPosition++] = (Byte) 0;
                DNSPacket[PacketPosition++] = (Byte) QueryType;

                // Set Query class
                DNSPacket[PacketPosition++] = (Byte) 0;
                DNSPacket[PacketPosition++] = (Byte) QueryClass;

            }

            #endregion

            Array.Resize(ref DNSPacket, PacketPosition);

            return DNSPacket;

        }

        #endregion

    }

}
