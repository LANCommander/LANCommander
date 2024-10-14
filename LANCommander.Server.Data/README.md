# LANCommander.Server.Data
This is the main assembly for defining database schema and the database context for Entity Framework Core.

## Adding Migrations
Migrations should only be added to the database provider projects. The following commands can be used to add a new migration:

```ps
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.SQLite --startup-project LANCommander.Server -- --database-provider=SQLite --connection-string="Data Source=LANCommander.db;Cache=Shared"
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.MySQL --startup-project LANCommander.Server -- --database-provider=MySQL --connection-string="Server=localhost;Uid=root;Pwd=password;Database=LANCommander"
dotnet ef migrations add <Migration Name> --project LANCommander.Server.Data.PostgreSQL --startup-project LANCommander.Server -- --database-provider=PostgreSQL --connection-string="Host=localhost;Port=5432;Database=LANCommander;User Id=postgre;Password=password"
```

A migration should be added to every database provider. Note that when running each of these, `Settings.yml` needs to be updated with the correct connection strings.

If something with the parsing of the database provider or connection string arguments needs to be looked at, you can add `--debugger` for the application to wait until a debugger is attached.