using client;
using client.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<PowerLimitMonitorService>();
builder.Services.AddSingleton<PowerLimitState>();
builder.Services.AddSingleton<IEngineCommunicationFactory, EngineCommunicationFactory>();
builder.Services.AddSingleton<EnergyMeasurementService>();

var host = builder.Build();
host.Run();