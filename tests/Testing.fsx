open System

let gen = Random(DateTime.Now.Millisecond)

let randomInt lo hi =
    gen.Next(lo, hi + 1)

let randBool () =
    randomInt 0 1 = 0

let bytesDomain =
    [|
        yield Array.empty
        for i in 1 .. 1024 do
            yield Array.init i (fun i -> byte (i % 256))
    |]

let byteRangeDomain =
    [|
        for bytes in bytesDomain ->
            let offset = randomInt 0 bytes.Length
            let length = randomInt 0 (bytes.Length - offset)
            (bytes, offset, length)
    |]

let testEqual test a b =
    if a <> b then
        failwithf "%s: expected %A and got %A" test a b

let uint32Domain =
    Seq.concat [
        seq { UInt32.MinValue .. UInt32.MinValue + uint32 1000000 }
        Seq.init 1000000 (fun x -> uint32 (gen.Next()))
        seq { UInt32.MaxValue - uint32 1000000 .. UInt32.MaxValue }
    ]
