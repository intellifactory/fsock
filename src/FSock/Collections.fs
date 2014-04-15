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
type Bag<'T>(items: seq<'T>) =
    let root = obj ()
    let items = Queue<'T>(items)
    let takers = Queue<'T->unit>()

    new () = Bag(Seq.empty)

    member bag.Add(x: 'T) =
        Monitor.Enter(root)
        if takers.Count > 0 then
            let t = takers.Dequeue()
            Monitor.Exit(root)
            async { return t x }
            |> Async.Start
        else
            items.Enqueue(x)
            Monitor.Exit(root)

    member bag.AsyncTake() =
        Async.FromContinuations(fun (ok, _, _) ->
            bag.Take(ok))

    member bag.Take(k: 'T -> unit) =
        Monitor.Enter(root)
        if items.Count > 0 then
            let it = items.Dequeue()
            Monitor.Exit(root)
            k it
        else
            takers.Enqueue(k)
            Monitor.Exit(root)

/// Represents a logical interval in a RingBuffer as one or two intervals
/// on the physical backing array.
/// (P1, L1) and (P2, L2) are first and second interval offset and length.
[<Struct>]
type Interval(p1: int, l1: int, l2: int) =
    member iv.P1 = p1
    member iv.L1 = l1
    member iv.P2 = 0
    member iv.L2 = l2

[<Sealed>]
type RingBuffer<'T>(capacity: int) =
    let buf : 'T [] = Array.zeroCreate capacity
    let mutable off = 0
    let mutable len = 0

    let point i =
        (off + i) % capacity

    let interval offset len =
        let p1 = point offset
        let l1 = min (capacity - p1) len
        Interval(p1, l1, len - l1)

    member rb.Add(b, o, c) =
        let tr = min (capacity - len) c
        if tr > 0 then
            let iv = interval len tr
            Array.blit b o buf iv.P1 iv.L1
            if iv.L2 > 0 then
                Array.blit b (o + iv.L1) buf iv.P2 iv.L2
            len <- len + tr
        tr

    member rb.Take(b, o, c) =
        let tr = min len c
        if tr > 0 then
            let iv = interval 0 tr
            Array.blit buf iv.P1 b o iv.L1
            if iv.L2 > 0 then
                Array.blit buf iv.P2 b (o + iv.L1) iv.L2
            off <- off + tr
            len <- len - tr
        tr

    member rb.Count = len
    member rb.IsFull = len = capacity

[<Sealed>]
type RQReader<'T>(b: 'T[], o: int, c: int, k: int -> unit) =
    member r.Read(buf: RingBuffer<'T>) =
        let n = buf.Take(b, o, c)
        async { return k n } |> Async.Start

[<Sealed>]
type RQWriter<'T>(b: 'T[], k: unit -> unit) =
    let mutable offset = 0
    let mutable count = b.Length

    new (b, o, c, k) = RQWriter(Array.sub b o c, k)

    member r.Write(buf: RingBuffer<'T>) =
        let n = buf.Add(b, offset, count)
        if n = count then
            async { return k () } |> Async.Start
            true
        else
            offset <- offset + n
            count <- count - n
            false

[<Sealed>]
type RingQueue<'T>(capacity: int) =
    let root = obj ()
    let buf = RingBuffer<'T>(capacity)
    let readers = Queue<RQReader<'T>>()
    let writers = Queue<RQWriter<'T>>()

    let rec progress () =
        if buf.Count > 0 && readers.Count > 0 then
            let r = readers.Dequeue()
            r.Read(buf)
            progress ()
        elif writers.Count > 0 && not buf.IsFull then
            let w = writers.Peek()
            if w.Write(buf) then
                writers.Dequeue() |> ignore
            progress ()

    member rb.Read(b, o, c, k) =
        Monitor.Enter(root)
        if buf.Count > 0 then
            let n = buf.Take(b, o, c)
            progress ()
            Monitor.Exit(root)
            k n
        else
            readers.Enqueue(RQReader(b, o, c, k))
            progress ()
            Monitor.Exit(root)

    member rb.Write(b, o, c, k) =
        Monitor.Enter(root)
        let n = buf.Add(b, o, c)
        if n = c then
            progress ()
            Monitor.Exit(root)
            k ()
        else
            writers.Enqueue(RQWriter(b, o + n, c - n, k))
            progress ()
            Monitor.Exit(root)

    member rb.Write(b, k) =
        rb.Write(b, 0, Array.length b, k)

    member rb.AsyncRead(b, o, c) =
        Async.FromContinuations(fun (ok, _, _) -> rb.Read(b, o, c, ok))

    member rb.AsyncReadExact(b, o, c) =
        async {
            let! n = rb.AsyncRead(b, o, c)
            if n = c then
                return ()
            else
                return! rb.AsyncReadExact(b, o + n, c - n)
        }

    member rb.AsyncWrite(b, o, c) =
        Async.FromContinuations(fun (ok, _, _) -> rb.Write(b, o, c, ok))
