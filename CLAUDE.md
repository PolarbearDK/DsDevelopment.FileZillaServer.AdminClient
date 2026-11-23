# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

NuGet package `DsDevelopment.FileZillaServer.AdminClient`: a .NET client for the **FileZilla Server 1.x** admin protocol, which FileZilla calls RMP. The library targets `netstandard2.0;net10.0` and the tests target `net10.0`. The source of truth is the server's C++ source (`src/server/administration.hpp` and `src/filezilla/serialization/`), downloadable from filezilla-project.org. The 0.9.x library lives in a separate repo, Miracle.FileZilla.Api.

## Commands

```powershell
dotnet build                                   # version from GitVersion.MsBuild (needs git history)
dotnet test                                    # offline tests (fixtures)
dotnet test --filter "FullyQualifiedName~GroupsAndUsersTests"   # subset
$env:FZ_ADMIN_PASSWORD='...'; $env:FZ_ADMIN_FINGERPRINT='..:..'; dotnet test --filter Category=LiveServer
dotnet pack -c Release
```

Versioning is done by GitVersion (`GitVersion.yml`, GitHubFlow). Don't add a `<Version>` to the csproj; release by tagging, e.g. `v0.1.0`.

## Architecture

- **Transport** (`Protocol/AdminConnection.cs`): TLS to port 14148. The certificate is self-signed and pinned by SHA-256 fingerprint. Each frame is `u8 byte-order flag (1 = little-endian), u32 length, u16 message index, body`. The server pushes unrequested messages (log lines, update info), and `Receive` skips them. Index 0 is an exception from the server.
- **Encoding** (`RmpReader`/`RmpWriter`): little-endian integers. Strings and lists use the "shortened size" prefix (1 byte below 251). `std::optional` is a u8 has-value flag followed by the value. `std::variant` and `expected` are an int16 index followed by the value. Hashes and IPs are fixed length with no prefix. Fields must be written in the exact order the C++ `serialize()` lists them, and `optional_nvp` adds no bytes in the binary archive.
- **Version lock**: login sends flavour (u8, 1 = standard) and the protocol version (u32). The server rejects anything but an exact match. `MessageIndex.cs` holds the protocol version (70 = 1.12.x) and message indexes, which are positions in `administration::any_message`. FileZilla renumbers them when it adds messages, so supporting a new server version means re-deriving them from that version's source.
- **Models** (`Models/`): each has internal `Read(RmpReader)` / `Write(RmpWriter)`, and the two must stay symmetric. Name wire values (variant indexes, flags, sizes) as constants rather than commenting literals.

## Tests

`Fixtures/*.bin` are real `get_groups_and_users` responses from 1.12.6. Tests decode them and require re-encoding to be byte-identical. That round-trip is the main safeguard when changing a model. `LiveServerTests` only use `save: false` and revert their changes.
