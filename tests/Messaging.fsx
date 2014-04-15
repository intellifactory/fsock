#load "Testing.fsx"
#load "../src/FSock/Collections.fs"
open FSock
#load "../src/FSock/Definitions.fs"
open FSock
#load "../src/FSock/Messaging.fs"
open FSock.Messaging

let testReadWriteUInt32 () =
    let bytes = Array.zeroCreate 4
    for i in Testing.uint32Domain do
        writeUInt32 i bytes
        if readUInt32 bytes 0 <> i then
            failwithf "readUInt32/writeUInt32 problem at: %i" i

let all () =
    testReadWriteUInt32 ()
    printfn "Messaging: OK"
