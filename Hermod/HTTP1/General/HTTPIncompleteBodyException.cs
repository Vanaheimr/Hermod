namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The HTTP message body ended before the declared Content-Length was received.
    /// </summary>
    public sealed class HTTPIncompleteBodyException(UInt64 ExpectedBytes,
                                                     UInt64 ReceivedBytes)

        : IOException($"The HTTP message body ended after {ReceivedBytes} of {ExpectedBytes} declared bytes.")

    {

        public UInt64 ExpectedBytes { get; } = ExpectedBytes;

        public UInt64 ReceivedBytes { get; } = ReceivedBytes;

    }

}
