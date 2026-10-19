using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Tidebite.Core;
using Tidebite.Matchmaker;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=8192);
builder.Services.ConfigureHttpJsonOptions(options=>options.SerializerOptions.IncludeFields=true);
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=429;
    options.AddConcurrencyLimiter("public",limiter=>{limiter.PermitLimit=64;limiter.QueueLimit=0;limiter.QueueProcessingOrder=QueueProcessingOrder.OldestFirst;});
});
string path=Environment.GetEnvironmentVariable("TIDEBITE_ROOMS_FILE")??throw new InvalidOperationException("Set TIDEBITE_ROOMS_FILE to a private room configuration file.");
var definitions=JsonSerializer.Deserialize<RoomDefinition[]>(File.ReadAllText(path),new JsonSerializerOptions{IncludeFields=true})??throw new InvalidOperationException("Missing rooms.");
var registry=new RoomRegistry(definitions);
var app=builder.Build();app.UseRateLimiter();
app.MapGet("/health",()=>Results.Ok(new{status="ok",protocol=OnlineProtocol.Version}));
app.MapPost("/v1/matches",(MatchRequest request)=>
{
    var reply=registry.Match(request);
    return Results.Json(reply,statusCode:reply.status=="assigned"?200:reply.status=="busy"?503:reply.status=="version"?409:400);
}).RequireRateLimiting("public");
app.MapPost("/v1/matches/cancel",(CancelRequest request)=>registry.Cancel(request.reservationId??"",request.ticket??"")?Results.NoContent():Results.StatusCode(403)).RequireRateLimiting("public");
app.MapPost("/v1/rooms/{id}/heartbeat",(string id,RoomHeartbeat heartbeat,HttpRequest request)=>
{
    string authorization=request.Headers.Authorization.ToString();
    return authorization.StartsWith("Bearer ",StringComparison.Ordinal)&&registry.Heartbeat(id,authorization.Substring(7),heartbeat)?Results.NoContent():Results.StatusCode(403);
});
app.Run();

public sealed class CancelRequest { public string? reservationId,ticket; }
