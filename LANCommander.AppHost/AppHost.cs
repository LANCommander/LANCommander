using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var server = builder
    .AddProject<LANCommander_Server>("Server")
    .WithExternalHttpEndpoints();

var launcher = builder
    .AddProject<LANCommander_Launcher>("Launcher")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();