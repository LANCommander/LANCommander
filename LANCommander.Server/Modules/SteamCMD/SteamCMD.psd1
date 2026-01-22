@{
    RootModule        = 'SteamCMD.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = 'd9e2b2b7-7c4a-4c1c-9c33-93d5f65a4a21'
    Author            = 'Pat Hartl'
    CompanyName       = 'LANCommander'
    Description       = 'Install and update Steam games using SteamCMD'
    PowerShellVersion = '7.0'

    FunctionsToExport = @('Install-SteamGame')
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}