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

/// Represents a channel that can be read from.
[<Sealed>]
type InputChannel =

    /// Reads into target buffer, returns number of bytes read.
    member AsyncRead : target: byte[] * offset: int * count: int -> Async<int>

    /// Reads until the target buffer is entirely filled.
    member AsyncReadExact : target: byte[] * offset: int * count: int -> Async<unit>

/// Represents a channel that can be written to.
[<Sealed>]
type OutputChannel =

    /// Writes from the source buffer, returns when the write is complete.
    member AsyncWrite : source: byte[] * offset: int * count: int -> Async<unit>

/// A pipe of connected InputChannel and OutputChannel.
[<Sealed>]
type Channel =

    /// Constructs a new channel with a given capacity.
    new : capacity: int -> Channel

    /// Constructs a new channel with a default capacity.
    new : unit -> Channel

    /// The input end of the pipe.
    member In : InputChannel

    /// The output end of the pipe.
    member Out : OutputChannel

/// Write-once variables for synchronization (aka IVar).
[<Sealed>]
type Future<'T> =

    /// Awaits for the Future using as F# async.
    member AsyncAwait : unit -> Async<'T>

    /// Awaits for the Future asynchronously.
    member Await : unit -> Task<'T>

    /// Schedules the continuation to execute when the Future arrives.
    member On : Action<'T> -> unit

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

/// Classifies results of executing a logical thread.
type Exit =
    | ErrorExit of exn
    | NormalExit

    /// Extracts the associated exception, if any.
    member Exception : exn

    /// Tests if the result is an error.
    member IsError : bool

/// Inspired by Racket custodians, this object manages finalization.
/// Currently designed to manage a single logical thread (see Start).
[<Sealed>]
type Custodian =

    /// Disposing the Custodian finalizes all attached resources.
    interface IDisposable

    /// Creates a new Custodian with a given exception reporter.
    new : Action<exn> -> Custodian

    /// Creates a new Custodian that ignores errors.
    new : unit -> Custodian

    /// Adds a resource to finalize together with the Custodian.
    member Add : IDisposable -> unit

    /// Schedules an Async job to run on finalization.
    member AsyncFinally : Async<unit> -> unit

    /// Closes the custodian and finalizes associated resource.
    /// Same as Dispose in the IDisposable implementation.
    member Close : Exit -> unit

    /// Same as `Close NormalExit`.
    member Close : unit -> unit

    /// Schedules a simple job ot run on finalization.
    member Finally : Action -> unit

    /// Reports an exception.
    member Report : exn -> unit

    /// Forks the given thread and waits until it completes,
    /// then disposes resources managed by this Custodian.
    member Start : Async<unit> -> unit

    /// The result of closing the Custodian.
    member Closed : Future<Exit>

