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
open System.Collections
open System.Collections.Generic
open System.Threading

[<Sealed>]
type InputChannel(rb: RingQueue<byte>) =
    member ch.AsyncRead(b, o, c) = rb.AsyncRead(b, o, c)
    member ch.AsyncReadExact(b, o, c) = rb.AsyncReadExact(b, o, c)
    member ch.IsClosed = rb.IsClosed

[<Sealed>]
type OutputChannel(rb: RingQueue<byte>) =
    member ch.AsyncWrite(b, o, c) = rb.AsyncWrite(b, o, c)
    member ch.IsClosed = rb.IsClosed

[<Sealed>]
type Future<'T>() =
    let root = obj ()
    let waiters = Queue<Action<'T>>()
    let mutable v = None

    member this.AsyncAwait() =
        Async.FromContinuations(fun (ok, _, _) -> this.On(Action<'T>(ok)))

    member this.Await() =
        this.AsyncAwait()
        |> Async.StartAsTask

    member this.On(k: Action<'T>) =
        Monitor.Enter(root)
        match v with
        | Some v ->
            Monitor.Exit(root)
            k.Invoke(v)
        | None ->
            waiters.Enqueue(k)
            Monitor.Exit(root)

    member this.Set<'T>(value: 'T) =
        lock root <| fun () ->
            if v.IsNone then
                v <- Some value
                for w in waiters do
                    async { return w.Invoke(value) }
                    |> Async.Start
                waiters.Clear()

    member this.Is = v.IsSome

[<Sealed>]
type FutureHandle<'T>(f: Future<'T>) =
    member h.Future = f
    member h.Set(x) = f.Set(x)

[<Sealed>]
type Future =

    static member Create<'T>() : FutureHandle<'T> =
        FutureHandle<'T>(Future<'T>())

    static member internal Both(a: Future<unit>, b: Future<unit>) =
        let f = Future.Create()
        let k = ref 0
        let g () =
            if Interlocked.Increment(&k.contents) = 2 then
                f.Set(())
        let h = Action<unit>(g)
        a.On(h)
        b.On(h)
        f.Future

    static member internal First(a: Future<'T>, b: Future<'T>) =
        let f = Future.Create()
        let k = ref 0
        let g x =
            if Interlocked.Increment(&k.contents) = 1 then
                f.Set(x)
        let h = Action<'T>(g)
        a.On(h)
        b.On(h)
        f.Future

[<Sealed>]
type Channel(capacity: int) =
    let fin = Future.Create()
    let rb = RingQueue<byte>(capacity, fun () -> fin.Set(()))
    let ch1 = InputChannel(rb)
    let ch2 = OutputChannel(rb)
    new () = Channel(16384)
    member ch.Close() = rb.Close()
    member ch.Done = fin.Future
    member ch.In = ch1
    member ch.Out = ch2
    member ch.IsClosed = rb.IsClosed

module Async =

    // TODO: better efficiency here.
    let Wrap fin main =
        async {
            let! res =
                async {
                    try
                        let! v = main
                        return Choice1Of2 v
                    with e ->
                        return Choice2Of2 e
                }
            match res with
            | Choice1Of2 r ->
                do! fin
                return r
            | Choice2Of2 e ->
                try do! fin with err -> ()
                return raise e
        }

    let CaptureError (report: exn -> unit) (main: Async<unit>) =
        async {
            try
                return! main
            with e ->
                return report e
        }

    let StartThread (handle: FutureHandle<unit>) (main: Async<unit>) =
        async {
            try
                return! main
            finally
                handle.Set(())
        }
        |> Async.Start
