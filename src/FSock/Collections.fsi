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

/// HashSet with a lock for thread safety.
[<Sealed>]
type internal LockedHashSet<'T> =

    /// Creates a new one.
    new : unit -> LockedHashSet<'T>

    /// Adds an item.
    member Add : 'T -> unit

    /// Removes an item.
    member Remove : 'T -> unit

    /// Extracts held items.
    member ToArray : unit -> 'T []

/// Equivalent of ConcurrentBag, but with item retrieval raised to Async.
/// This collection is thread-safe.
[<Sealed>]
type internal Bag<'T> =

    /// Creates an empty bag.
    new : unit -> Bag<'T>

    /// Initializes a bag with a sequence of items.
    new : seq<'T> -> Bag<'T>

    /// Adds an item.
    member Add : 'T -> unit

    /// Picks an item asynchronously.
    member AsyncTake : unit -> Async<'T>

    /// Picks an item into the callback.
    member Take : ('T -> unit) -> unit

/// Equivalent of Queue, but implemented as a fixed-capacity ring buffer
/// with bulk Add/Take operations. This collection is not thread-safe.
[<Sealed>]
type internal RingBuffer<'T> =

    /// Creates a new RingBuffer with a given capacity.
    new : capacity: int -> RingBuffer<'T>

    /// Adds data from the source array to the end of the RingBuffer.
    /// Returns the count of elements copied
    member Add : source: 'T[] * offset: int * count: int -> int

    /// Takes and removes data from the beginning of the RingBuffer,
    /// copying it to the target array.
    /// Returns the count of elements copied.
    member Take : target: 'T[] * offset: int * count: int -> int

/// Thread-safe collection for synchronizing bulk readers and writers.
/// Maintains order if there is only 1 reader and 1 writer.
/// Includes mechanisms for flow control when buffer gets filled.
[<Sealed>]
type internal RingQueue<'T> =

    /// Creates a new queue backed by a buffer of given capacity.
    /// The onFinalized handler is called after Close command when
    /// all buffered elements are read.
    new : capacity: int * onFinalized : (unit -> unit) -> RingQueue<'T>

    /// Closes the channel, releasing any processes waiting on the channel.
    /// Subsequent writes return immediately, and reads also, with 0 bytes read.
    member Close : unit -> unit

    /// Reads into target buffer, returns number of bytes read.
    member AsyncRead : target: 'T[] * offset: int * count: int -> Async<int>

    /// Reads until the target buffer is entirely filled.
    member AsyncReadExact : target: 'T[] * offset: int * count: int -> Async<unit>

    /// Writes from the source buffer, returns when the write is complete.
    member AsyncWrite : source: 'T[] * offset: int * count: int -> Async<unit>

    /// Continuation version of AsyncRead.
    member Read : target: 'T[] * offset: int * count: int * (int -> unit) -> unit

    /// Continuation version of AsyncWrite.
    member Write : source: 'T[] * offset: int * count: int * (unit -> unit) -> unit

    /// Whether the queue is closed.
    member IsClosed : bool
