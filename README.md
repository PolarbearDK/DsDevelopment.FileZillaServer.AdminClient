# DsDevelopment.FileZillaServer.AdminClient

.NET client for the administration interface of **FileZilla Server 1.x**. Use it to automate user and group management. It does the same job as the users and groups pages of the FileZilla Server administration UI.

For FileZilla Server 0.9.x, use [Miracle.FileZilla.Api](https://github.com/PolarbearDK/Miracle.FileZilla.Api) instead.

This project is not affiliated with the FileZilla project.

> **Preview.** The library currently supports connecting, logging in, and getting or setting groups and users.

## Supported server versions

FileZilla Server only accepts admin clients that use exactly the same admin protocol version as the server, and FileZilla changes that version without notice. Each release of this library therefore supports specific server versions:

| Library | Admin protocol | FileZilla Server |
|---------|----------------|------------------|
| 0.1.x   | 70             | 1.12.x (tested with 1.12.6) |

If the server uses a different protocol version, `Connect` throws a `LoginFailedException` that says so.

## Usage

The admin interface listens on port 14148 and uses TLS with a self-signed certificate. The client only accepts that certificate if you give it the certificate's SHA-256 fingerprint. You can find the fingerprint in the server's administration settings.

```csharp
using DsDevelopment.FileZillaServer.AdminClient;
using DsDevelopment.FileZillaServer.AdminClient.Models;

var options = new FileZillaAdminClientOptions
{
    Host = "127.0.0.1",
    Port = 14148,
    CertificateFingerprint = "9d:2e:bd:36:...", // SHA-256, from the server's admin settings
};

using var client = new FileZillaAdminClient(options);
client.Connect("admin password");

var data = client.GetGroupsAndUsers();

var user = new User
{
    Name = "alice",
    Groups = { "Customers" },               // must be an existing group
    MountPoints =
    {
        new MountPoint { VirtualPath = "/", NativePath = @"C:\Ftp\alice", Access = MountAccess.ReadWrite, Flags = MountFlags.AutoCreate },
    },
};
user.SetPassword("secret");                 // PBKDF2, like the server's own UI
data.Users.Add(user);

// Replaces ALL groups and users. save: true also writes the server's configuration file.
client.SetGroupsAndUsers(data, save: true);
```

Groups work the same way through `data.Groups`.

## Building

```powershell
dotnet build
dotnet test                                    # offline tests
```

Versions come from git tags via [GitVersion](https://gitversion.net/), so no version is set in the project file.

The live tests run against a real server, only when these environment variables are set:

```powershell
$env:FZ_ADMIN_PASSWORD='...'
$env:FZ_ADMIN_FINGERPRINT='..:..'
# optional: $env:FZ_ADMIN_HOST, $env:FZ_ADMIN_PORT
dotnet test --filter Category=LiveServer
```

These tests make changes with `save: false`, so they never touch the server's configuration file, and they revert everything they change.

## License

MIT
