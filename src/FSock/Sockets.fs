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
#nowarn "40"

[<Sealed>]
exception SocketException of SocketError with
    override this.Message =
        match this :> exn with
        | SocketException err -> string err
        | _ -> "impossible"

[<Sealed>]
type SocketPool(capacity: int) =
    let items =
        let all =
            let chunkSize = 8192
            let memory : byte [] = Array.zeroCreate (chunkSize * capacity)
            let mk i = ArraySegment<byte>(memory, chunkSize * i, chunkSize)
            Seq.init capacity mk
        Bag(all)
    new () = new SocketPool(32)
    member this.CheckIn(v) = items.Add(v)
    member this.CheckOut(k) = items.Take(k)

module Sockets =

    let IsBenign error =
        match error with
        | SocketError.ConnectionReset
        | SocketError.Disconnecting
        | SocketError.OperationAborted
        | SocketError.Shutdown -> true
        | _ -> false

(*
    Below: a few state machines from hell.
    Would be much nicer to use something higher-level, perhaps CML/Hopac.
*)

[<Sealed>]
type SocketSender
    (
        pool: SocketPool,
        buf: ArraySegment<byte>,
        report: exn -> unit,
        socket: Socket,
        input: InputChannel,
        fut: FutureHandle<unit>
    ) as args =

    inherit SocketAsyncEventArgs()

    let received : int -> unit = args.Received
    let error : exn -> unit = args.Error

    override a.OnCompleted(_) =
        match a.SocketError with
        | SocketError.Success -> a.Sent()
        | _ -> a.Error()

    member a.Error() =
        if not (Sockets.IsBenign a.SocketError) then
            report (SocketException a.SocketError)
        a.Stop()

    member a.Error(e: exn) =
        match e with
        | :? ObjectDisposedException -> a.Stop()
        | _ -> report e; a.Stop()

    member a.Received(k: int) =
        try
            if k = 0 && input.IsClosed then
                a.Stop()
            else
                a.SetBuffer(a.Offset, k)
                let sched =
                    try
                        socket.SendAsync(a)
                    with e ->
                        a.Error(e)
                        true
                if not sched then
                    a.Sent()
        with e ->
            a.Error(e)

    member a.Sent() =
        let work = input.AsyncRead(a.Buffer, a.Offset, buf.Count)
        Async.StartWithContinuations(work, received, error, ignore)

    member a.Start() =
        a.SetBuffer(buf.Array, buf.Offset, buf.Count)
        a.Sent()

    member a.Stop() =
        a.SetBuffer(Array.empty, 0, 0)
        pool.CheckIn(buf)
        a.Dispose()
        fut.Set(())

    static member Fork(pool: SocketPool, socket, report, input) =
        let fut = Future.Create()
        pool.CheckOut(fun buf ->
            let p = new SocketSender(pool, buf, report, socket, input, fut)
            p.Start())
        fut.Future

[<Sealed>]
type SocketReceiver
    (
        pool: SocketPool,
        buf: ArraySegment<byte>,
        report: exn -> unit,
        socket: Socket,
        out: OutputChannel,
        fut: FutureHandle<unit>
    ) as args =

    inherit SocketAsyncEventArgs()

    let sent : unit -> unit = args.Sent
    let error : exn -> unit = args.Error

    override a.OnCompleted(_) =
        match a.SocketError with
        | SocketError.Success -> a.Received()
        | _ -> a.Error()

    member a.Error() =
        if not (Sockets.IsBenign a.SocketError) then
            report (SocketException a.SocketError)
        a.Stop()

    member a.Error(e: exn) =
        match e with
        | :? ObjectDisposedException -> a.Stop()
        | _ -> report e; a.Stop()

    member a.Received() =
        if out.IsClosed then
            a.Stop()
        else
            let work = out.AsyncWrite(a.Buffer, a.Offset, a.BytesTransferred)
            Async.StartWithContinuations(work, sent, error, ignore)

    member a.Sent() =
        try
            if socket.ReceiveAsync(a) |> not then
                a.Received()
        with e ->
            a.Error(e)

    member a.Start() =
        a.SetBuffer(buf.Array, buf.Offset, buf.Count)
        a.Sent()

    member a.Stop() =
        a.SetBuffer(Array.empty, 0, 0)
        pool.CheckIn(buf)
        a.Dispose()
        fut.Set(())

    static member Fork(pool: SocketPool, socket, report, output) =
        let fut = Future.Create()
        pool.CheckOut(fun buf ->
            let p = new SocketReceiver(pool, buf, report, socket, output, fut)
            p.Start())
        fut.Future

[<Sealed>]
type SocketAcceptor(k1: option<Socket> -> unit, k2: exn -> unit) =
    inherit SocketAsyncEventArgs()

    let mutable isDone = 0

    override a.OnCompleted(_) =
        if Interlocked.Increment(&isDone) = 1 then
            match a.SocketError with
            | SocketError.Success ->
                let s = a.AcceptSocket
                a.Dispose()
                k1 (Some s)
            | e ->
                a.Dispose()
                k2 (SocketException e)

    member a.Cancel() =
        if Interlocked.Increment(&isDone) = 1 then
            a.Dispose()
            k1 None

    member a.Start(socket: Socket) =
        if socket.AcceptAsync(a) |> not then
            a.OnCompleted(a)

    static member AsyncAccept(stop: Future<unit>, socket) =
        Async.FromContinuations(fun (k1, k2, _) ->
            let self = new SocketAcceptor(k1, k2)
            stop.On(fun () -> self.Cancel())
            self.Start(socket))

[<Sealed>]
type SocketOp(k1: unit -> unit, k2: exn -> unit) =
    inherit SocketAsyncEventArgs()

    member op.Complete() =
        match op.SocketError with
        | SocketError.Success ->
            op.Dispose()
            k1 ()
        | e ->
            op.Dispose()
            k2 (SocketException e)

    override op.OnCompleted(_) =
        op.Complete()

module SocketUtility =

    type A = SocketAsyncEventArgs

    let inline doAsync (op: A -> bool) =
        Async.FromContinuations(fun (k1, k2, _) ->
            let so = new SocketOp(k1, k2)
            if not (op so) then
                so.Complete())

    let Accept stop socket =
        SocketAcceptor.AsyncAccept(stop, socket)

    let Connect (ip: IPEndPoint) =
        async {
            let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            do! doAsync (fun args ->
                    args.RemoteEndPoint <- ip
                    socket.ConnectAsync(args))
            return socket
        }

    let IgnoreErrors main =
        async {
            try
                return! main
            with
            | SocketException x when Sockets.IsBenign x ->
                return () // These error codes are normal when the socket gets closed.
            | :? ObjectDisposedException ->
                return () // Normal when the socket gets closed and cleaned up.
        }

    let Disconnect (socket: Socket) =
        doAsync socket.DisconnectAsync
        |> IgnoreErrors

    type Context =
        {
            Report : exn -> unit
            Socket : Socket
            SocketPool : SocketPool
        }

    let ForkReceiver ctx ch =
        SocketReceiver.Fork(ctx.SocketPool, ctx.Socket, ctx.Report, ch)

    let ForkSender ctx ch =
        SocketSender.Fork(ctx.SocketPool, ctx.Socket, ctx.Report, ch)
