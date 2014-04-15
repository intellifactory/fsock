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
type Connection(custodian: Custodian, inp: InputChannel, out: OutputChannel) =

    interface IDisposable with
        member c.Dispose() =
            c.Close()

    member c.AsyncReceiveMessage() =
        Messaging.AsyncReceiveMessage inp

    member c.AsyncSendMessage(msg) =
        Messaging.AsyncSendMessage out msg

    member c.Close() =
        custodian.Close()

    member c.ReceiveMessage() =
        c.AsyncReceiveMessage()
        |> Async.StartAsTask

    member c.SendMessage(msg) =
        c.AsyncSendMessage(msg)
        |> Async.StartAsTask :> Task

    member c.Closed = custodian.Closed
    member c.Custodian = custodian
    member c.InputChannel = inp
    member c.OutputChannel = out

    static member Create(pool: SocketPool, socket: Socket, report: exn -> unit) =
        let custodian = new Custodian(Action<exn>(report))
        let wrap main =
            async { try return! main with e -> return custodian.Report(e) }
        let par2 work1 work2 =
            Async.Parallel [| work1; work2 |]
            |> Async.Ignore
        let c1 = Channel()
        let c2 = Channel()
        let stop =
            async {
                use _ = socket
                return! SocketUtility.Disconnect socket
            }
            |> wrap
        let work =
            let receive = SocketUtility.Receive pool socket c1.Out
            let send = SocketUtility.Send pool socket c2.In
            par2 (wrap receive) (wrap send)
        custodian.AsyncFinally(stop)
        custodian.Start(work)
        new Connection(custodian, c1.In, c2.Out)

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
type Server(custodian: Custodian) =

    static let start (config: ServerConfig) =
        let custodian = new Custodian(config.Report)
        let listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        let pool = new SocketPool()
        custodian.Add(listenSocket)
        custodian.Add(pool)
        listenSocket.Bind(config.IPEndPoint)
        listenSocket.Listen(config.Backlog)
        let onConnect = config.OnConnect
        let handleConnection socket =
            Connection.Create(pool, socket, config.Report.Invoke)
            |> onConnect.Invoke
        async {
            while true do
                let! socket = SocketUtility.Accept listenSocket
                do handleConnection socket
        }
        |> SocketUtility.IgnoreErrors
        |> custodian.Start
        custodian

    interface IDisposable with
        member this.Dispose() =
            this.Stop()

    member this.Stop() =
        custodian.Close()

    static member Start(config) =
        new Server(start config)

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

    interface IDisposable with
        member c.Dispose() = conn.Close()

    member c.Connection = conn

    static member AsyncConnect(conf: ClientConfig) =
        async {
            let! socket = SocketUtility.Connect conf.IPEndPoint
            let conn =
                match conf.SocketPool with
                | None ->
                    let pool = new SocketPool()
                    let conn = Connection.Create(pool, socket, conf.Report.Invoke)
                    conn.Custodian.Add(pool)
                    conn
                | Some pool ->
                    Connection.Create(pool, socket, conf.Report.Invoke)
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
