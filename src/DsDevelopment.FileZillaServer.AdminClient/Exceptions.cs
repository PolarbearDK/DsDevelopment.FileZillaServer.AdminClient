using System;

namespace DsDevelopment.FileZillaServer.AdminClient
{
    /// <summary>Base class for errors reported by this library.</summary>
    public class AdminClientException : Exception
    {
        /// <summary>Creates a new exception.</summary>
        public AdminClientException(string message) : base(message) { }

        /// <summary>Creates a new exception with an inner exception.</summary>
        public AdminClientException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>The server sent data this library could not interpret, or reported a protocol error.</summary>
    public class ProtocolException : AdminClientException
    {
        /// <summary>Creates a new exception.</summary>
        public ProtocolException(string message) : base(message) { }
    }

    /// <summary>Login was rejected: wrong password, or an unsupported server version.</summary>
    public class LoginFailedException : AdminClientException
    {
        /// <summary>Creates a new exception.</summary>
        public LoginFailedException(string message) : base(message) { }
    }

    /// <summary>The server reported that a command failed.</summary>
    public class CommandFailedException : AdminClientException
    {
        /// <summary>Creates a new exception.</summary>
        public CommandFailedException(string message) : base(message) { }
    }
}
