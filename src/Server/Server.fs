module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web
open Saturn
open Giraffe
open Giraffe.Auth

open Shared

module Storage =
    let todos = ResizeArray()

    let addTodo todo =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok()
        else
            Error "Invalid todo"

    do
        addTodo (Todo.create "Create new SAFE project") |> ignore
        addTodo (Todo.create "Write your app") |> ignore
        addTodo (Todo.create "Ship it!!!") |> ignore

let todosApi = {
    getTodos = fun () -> async { return Storage.todos |> List.ofSeq }
    addTodo =
        fun todo -> async {
            return
                match Storage.addTodo todo with
                | Ok() -> todo
                | Error e -> failwith e
        }
}

let remotingApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue todosApi
    |> Remoting.buildHttpHandler

let signIn : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            do! ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme)
            return! next ctx
        }

let requiresAuth =
    requiresAuthentication signIn

let webApp =
    choose [
        route "/" >=> requiresAuth >=> text "hello world"
        routeStartsWith "/api" >=> remotingApi
    ]

let registerMiddleware (appBuilder : IApplicationBuilder) =
    appBuilder
        .UseAuthentication()
        .UseAuthorization()

let registerServices (services : IServiceCollection) =
    let sp = services.BuildServiceProvider()
    let config = sp.GetService<IConfiguration>()
    services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(config)
        .Services
        .AddAuthorization()

let app = application {
    app_config registerMiddleware
    service_config registerServices
    use_router webApp
    memory_cache
    use_static "public"
    use_gzip
}

[<EntryPoint>]
let main _ =
    run app
    0