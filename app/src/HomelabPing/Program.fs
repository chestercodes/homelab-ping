namespace HomelabPing

#nowarn "20"

open System
open System.IO
open System.Diagnostics
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open Prometheus
open Serilog
open Serilog.Formatting
open Serilog.Sinks.Grafana.Loki

module Custom =
    type LokiJsonTextFormatterWithNewLine() =
        let underlying = LokiJsonTextFormatter()

        interface ITextFormatter with
            member this.Format((logEvent: Events.LogEvent), (output: TextWriter)) =
                // want to have a new line after each log entry that makes itself to the console
                underlying.Format(logEvent, output)
                output.WriteLine()
                ()


module Program =
    open Npgsql

    type PingResponse = { value: string }

    let exitCode = 0

    let throwIfNull envVarName =
        let v = Environment.GetEnvironmentVariable(envVarName)

        if v = null then
            raise (Exception(envVarName + " is null"))
        else
            v

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)

        builder.Services.AddControllers()
        builder.Host.UseSerilog()

        // Define some important constants to initialize tracing with
        let serviceName = "homelab-ping"
        let envname = throwIfNull "ENVNAME"
        let serviceNamespace = envname
        let serviceVersion = throwIfNull "IMAGE_TAG"

        let oltpEndpoint = throwIfNull "OLTP_ENDPOINT"

        let user = throwIfNull "MAINDB_USER"
        let password = throwIfNull "MAINDB_PASSWORD"

        let hostFromEnvname =
            sprintf "%s-homelabmaindb-cluster-postgresql.%s.svc" envname envname

        let hostFromEnv = Environment.GetEnvironmentVariable("HOST")
        let host = if hostFromEnv <> null then hostFromEnv else hostFromEnvname


        let connectionString =
            sprintf "Host=%s;Port=5432;User ID=%s;Password=%s;Database=pingapp" host user password

        printfn "%s" connectionString

        let appResourceBuilder =
            ResourceBuilder
                .CreateDefault()
                .AddService(serviceName, serviceNamespace, serviceVersion)

        // Configure important OpenTelemetry settings, the console exporter, and instrumentation library
        builder.Services.AddOpenTelemetryTracing(fun tracerProviderBuilder ->
            tracerProviderBuilder
            |> fun b ->
                b.AddOtlpExporter(fun opt ->
                    // opt.Protocol <- OtlpExportProtocol.HttpProtobuf
                    opt.Endpoint <- Uri(oltpEndpoint))
            |> fun b -> b.AddSource(serviceName)
            |> fun b -> b.SetResourceBuilder(appResourceBuilder)
            |> fun b -> b.AddHttpClientInstrumentation()
            |> fun b -> b.AddAspNetCoreInstrumentation()
            |> ignore)

        let MyActivitySource = new ActivitySource(serviceName)

        use server = new Prometheus.KestrelMetricServer(9090)
        server.Start()

        let RequestCount =
            Metrics.CreateCounter("pingapp_num_requests_since_startup", "Number of requests since startup.")

        let formatter = Custom.LokiJsonTextFormatterWithNewLine()

        let logger =
            new LoggerConfiguration()
            |> fun l -> l.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            |> fun l -> l.Enrich.FromLogContext()
            |> fun l -> l.Enrich.WithProperty("app_name", serviceName)
            |> fun l -> l.Enrich.WithProperty("envname", envname)
            |> fun l -> l.Enrich.WithProperty("image_tag", serviceVersion)
            |> fun l -> l.WriteTo.Console(formatter)
            |> fun l -> l.CreateLogger()

        Log.Logger <- logger

        let pingFunc =
            fun () ->
                use activity = MyActivitySource.StartActivity("Ping")
                activity.SetTag("foo", 1)
                activity.SetTag("bar", "Hello, World!")

                RequestCount.Inc(1)

                Log.Information(
                    "Hi there it is {Time} and I have been called {NumTimes} since I started up",
                    DateTime.UtcNow,
                    RequestCount.Value
                )

                task {
                    use conn = new NpgsqlConnection(connectionString)
                    do! conn.OpenAsync()

                    use cmd = new NpgsqlCommand("SELECT Name FROM public.pingtable", conn)
                    use! reader = cmd.ExecuteReaderAsync()
                    let! canRead = reader.ReadAsync()

                    if canRead then
                        let firstNameInTable = reader.GetString(0)
                        Log.Information(firstNameInTable)
                    else
                        Log.Information("No data in table!")

                    ()
                }
                |> Async.AwaitTask
                |> Async.RunSynchronously

                { value = "pong" }

        let app = builder.Build()

        app.MapGet("/health", Func<string>(fun () -> "I'm good cheers")) |> ignore

        app.MapGet("/ping", Func<PingResponse>(pingFunc)) |> ignore

        app.UseHttpsRedirection()

        app.UseSerilogRequestLogging()

        app.UseAuthorization()
        app.MapControllers()
        printfn "Starting app..."
        app.Run()

        exitCode
