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
    let log x = Printf.ksprintf (fun msg -> lock stdout (fun () -> stdout.WriteLine(msg))) x
    let received = ConcurrentQueue()
    let report prefix e =
        log "%s :: %O" prefix e
        errors.Add(e)
    let client =
        async {
            let! cli =
                FSock.Client.AsyncConnect(fun cfg ->
                    cfg.Report <- fun e -> report "client" e
                    cfg.Port <- port)
            do!
                async {
                    for msg in domain do
                        do! cli.Connection.AsyncSendMessage(msg)
                        let! rep = cli.Connection.AsyncReceiveMessage()
                        if rep <> Array.append msg msg then
                            report "client" (exn "Turnaround failure")
                }
                |> cli.Connection.AsyncWrap
            return ()
        }
    let server =
        let fut = FSock.Future.Create()
        let rec server : FSock.Server =
            FSock.Server.Start(fun cfg ->
                cfg.Report <- fun e -> report "server" e
                cfg.Port <- port
                cfg.OnConnect <- fun conn ->
                    conn.Closed.On(fun () ->
                        async {
                            do! server.AsyncStop()
                            return fut.Set(())
                        }
                        |> Async.Start)
                    async {
                        for i in 1 .. domain.Length do
                            let! msg = conn.AsyncReceiveMessage()
                            do! conn.AsyncSendMessage(Array.append msg msg)
                            do received.Enqueue(msg)
                    }
                    |> conn.AsyncWrap
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
            log "%O" e
        failwithf "FSock: encountered errors"
    log "FSock: OK"
