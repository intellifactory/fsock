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

[<Sealed>]
type OutputChannel(rb: RingQueue<byte>) =
    member ch.AsyncWrite(b, o, c) = rb.AsyncWrite(b, o, c)

[<Sealed>]
type Channel(capacity: int) =
    let rb = RingQueue<byte>(capacity)
    let ch1 = InputChannel(rb)
    let ch2 = OutputChannel(rb)
    new () = Channel(16384)
    member ch.In = ch1
    member ch.Out = ch2

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
            if v.IsSome then
                failwith "Cannot set a Future twice"
            else
                v <- Some value
                for w in waiters do
                    async { return w.Invoke(value) }
                    |> Async.Start
                waiters.Clear()

[<Sealed>]
type FutureHandle<'T>(f: Future<'T>) =
    member h.Future = f
    member h.Set(x) = f.Set(x)

[<Sealed>]
type Future =
    static member Create<'T>() : FutureHandle<'T> =
        FutureHandle<'T>(Future<'T>())

type Exit =
    | ErrorExit of exn
    | NormalExit

    member x.Exception =
        match x with
        | ErrorExit e -> e
        | NormalExit -> exn ()

    member x.IsError =
        match x with
        | ErrorExit e -> true
        | NormalExit -> false

[<Sealed>]
type Custodian(report: Action<exn>) =
    let root = obj ()
    let finalizers = ResizeArray()
    let mutable closed = false
    let futureHandle = Future.Create()
    let mutable exit = NormalExit

    let report e =
        exit <- Exit.ErrorExit e
        report.Invoke(e)

    let wrap main =
        async { try return! main with e -> return report e }

    new () =
        new Custodian(Action<exn>(ignore))

    interface IDisposable with
        member c.Dispose() = c.Close()

    member c.Add(d: IDisposable) =
        c.Finally(Action(d.Dispose))

    member c.AsyncFinally(work: Async<unit>) =
        lock root <| fun () ->
            if closed
                then Async.Start(work)
                else finalizers.Add(work)

    member c.Close(result) =
        lock root <| fun () ->
            if not closed then
                if not exit.IsError then
                    exit <- result
                closed <- true
                let fs = finalizers.ToArray()
                async {
                    for f in fs do
                        return! wrap f
                    return futureHandle.Set(exit)
                }
                |> Async.Start
                finalizers.Clear()

    member c.Close() =
        c.Close(NormalExit)

    member c.Finally(work: Action) =
        async { return work.Invoke() }
        |> c.AsyncFinally

    member c.Report(e) =
        report e

    member c.Start(work: Async<unit>) =
        async {
            use _ = c
            return! wrap work
        }
        |> Async.Start

    member c.Closed = futureHandle.Future

