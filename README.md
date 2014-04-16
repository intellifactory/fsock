# FSock

Simple API for communicating async socket clients and servers.

FSock implements an API for creating async socket clients and servers
that can exchange raw bytes or binary messages.  Imagined as a
lightweight managed async zeromq replacement focusing on TCP sockets.
Strives to be portable to Mono and usable from both F# and C#.

**Status**: experimental.

**Good for**: portable inter-process communication on a network you
control, working with sockets without incurring large overheads such
as supsended heavy-weight CLR threads.

**Not (yet) good for**: public-facing services exposed to a hostile
environment, such as web servers.  For that need to implement resource
and time limits, as well as more testing/validation of how Socket
class is being used.

* NuGet package: [FSock](https://www.nuget.org/packages/FSock)
* API:
  * [FSock.fsi](src/FSock/FSock.fsi) - main API
  * [Sockets.fsi](src/FSock/Sockets.fsi) - socket details
  * [Definitions.fsi](src/FSock/Definitions.fsi) - channels, futures, etc

## Example

See [FSock.fsx](tests/FSock.fsx) for an example.
