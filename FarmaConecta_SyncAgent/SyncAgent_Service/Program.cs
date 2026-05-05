using SyncAgent_Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FarmaConecta SyncAgent Service";
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<StateManager>();
builder.Services.AddTransient<ErpExtractor>();
builder.Services.AddTransient<SyncApiClient>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
