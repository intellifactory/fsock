# FsNuGet

Simple API for communicating async socket clients and servers.

FSock implements an API for creating async socket clients and servers
that can exchange raw bytes or binary messages.  Imagined as a
lightweight managed async zeromq replacement focusing on TCP sockets.
Strives to be portable to Mono and usable from both F# and C#.

* NuGet package: [FSock](https://www.nuget.org/packages/FSock)
* API:
** [FSock.fsi](src/FSock/FSock.fsi) - main API
** [Sockets.fsi](src/FSock/Sockets.fsi) - socket details
** [Definitions.fsi](src/FSock/Sockets.fsi) - channels, futures, etc
