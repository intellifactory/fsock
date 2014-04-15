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
  * [Definitions.fsi](src/FSock/Sockets.fsi) - channels, futures, etc

## Example

```fsharp
let client =
  async {
    use! cli =
      FSock.Client.AsyncConnect(fun cfg ->
        cfg.Report <- fun err -> eprintfn "%O" err
        cfg.Port <- 8091)
    do! cli.Connection.AsyncSendMessage("HELLO"B)
    let! msg = cli.Connection.AsyncReceiveMessage()
    do printfn "Received back: %i bytes" msg.Length
    return ()
  }
  
let server =
  async {
    FSock.Server.Start(fun cfg ->
      cfg.Report <- fun err -> eprintfn "%O" err
      cfg.Port <- 8091
      cfg.OnConnect <- fun conn ->
        async {
          use _ = conn
          let! msg = conn.AsyncReceiveMessage()
          do printfn "RECEIVED: %i bytes" msg.Length 
          do! conn.AsyncSendMessage(msg)
          return ()
        }
        |> Async.Start)  
  }
```
