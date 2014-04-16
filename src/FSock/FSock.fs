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

#nowarn "40"

open System
open System.Collections
open System.Collections.Generic
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

[<Sealed>]
type Connection(socket: Socket, close: unit -> unit, closed: Future<unit>, inp: InputChannel, out: OutputChannel, report: exn -> unit) =

    let asyncClose =
        async {
            do close ()
            return! closed.AsyncAwait()
        }

    member c.AsyncClose() =
        asyncClose

    member c.AsyncWrap(work) =
        Async.Wrap asyncClose work

    member c.AsyncReceiveMessage() =
        Messaging.AsyncReceiveMessage inp

    member c.AsyncSendMessage(msg) =
        Messaging.AsyncSendMessage out msg

    member c.Close() =
        Async.StartAsTask(asyncClose) :> Task

    member c.ReceiveMessage() =
        c.AsyncReceiveMessage()
        |> Async.StartAsTask

    member c.Report(e) =
        report e

    member c.SendMessage(msg) =
        c.AsyncSendMessage(msg)
        |> Async.StartAsTask :> Task

    member c.Terminate() =
        try close (); socket.Close() with :? ObjectDisposedException -> ()

    member c.Closed = closed
    member c.InputChannel = inp
    member c.OutputChannel = out

    static member Create(pool: SocketPool, socket: Socket, report: exn -> unit) =
        let closed = Future.Create()
        let c1 = Channel()
        let c2 = Channel()
        let close () =
            c1.Close()
            c2.Close()
        let ctx : SocketUtility.Context =
            {
                Socket = socket
                SocketPool = pool
                Report = report
            }
        let receiverDone = SocketUtility.ForkReceiver ctx c1.Out
        let senderDone = SocketUtility.ForkSender ctx c2.In
        async {
            try
                use _ = socket
                do! senderDone.AsyncAwait()
                return!
                    SocketUtility.Disconnect socket
                    |> Async.CaptureError report
            with :? ObjectDisposedException ->
                return ()
        }
        |> Async.CaptureError report
        |> Async.StartThread closed
        new Connection(socket, close, closed.Future, c1.In, c2.Out, report)

[<Sealed>]
type ServerConfig() =

    member val Backlog = 32 with get, set
    member val OnConnect = Action<Connection>(ignore) with get, set
    member val IPEndPoint = IPEndPoint(IPAddress.Any, 9912) with get, set
    member val Report = Action<exn>(ignore) with get, set

    member c.Address
        with get () = c.IPAddress.ToString()
        and set x = c.IPAddress <- Dns.GetHostAddresses(x).[0]

    member c.IPAddress
        with get () = c.IPEndPoint.Address
        and set (x: IPAddress) = c.IPEndPoint <- IPEndPoint(x, c.IPEndPoint.Port)

    member c.Port
        with get () = c.IPEndPoint.Port
        and set x = c.IPEndPoint <- IPEndPoint(c.IPEndPoint.Address, x)

[<Sealed>]
type Server(stop: unit -> unit, stopped: Future<unit>) =

    static let start (config: ServerConfig) =
        let report = config.Report.Invoke
        let stopped = Future.Create()
        let stopRequested = Future.Create()
        let connections = LockedHashSet<Connection>()
        let pool = SocketPool()
        let handle sock =
            let conn = Connection.Create(pool, sock, report)
            connections.Add(conn)
            conn.Closed.On(fun _ -> connections.Remove(conn))
            async { return config.OnConnect.Invoke(conn) }
            |> Async.CaptureError config.Report.Invoke
            |> Async.Start
        async {
            use listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            try
                do
                    listenSocket.Bind(config.IPEndPoint)
                    listenSocket.Listen(config.Backlog)
                let rec loop =
                    async {
                        let! sock = SocketUtility.Accept stopRequested.Future listenSocket
                        match sock with
                        | None -> return ()
                        | Some sock ->
                            do handle sock
                            return! loop
                    }
                return! loop
            finally
                for c in connections.ToArray() do
                    try c.Terminate() with e -> report e
        }
        |> Async.StartThread stopped
        let stop () = stopRequested.Set(())
        Server(stop, stopped.Future)

    let stop =
        async {
            do stop ()
            return! stopped.AsyncAwait()
        }

    member this.AsyncStop() =
        stop

    member this.AsyncWrap(work) =
        Async.Wrap stop work

    member this.Stop() =
        Async.StartAsTask(stop) :> Task

    static member Start(config) =
        start config

    static member Start(k: Action<_>) =
        let cfg = ServerConfig()
        k.Invoke(cfg)
        Server.Start(cfg)

[<Sealed>]
type ClientConfig() =

    member val IPEndPoint = IPEndPoint(IPAddress.Loopback, 9912) with get, set
    member val Report = Action<exn>(ignore) with get, set
    member val SocketPool : option<SocketPool> = None with get, set

    member c.Address
        with get () = c.IPAddress.ToString()
        and set x = c.IPAddress <- Dns.GetHostAddresses(x).[0]

    member c.IPAddress
        with get () = c.IPEndPoint.Address
        and set (x: IPAddress) = c.IPEndPoint <- IPEndPoint(x, c.IPEndPoint.Port)

    member c.Port
        with get () = c.IPEndPoint.Port
        and set x = c.IPEndPoint <- IPEndPoint(c.IPEndPoint.Address, x)

[<Sealed>]
type Client(conn: Connection) =

    member c.Connection = conn

    static member AsyncConnect(conf: ClientConfig) =
        async {
            let! socket = SocketUtility.Connect conf.IPEndPoint
            let pool =
                match conf.SocketPool with
                | None -> SocketPool()
                | Some pool -> pool
            let conn = Connection.Create(pool, socket, conf.Report.Invoke)
            return new Client(conn)
        }

    static member AsyncConnect(k) =
        let c = ClientConfig()
        k c
        Client.AsyncConnect(c)

    static member Connect(conf: ClientConfig) =
        Client.AsyncConnect(conf) |> Async.StartAsTask

    static member Connect(k: Action<_>) =
        let c = ClientConfig()
        k.Invoke(c)
        Client.Connect(c)
