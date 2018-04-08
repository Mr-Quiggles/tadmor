﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Hangfire.Logging.LogProviders;
using Hangfire.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Tadmor.Data;
using Tadmor.Services;
using Tadmor.Services.E621;

namespace Tadmor
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                var provider = GetServiceProvider();
                var context = provider.GetService<AppDbContext>();
                await context.Database.MigrateAsync();
                var discord = provider.GetService<DiscordService>();
                await discord.Start();
                var hangfireServer = new BackgroundJobServer(new BackgroundJobServerOptions {WorkerCount = 1});
                await Task.Delay(-1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey();
            }
        }

        public static ServiceProvider GetServiceProvider()
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            var services = new ServiceCollection()
                .Configure<DiscordOptions>(configuration.GetSection(nameof(DiscordOptions)))
                .Configure<E621Options>(configuration.GetSection(nameof(E621Options)))
                .AddLogging(logger => logger.AddConsole())
                .AddDbContext<AppDbContext>(builder => builder.UseSqlite(configuration.GetConnectionString("Main")))
                .AddSingleton(new CommandService(new CommandServiceConfig {DefaultRunMode = RunMode.Async}))
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<IocHangfireJobActivator>()
                .Scan(scan => scan
                    .FromEntryAssembly()
                    .AddClasses(classes => classes.InNamespaces($"{nameof(Tadmor)}.{nameof(Services)}"))
                    .AsSelf()
                    .WithSingletonLifetime())
                .BuildServiceProvider();
            GlobalConfiguration.Configuration.UseSQLiteStorage(configuration.GetConnectionString("Hangfire"));
            GlobalConfiguration.Configuration.UseActivator(services.GetService<IocHangfireJobActivator>());
            GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute {Attempts = 0});
            return services;
        }
    }
}