using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var usePostgres = builder.Configuration.GetValue("UsePostgres", false);

var server = builder.AddProject<Projects.LANCommander_Server>("lancommander-server");

if (usePostgres)
{
    var postgres = builder.AddPostgres("pg")
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("lancommander");

    server
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("DatabaseProvider", "PostgreSQL");
}

builder.Build().Run();
