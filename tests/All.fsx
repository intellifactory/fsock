#load "Collections.fsx"
#load "Messaging.fsx"
#load "FSock.fsx"

let runAllTests () =
    Collections.all ()
    Messaging.all ()
    FSock.all ()

runAllTests ()
