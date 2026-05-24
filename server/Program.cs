using Microsoft.AspNetCore.SignalR;
using server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<GameService>();
builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});
builder.Services.AddSignalR();

var app = builder.Build();
app.UseRouting();
app.UseCors("ClientCors");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapHub<SignalRService>("/chessHub");
app.MapGet("/ping", async (GameService gameService) =>
{
    await gameService.Ping("dupa");
    return Results.Ok(new { ok = true });
});

app.Run();