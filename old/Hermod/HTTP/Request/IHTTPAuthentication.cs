using System;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IHTTPAuthentication
    {

        HTTPAuthenticationTypes  HTTPCredentialType    { get; }

        String                   HTTPText              { get; }

    }

}