// $begin{copyright}
//
// Copyright (c) 2008-2014 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace FSock

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

/// Represents a socket connection.
/// This class provides symmetry between clients and servers.
[<Sealed>]
type Connection =

    /// Closes the connection and waits for a graceful or otherwise disconnect.
    member AsyncClose : unit -> Async<unit>

    /// Receives a length-prefixed binary message.
    /// You can also use InputChannel for more direct byte-level access.
    member AsyncReceiveMessage : unit -> Async<option<byte[]>>

    /// Sends a length-prefixed binary message.
    /// You can also use OutputChannel for more direct byte-level access.
    member AsyncSendMessage : byte[] -> Async<unit>

    /// Wraps a thread with finalization, making sure that AsyncClose is called
    /// after either normal or error termination. This is similar to the IDisposable
    /// pattern, however that is not practical since connection closure is async.
    member AsyncWrap : Async<'T> -> Async<'T>

    /// Task form of AsyncClose.
    member Close : unit -> Task

    /// Reports an exception.
    member Report : exn -> unit

    /// Task form of AsyncReceiveMessage.
    member ReceiveMessage : unit -> Task<option<byte[]>>

    /// Task form of SendMessage.
    member SendMessage : byte[] -> Task

    /// Terminate immediately, closing and releasing the socket.
    member Terminate : unit -> unit

    /// A future that arrives when the connetion is closed.
    member Closed : Future<unit>

    /// Raw asynchronous input channel.
    member InputChannel : InputChannel

    /// Raw asynchronous output channel.
    member OutputChannel : OutputChannel

/// Configures the Server.
[<Sealed>]
type ServerConfig =

    /// Creates the default config.
    new : unit -> ServerConfig

    /// IP address name, a lens to IPAddress.
    member Address : string with get, set

    /// Backlog socket parameter.
    member Backlog : int with get, set

    /// IP address.
    member IPAddress : IPAddress with get, set

    /// Endpoint to listen on.
    member IPEndPoint : IPEndPoint with get, set

    /// Connection handler.
    member OnConnect : Action<Connection> with get, set

    /// Port, a lens into IPEndPoint.
    member Port : int with get, set

    /// Exception reporter.
    member Report : Action<exn> with get, set

/// Socket servers.
[<Sealed>]
type Server =

    /// Stops and awaits graceful closure.
    member AsyncStop : unit -> Async<unit>

    /// Similar to Connection.AsyncWrap.
    member AsyncWrap : Async<'T> -> Async<'T>

    /// Task form of AsyncStop.
    member Stop : unit -> Task

    /// Starts a server with a given configuration.
    static member Start : ServerConfig -> Server

    /// Fluent version of Start.
    static member Start : Action<ServerConfig> -> Server

/// Configures the Client.
[<Sealed>]
type ClientConfig =

    /// Creates the default config.
    new : unit -> ClientConfig

    /// IP address to connect to, lens to IPEndPoint.
    member Address : string with get, set

    /// Typed IP address to connect to, lens to IPEndPoint.
    member IPAddress : IPAddress with get, set

    /// Endpoint to connect to.
    member IPEndPoint : IPEndPoint with get, set

    /// Port to connect to, lens to IPEndPoint.
    member Port : int with get, set

    /// Exception reporter.
    member Report : Action<exn> with get, set

    /// Socket pool to reuse (or none, to create a new one).
    member SocketPool : option<SocketPool> with get, set

/// Socket clients.
[<Sealed>]
type Client =

    /// The connection to the server.
    member Connection : Connection

    /// Establishes a connection.
    static member AsyncConnect : ClientConfig -> Async<Client>

    /// Fluent form of AsyncConnect.
    static member AsyncConnect : (ClientConfig -> unit) -> Async<Client>

    /// Task form of AsyncConnect.
    static member Connect : ClientConfig -> Task<Client>

    /// Fluent task form of AsyncConnect.
    static member Connect : Action<ClientConfig> -> Task<Client>
