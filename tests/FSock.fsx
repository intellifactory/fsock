#load "../src/FSock/Collections.fs"
open FSock
#load "../src/FSock/Definitions.fs"
open FSock
#load "../src/FSock/Messaging.fs"
open FSock
#load "../src/FSock/Sockets.fs"
open FSock
#load "../src/FSock/FSock.fs"
#load "Testing.fsx"
#nowarn "40"

open System
open System.Collections
open System.Collections.Concurrent

let all () =
    let port = 8091
    let errors = ConcurrentBag()
    let domain = Testing.bytesDomain
    let received = ConcurrentQueue()
    let client =
        async {
            use! cli =
                FSock.Client.AsyncConnect(fun cfg ->
                    cfg.Report <- fun err -> errors.Add(err)
                    cfg.Port <- port)
            for msg in domain do
                do! cli.Connection.AsyncSendMessage(msg)
            return ()
        }
    let server =
        let fut = FSock.Future.Create()
        let rec server : FSock.Server =
            FSock.Server.Start(fun cfg ->
                cfg.Report <- fun err -> errors.Add(err)
                cfg.Port <- port
                cfg.OnConnect <- fun conn ->
                    async {
                        for i in 1 .. domain.Length do
                            let! msg = conn.AsyncReceiveMessage()
                            do received.Enqueue(msg)
                        do server.Stop()
                        do fut.Set(())
                        return ()
                    }
                    |> Async.Start)
        fut.Future.AsyncAwait()
    let wrap main =
        async { try return! main with e -> return errors.Add(e) }
    Async.Parallel [| client; server |]
    |> Async.Ignore
    |> wrap
    |> Async.RunSynchronously
    if received.ToArray() <> domain then
        failwithf "FSock: did not receive the sent messages"
    if errors.Count > 0 then
        for e in errors do
            eprintfn "%O" e
        failwithf "FSock: encountered errors"
    printfn "FSock: OK"

