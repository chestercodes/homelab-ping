module Tests

open System
open Xunit
open System.IO
open System.Linq
open System.Threading.Tasks
open System.Net
open System.Net.Http

let throwIfNull envVarName =
    let v = Environment.GetEnvironmentVariable(envVarName)

    if v = null then
        raise (Exception(envVarName + " is null"))
    else
        v

[<Fact>]
let ``Can call the ping end point`` () =


    task {
        use client = new HttpClient()
        let envname = throwIfNull "ENVNAME"
        let baseUrl = sprintf "http://homelabping-stable-service.%s.svc:80" envname
        let url = sprintf "%s/ping" baseUrl
        printfn "Calling GET on %s" url

        let! response = client.GetAsync(url)

        ()
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously

    Assert.True(true)
