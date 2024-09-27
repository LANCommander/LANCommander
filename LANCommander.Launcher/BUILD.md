# Running
Some changes might need to be made in order to target a server properly.

## Project Versions
Both `LANCommander.Launcher` and `LANCommander.SDK` should have their versions changed if you want to communicate to a server properly.

Add this to the first `<PropertyGroup>` in each `.csproj`:
```xml
<Version>0.9.0</Version>
```

# Package JS
```ps
npm install
npm run package
```
This will package everything into `wwwroot/bundle.js`

# Data Migrations
To add a new migration, it has to be added to the data project:
```ps
add-migration <MigrationName> -StartupProject LANCommander.Launcher.Data
```