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

/// Thrown when socket operations fail.
[<Sealed>]
exception SocketException of SocketError

/// Provides a pool of resources used by the sockets.
/// Especially important for servers. In particular, since Socket
/// instances pin the buffers it uses, it is better to allocate one
/// large byte array in the pool and use segments of it as buffers, than to
/// allocate new buffers per Socket.
[<Sealed>]
type SocketPool =
    interface IDisposable

    /// Constructs a pool with default capacity.
    new : unit -> SocketPool

    /// Constructs a pool with a given capacity (number of simultaneous connections).
    new : capacity: int -> SocketPool

/// Facade for socket programming.
module internal SocketUtility =

    /// Facade for Socket.AsyncAccept.
    val Accept : Socket -> Async<Socket>

    /// Facade for Socket.AsyncConnect.
    val Connect : IPEndPoint -> Async<Socket>

    /// Facade for Socket.AsyncDisconnect.
    val Disconnect : Socket -> Async<unit>

    /// Swallows common cases of SocketException not considered to be error conditions.
    val IgnoreErrors : Async<unit> -> Async<unit>

    /// Constructs a tread that, when started, copies all bytes
    /// received from the Socket to the given OutputChannel.
    val Receive : SocketPool -> Socket -> OutputChannel -> Async<unit>

    /// Constructs a tread that, when started, copies all bytes
    /// from the InputChannel to the Socket.
    val Send : SocketPool -> Socket -> InputChannel -> Async<unit>
