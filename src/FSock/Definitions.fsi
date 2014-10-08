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
open System.Threading
open System.Threading.Tasks

/// Write-once variables for synchronization (aka IVar).
[<Sealed>]
type Future<'T> =

    /// Awaits for the Future using as F# async.
    member AsyncAwait : unit -> Async<'T>

    /// Awaits for the Future asynchronously.
    member Await : unit -> Task<'T>

    /// Schedules the continuation to execute when the Future arrives.
    member On : Action<'T> -> unit

    /// Tests if the future has arrived.
    member Is : bool

/// Represents a Future together with the ability to imperatively set it.
[<Sealed>]
type FutureHandle<'T> =

    /// Sets the associated future, should only be called once.
    member Set : 'T -> unit

    /// The associated Future value.
    member Future : Future<'T>

/// Static operations on Future values.
[<Sealed>]
type Future =

    /// Creates a new Future.
    static member Create<'T> : unit -> FutureHandle<'T>

    /// Waits for both.
    static member internal Both : Future<unit> * Future<unit> -> Future<unit>

    /// First of two.
    static member internal First : Future<'T> * Future<'T> -> Future<'T>

/// Represents a channel that can be read from.
[<Sealed>]
type InputChannel =

    /// Whether the channel is closed.
    member IsClosed : bool

    /// Reads into target buffer, returns number of bytes read.
    member AsyncRead : target: byte[] * offset: int * count: int -> Async<int>

    /// Reads until the target buffer is entirely filled.
    member AsyncReadExact : target: byte[] * offset: int * count: int -> Async<bool>

/// Represents a channel that can be written to.
[<Sealed>]
type OutputChannel =

    /// Writes from the source buffer, returns when the write is complete.
    member AsyncWrite : source: byte[] * offset: int * count: int -> Async<unit>

    /// Whether the channel is closed.
    member IsClosed : bool

/// A pipe of connected InputChannel and OutputChannel.
[<Sealed>]
type Channel =

    /// Constructs a new channel with a given capacity.
    new : capacity: int -> Channel

    /// Constructs a new channel with a default capacity.
    new : unit -> Channel

    /// Closes the channel, releasing any processes waiting on the channel.
    member Close : unit -> unit

    /// Arrives after Close, when all buffers are flushed.
    member Done : Future<unit>

    /// The input end of the pipe.
    member In : InputChannel

    /// The output end of the pipe.
    member Out : OutputChannel

    /// Whether the channel is closed.
    member IsClosed : bool

/// Async utilities.
module internal Async =

    /// IDisposable pattern with async finalizer.
    val Wrap : fin: Async<unit> -> Async<'T> -> Async<'T>

    /// Starts a given thread, setting the FutureHandle when done.
    val StartThread : FutureHandle<unit> -> Async<unit> -> unit

    /// Starts a given thread, setting the FutureHandle when done.
    val CaptureError : (exn -> unit) -> Async<unit> -> Async<unit>
