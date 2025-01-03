using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var dbType = builder.Configuration.GetValue("DatabaseType", "SQLite");

var server = builder.AddProject<Projects.LANCommander_Server>("lancommander-server");

switch (dbType)
{
    case "PostgreSQL":
        ConfigurePostgres(builder, server);
        break;

    case "MySQL":
        ConfigureMySQL(builder, server);
        break;
}

builder.Build().Run();

static void ConfigurePostgres(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> server)
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

static void ConfigureMySQL(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> server)
{
    var mysql = builder.AddMySql("mysql")
                .WithDataVolume()
                .WithPhpMyAdmin()
                .AddDatabase("lancommander");
    server
        .WithReference(mysql)
        .WaitFor(mysql)
        .WithEnvironment("DatabaseProvider", "MySQL");
}