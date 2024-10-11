# LANCommander.Server.Data
This is the main assembly for defining database schema and the database context for Entity Framework Core.

## Adding Migrations
Migrations should only be added to the database provider projects. The following commands can be used to add a new migration:

```ps
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.SQLite --startup-project LANCommander.Server
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.MySQL --startup-project LANCommander.Server
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.PostgreSQL --startup-project LANCommander.Server
```

A migration should be added to every database provider. Note that when running each of these, `Settings.yml` needs to be updated with the correct connection strings.