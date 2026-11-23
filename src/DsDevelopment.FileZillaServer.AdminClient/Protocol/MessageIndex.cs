namespace DsDevelopment.FileZillaServer.AdminClient.Protocol
{
    /// <summary>
    /// Wire constants for one specific FileZilla Server admin protocol version.
    /// </summary>
    /// <remarks>
    /// A message's index is its position in <c>administration::any_message</c>
    /// (src/server/administration.hpp in the server sources), where index 0 is <c>any_exception</c>.
    /// FileZilla renumbers messages whenever it changes the list, and bumps <see cref="ProtocolVersion"/>.
    /// Verified against FileZilla Server 1.12.6.
    /// </remarks>
    internal static class MessageIndex
    {
        /// <summary><c>administration::protocol_version</c>. Login fails unless this matches the server exactly.</summary>
        public const uint ProtocolVersion = 70;

        public const ushort Exception = 0;
        public const ushort AdminLogin = 1;
        public const ushort AdminLoginResponse = 2;

        public const ushort GetGroupsAndUsers = 23;
        public const ushort GetGroupsAndUsersResponse = 24;
        public const ushort SetGroupsAndUsers = 25;
        public const ushort SetGroupsAndUsersResponse = 26;
    }
}
