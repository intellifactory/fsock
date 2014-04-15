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
#nowarn "40"

[<Sealed>]
exception SocketException of SocketError with

    override this.Message =
        match this :> exn with
        | SocketException err -> string err
        | _ -> "impossible"

[<Sealed>]
type SocketArgs() =
    inherit SocketAsyncEventArgs()

    member val H1 : unit -> unit = ignore with get, set
    member val H2 : exn -> unit = ignore with get, set

    member args.Handle() =
        match args.SocketError with
        | SocketError.Success ->
            let h1 = args.H1
            args.H1 <- ignore
            args.H2 <- ignore
            h1 ()
        | e ->
            let h2 = args.H2
            args.H1 <- ignore
            args.H2 <- ignore
            h2 (SocketException e)

    override args.OnCompleted(_) =
        args.Handle()

    member args.Do(op: SocketAsyncEventArgs -> bool) : Async<unit> =
        Async.FromContinuations(fun (ok, no, _) ->
            args.H1 <- ok
            args.H2 <- no
            if not (op args) then
                args.Handle())

[<Sealed>]
type SocketPool(capacity: int) =

    let all =
        let chunkSize = 8192
        let memory : byte [] = Array.zeroCreate (chunkSize * capacity)
        let mk i =
            let args = new SocketArgs()
            args.SetBuffer(memory, chunkSize * i, chunkSize)
            args
        Array.init capacity mk

    let items =
        Bag(all)

    new () = new SocketPool(32)

    member this.WithLease(work: SocketArgs -> Async<'T>) : Async<'T> =
        async {
            let! item = items.AsyncTake()
            try
                return! work item
            finally
                items.Add(item)
        }

    interface IDisposable with
        member this.Dispose() =
            for x in all do
                x.Dispose()

module SocketUtility =

    let Accept (socket: Socket) =
        async {
            use args = new SocketArgs()
            do! args.Do(socket.AcceptAsync)
            return args.AcceptSocket
        }

    let Connect (ip: IPEndPoint) =
        async {
            use args = new SocketArgs(RemoteEndPoint = ip)
            let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            do! args.Do(socket.ConnectAsync)
            return socket
        }

    let IgnoreErrors main =
        async {
            try
                return! main
            with
            /// These error codes are normal when the socket gets closed.
            | SocketException SocketError.Disconnecting
            | SocketException SocketError.OperationAborted
            | SocketException SocketError.Shutdown

            /// Normal when the socket gets closed and cleaned up.
            | :? ObjectDisposedException ->
                return ()
        }

    let Disconnect (socket: Socket) =
        async {
            use args = new SocketArgs()
            return! args.Do(socket.DisconnectAsync)
        }
        |> IgnoreErrors

    (*
        NOTES:
            can probably do a lot better for Receive/Send, avoiding
            allocating intermediate Async's.
    *)

    let Receive (pool: SocketPool) (socket: Socket) (out: OutputChannel) =
        pool.WithLease <| fun args ->
            let buf : byte [] = Array.zeroCreate args.Count
            let rec loop : Async<unit> =
                async {
                    do! args.Do(socket.ReceiveAsync)
                    match args.BytesTransferred with
                    | 0 -> return ()
                    | n ->
                        do Buffer.BlockCopy(args.Buffer, args.Offset, buf, 0, n)
                        do! out.AsyncWrite(buf, 0, n)
                        return! loop
                }
            IgnoreErrors loop

    let Send (pool: SocketPool) (socket: Socket) (inp: InputChannel) =
        pool.WithLease <| fun args ->
            let buf : byte [] = Array.zeroCreate args.Count
            let rec loop : Async<unit> =
                async {
                    let! n = inp.AsyncRead(buf, 0, buf.Length)
                    do Buffer.BlockCopy(buf, 0, args.Buffer, args.Offset, n)
                    do args.SetBuffer(args.Offset, n)
                    do! args.Do(socket.SendAsync)
                    do args.SetBuffer(args.Offset, buf.Length)
                    return! loop
                }
            IgnoreErrors loop
