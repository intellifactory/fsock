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

module internal Messaging =

    let writeUInt32 (data: uint32) (out: byte[]) =
        out.[3] <- byte (data >>> 24)
        out.[2] <- byte (data >>> 16)
        out.[1] <- byte (data >>> 8)
        out.[0] <- byte data

    let readUInt32 data offset =
        BitConverter.ToUInt32(data, offset)

    // NOTE: should definitely be possible to increase effiency here.

    let AsyncSendMessage (out: OutputChannel) (msg: byte[]) =
        let len = msg.Length
        let bytes = Array.zeroCreate (4 + len)
        writeUInt32 (uint32 len) bytes
        Buffer.BlockCopy(msg, 0, bytes, 4, len)
        out.AsyncWrite(bytes, 0, bytes.Length)

    let AsyncReceiveMessage (inp: InputChannel) =
        async {
            let buf = Array.zeroCreate 4
            let! ok = inp.AsyncReadExact(buf, 0, 4)
            if not ok then return None else
            let n = readUInt32 buf 0
            let buf = Array.zeroCreate (int n)
            let! ok = inp.AsyncReadExact(buf, 0, buf.Length)
            if not ok then return None else
            return Some buf
        }
