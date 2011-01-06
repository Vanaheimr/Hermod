using System;

namespace de.ahzf.Hermod.Datastructures
{

    public interface IIPAddress
    {

        //int CompareTo(global::de.ahzf.Hermod.Sockets.IPv6Address myIPAddress);
        //int CompareTo(object myObject);
        //bool Equals(global::de.ahzf.Hermod.Sockets.IPv6Address myIPAddress);
        //bool Equals(object myObject);

        Byte   Length { get; }

        Byte[] GetBytes();
        Int32  GetHashCode();
        String ToString();

    }

}
