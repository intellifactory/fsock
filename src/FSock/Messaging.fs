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
            do! inp.AsyncReadExact(buf, 0, 4)
            let n = readUInt32 buf 0
            let buf = Array.zeroCreate (int n)
            do! inp.AsyncReadExact(buf, 0, buf.Length)
            return buf
        }
