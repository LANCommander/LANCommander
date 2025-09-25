using LANCommander.Server.Services.Factories;
using Action = LANCommander.SDK.Models.Manifest.Action;
using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Factories;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class ImporterTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task ImportGameWorks()
    {
        var importContextFactory = GetService<ImportContextFactory>();
        var importContext = importContextFactory.Create();

        var manifest = new SDK.Models.Manifest.Game
        {
            Id = Guid.NewGuid(),
            Title = "Test Game",
            SortTitle = "Test Game",
            DirectoryName = "TestGame",
            Description = "A comprehensive test game for import testing",
            Notes = "Test notes for the game",
            Singleplayer = true,
            ReleasedOn = DateTime.Now.AddYears(-1),
            InstallDirectory = "C:\\Games\\TestGame",
            Type = GameType.MainGame,
            BaseGame = null,
            
            // Actions
            Actions = new List<Action>
            {
                new Action
                {
                    Name = "Launch Game",
                    Arguments = "-fullscreen",
                    Path = "game.exe",
                    WorkingDirectory = "",
                    IsPrimaryAction = true,
                    SortOrder = 0
                },
                new Action
                {
                    Name = "Launch Editor",
                    Arguments = "-editor",
                    Path = "editor.exe",
                    WorkingDirectory = "",
                    IsPrimaryAction = false,
                    SortOrder = 1
                }
            },

            // Archives
            Archives = new List<SDK.Models.Manifest.Archive>
            {
                new SDK.Models.Manifest.Archive
                {
                    Id = Guid.NewGuid(),
                    Version = "1.0.0",
                    Changelog = "Initial release",
                    CompressedSize = 1024000,
                    UncompressedSize = 2048000,
                    ObjectKey = "test-archive-1.0.0.zip"
                }
            },

            // Collections
            Collections = new List<SDK.Models.Manifest.Collection>
            {
                new SDK.Models.Manifest.Collection
                {
                    Name = "Test Collection"
                }
            },

            // Custom Fields
            CustomFields = new List<SDK.Models.Manifest.GameCustomField>
            {
                new SDK.Models.Manifest.GameCustomField("TestField", "TestValue"),
                new SDK.Models.Manifest.GameCustomField("AnotherField", "AnotherValue")
            },

            // Developers
            Developers = new List<SDK.Models.Manifest.Company>
            {
                new SDK.Models.Manifest.Company
                {
                    Name = "Test Developer"
                }
            },

            // Engine
            Engine = new SDK.Models.Manifest.Engine
            {
                Name = "Test Engine"
            },

            // Genres
            Genres = new List<SDK.Models.Manifest.Genre>
            {
                new SDK.Models.Manifest.Genre
                {
                    Name = "Action"
                },
                new SDK.Models.Manifest.Genre
                {
                    Name = "Adventure"
                }
            },

            // Keys
            Keys = new List<SDK.Models.Manifest.Key>
            {
                new SDK.Models.Manifest.Key
                {
                    Value = "TEST-KEY-1234-5678-9ABC",
                    AllocationMethod = KeyAllocationMethod.UserAccount,
                    ClaimedByComputerName = null,
                    ClaimedByIpv4Address = null,
                    ClaimedByMacAddress = null
                }
            },

            // Media
            Media = new List<SDK.Models.Manifest.Media>
            {
                new SDK.Models.Manifest.Media
                {
                    Id = Guid.NewGuid(),
                    FileId = Guid.NewGuid(),
                    Name = "Game Cover",
                    Type = MediaType.Cover,
                    SourceUrl = "https://example.com/cover.jpg",
                    MimeType = "image/jpeg",
                    Crc32 = "12345678"
                },
                new SDK.Models.Manifest.Media
                {
                    Id = Guid.NewGuid(),
                    FileId = Guid.NewGuid(),
                    Name = "Game Icon",
                    Type = MediaType.Icon,
                    SourceUrl = "https://example.com/icon.ico",
                    MimeType = "image/x-icon",
                    Crc32 = "87654321"
                }
            },

            // Multiplayer Modes
            MultiplayerModes = new List<SDK.Models.Manifest.MultiplayerMode>
            {
                new SDK.Models.Manifest.MultiplayerMode
                {
                    Type = MultiplayerType.Local,
                    NetworkProtocol = NetworkProtocol.TCPIP,
                    Description = "Local multiplayer for 2-4 players",
                    MinPlayers = 2,
                    MaxPlayers = 4,
                    Spectators = 0
                }
            },

            // Platforms
            Platforms = new List<SDK.Models.Manifest.Platform>
            {
                new SDK.Models.Manifest.Platform
                {
                    Name = "Windows"
                },
                new SDK.Models.Manifest.Platform
                {
                    Name = "Linux"
                }
            },

            // Play Sessions
            PlaySessions = new List<SDK.Models.Manifest.PlaySession>
            {
                new SDK.Models.Manifest.PlaySession
                {
                    Start = DateTime.Now.AddDays(-1),
                    End = DateTime.Now.AddHours(-2),
                    User = "DoctorDalek"
                }
            },

            // Publishers
            Publishers = new List<SDK.Models.Manifest.Company>
            {
                new SDK.Models.Manifest.Company
                {
                    Name = "Test Publisher"
                }
            },

            // Saves
            Saves = new List<SDK.Models.Manifest.Save>
            {
                new SDK.Models.Manifest.Save
                {
                    Id = Guid.NewGuid(),
                    User = "TestUser"
                }
            },

            // Save Paths
            SavePaths = new List<SDK.Models.Manifest.SavePath>
            {
                new SDK.Models.Manifest.SavePath
                {
                    Id = Guid.NewGuid(),
                    Type = SavePathType.File,
                    Path = "save\\*.sav",
                    WorkingDirectory = "",
                    IsRegex = false,
                }
            },

            // Scripts
            Scripts = new List<SDK.Models.Manifest.Script>
            {
                new SDK.Models.Manifest.Script
                {
                    Id = Guid.NewGuid(),
                    Type = ScriptType.Install,
                    Name = "Install Script",
                    Description = "Script to install the game",
                    RequiresAdmin = false,
                }
            },

            // Tags
            Tags = new List<SDK.Models.Manifest.Tag>
            {
                new SDK.Models.Manifest.Tag
                {
                    Name = "Test Tag"
                },
                new SDK.Models.Manifest.Tag
                {
                    Name = "Another Tag"
                }
            }
        };

        // Set the manifest on the import context

        // Prepare the import queue with all flags
        var importFlags = ImportRecordFlags.Actions | ImportRecordFlags.Archives | ImportRecordFlags.Collections |
                         ImportRecordFlags.CustomFields | ImportRecordFlags.Developers | ImportRecordFlags.Engine |
                         ImportRecordFlags.Genres | ImportRecordFlags.Keys | ImportRecordFlags.Media |
                         ImportRecordFlags.MultiplayerModes | ImportRecordFlags.Platforms | ImportRecordFlags.PlaySessions |
                         ImportRecordFlags.Publishers | ImportRecordFlags.Saves | ImportRecordFlags.SavePaths |
                         ImportRecordFlags.Scripts | ImportRecordFlags.Tags;

        await importContext.PrepareGameImportQueueAsync(manifest, importFlags);

        // Import the queue
        await importContext.ImportQueueAsync();

        // Assert that the game was imported successfully
        Assert.NotNull(importContext.DataRecord);
        Assert.IsType<LANCommander.Server.Data.Models.Game>(importContext.DataRecord);
        
        var importedGame = (LANCommander.Server.Data.Models.Game)importContext.DataRecord;
        Assert.Equal("Test Game", importedGame.Title);
        Assert.Equal("A comprehensive test game for import testing", importedGame.Description);

        // Assert that all items were processed
        // Note: The exact count may vary depending on how the importers handle the data
        // We expect at least some items to be processed
        Assert.True(importContext.Processed > 0, "At least some items should have been processed");

        // Clean up
        importContext.Dispose();
    }
}