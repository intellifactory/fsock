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

[<Sealed>]
type Connection =
    interface IDisposable
    member AsyncReceiveMessage : unit -> Async<byte[]>
    member AsyncSendMessage : byte[] -> Async<unit>
    member Close : unit -> unit
    member Closed : Future<Exit>
    member Custodian : Custodian
    member InputChannel : InputChannel
    member OutputChannel : OutputChannel
    member ReceiveMessage : unit -> Task<byte[]>
    member SendMessage : byte[] -> Task

[<Sealed>]
type ServerConfig =
    new : unit -> ServerConfig
    member Address : string with get, set
    member Backlog : int with get, set
    member IPAddress : IPAddress with get, set
    member IPEndPoint : IPEndPoint with get, set
    member OnConnect : Action<Connection> with get, set
    member Port : int with get, set
    member Report : Action<exn> with get, set

[<Sealed>]
type Server =
    interface IDisposable
    member Stop : unit -> unit
    static member Start : ServerConfig -> Server
    static member Start : Action<ServerConfig> -> Server

[<Sealed>]
type ClientConfig =
    new : unit -> ClientConfig
    member Address : string with get, set
    member IPAddress : IPAddress with get, set
    member IPEndPoint : IPEndPoint with get, set
    member Port : int with get, set
    member Report : Action<exn> with get, set
    member SocketPool : option<SocketPool> with get, set

[<Sealed>]
type Client =
    interface IDisposable
    member Connection : Connection
    static member AsyncConnect : ClientConfig -> Async<Client>
    static member AsyncConnect : (ClientConfig -> unit) -> Async<Client>
    static member Connect : ClientConfig -> Task<Client>
    static member Connect : Action<ClientConfig> -> Task<Client>

