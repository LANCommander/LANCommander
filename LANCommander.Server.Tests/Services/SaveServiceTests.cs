using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class SaveServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task SaveUploadWorksAsync()
    {
        var gameService = GetService<GameService>();
        var saveService = GetService<GameSaveService>();
        
        var user = await EnsureAdminUserCreatedAsync();
        
        await Client.AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);
        
        var installDirectory = GetTemporaryDirectory();
        var tempPath = await EnsureStorageLocationsExistAsync();

        try
        {
            var game = new Game
            {
                Title = "Test Game",
                SavePaths =
                [
                    new SavePath
                    {
                        Path = "save",
                        WorkingDirectory = "{InstallDir}",
                        Type = SavePathType.File,
                    }
                ]
            };

            game = await gameService.AddAsync(game);
            
            // Mock game install directory
            var sdkGame = await Client.Games.GetAsync(game.Id);

            var gameInstallDirectory = await Client.Games.GetInstallDirectory(sdkGame, installDirectory);
            var manifest = Client.Games.GetManifest(game.Id);
            
            Directory.CreateDirectory(Path.Combine(gameInstallDirectory, ".lancommander"));
            Directory.CreateDirectory(Path.Combine(gameInstallDirectory, "save"));
            await ManifestHelper.WriteAsync(manifest, gameInstallDirectory);

            var randomFiles = new List<string>();

            for (var i = 0; i < 10; i++)
                randomFiles.Add(Path.GetRandomFileName());
            
            foreach (var file in randomFiles)
                await File.WriteAllTextAsync(Path.Combine(gameInstallDirectory, "save", file), file);
            
            var uploadedSave = await Client.Saves.UploadAsync(gameInstallDirectory, game.Id);

            var createdSavePath = game.SavePaths!.First();

            var saves = await saveService.GetAsync();
            
            saves.Count.ShouldBe(1);
            
            var uploadedSavePath = await saveService.GetSavePathAsync(game.Id, user.Id);

            var exists = File.Exists(uploadedSavePath);

            exists.ShouldBeTrue();
            
            var fileInfo = new FileInfo(uploadedSavePath);

            fileInfo.Length.ShouldBe(uploadedSave.Size);

            using (ZipArchive archive = ZipArchive.Open(uploadedSavePath, new ReaderOptions() { ArchiveEncoding = new ArchiveEncoding() { Default = Encoding.UTF8 }}))
            {
                var entries = archive.Entries.ToList();
            }
        }
        finally
        {
            Directory.Delete(installDirectory, true);
            Directory.Delete(tempPath, true);
        }
    }
}