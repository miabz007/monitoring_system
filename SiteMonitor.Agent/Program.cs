using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteMonitor.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<Worker>();

builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<Worker>());

var host = builder.Build();

var trayApp = new TrayApplication(host);

System.Windows.Forms.Application.Run(trayApp);