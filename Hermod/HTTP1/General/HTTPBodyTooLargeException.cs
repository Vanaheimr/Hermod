namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The HTTP request body exceeded the configured maximum size.
    /// </summary>
    public sealed class HTTPBodyTooLargeException(UInt64 ReceivedBytes,
                                                  UInt64 MaximumBytes)

        : IOException($"The HTTP request body exceeds the configured maximum of {MaximumBytes} bytes.")

    {

        public UInt64 ReceivedBytes { get; } = ReceivedBytes;

        public UInt64 MaximumBytes { get; } = MaximumBytes;

    }

}
