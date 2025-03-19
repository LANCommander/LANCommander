using System.Text;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Utilities;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class SaveServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task ArchiveCreationWorks()
    {
        try
        {
            File.WriteAllText("test.txt", "Hello World!");

            using (var archive = ZipArchive.Create())
            {
                archive.AddEntry("test.txt", "test.txt");

                archive.SaveTo("test.zip", CompressionType.None);
                
                var fileInfo = new FileInfo("test.zip");
                
                fileInfo.Length.ShouldBe(126);
            }

            using (Stream stream = File.OpenRead("test.zip"))
            using (var reader = ReaderFactory.Open(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    reader.Entry.Key.ShouldBe("test.txt");
                }
            }
        }
        finally
        {
            if (File.Exists(@"test.txt"))
                File.Delete(@"test.txt");
            
            if (File.Exists(@"test.zip"))
                File.Delete(@"test.zip");
        }
    }
    
    [Fact]
    public async Task ArchiveCreationToStreamWorks()
    {
        try
        {
            File.WriteAllText("test.txt", "Hello World!");
            File.WriteAllText("test.txt", "Hello World!");

            using (var ms = new MemoryStream())
            using (var archive = ZipArchive.Create())
            {
                archive.AddEntry("test.txt", "test.txt");

                archive.SaveTo(ms, CompressionType.None);

                ms.Position = 0;

                ms.Length.ShouldBe(126);

                using (var reader = ReaderFactory.Open(ms))
                {
                    while (reader.MoveToNextEntry())
                    {
                        reader.Entry.Key.ShouldBe("test.txt");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            if (File.Exists(@"test.txt"))
                File.Delete(@"test.txt");
            
            if (File.Exists(@"test.zip"))
                File.Delete(@"test.zip");
        }
    }
    
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

            SDK.Models.GameSave uploadedSave;

            #region Pack and Upload Save

            long packedSize = 0;

            using (var savePacker = new SavePacker(gameInstallDirectory))
            {
                if (manifest?.SavePaths.Any() ?? false)
                    savePacker.AddPaths(manifest.SavePaths);

                await savePacker.AddManifestAsync(manifest);

                var stream = await savePacker.PackAsync();

                using (var reader = ReaderFactory.Open(stream, new ReaderOptions()
                       {
                           LeaveStreamOpen = true,
                       }))
                {
                    var savePath = manifest.SavePaths.First();
                    var entries = new List<string>();

                    while (reader.MoveToNextEntry())
                    {
                        entries.Add(reader.Entry.Key);
                    }

                    foreach (var file in randomFiles)
                        entries.ShouldContain($"Files/{savePath.Id}/save/{file}");

                    entries.ShouldContain(ManifestHelper.ManifestFilename);
                    entries.Count.ShouldBe(11);
                }

                stream.Position = 0;

                packedSize = stream.Length;

                uploadedSave = await Client.Saves.UploadAsync(stream, manifest);
            }

            #endregion

            var createdSavePath = game.SavePaths!.First();

            var saves = await saveService.GetAsync();

            saves.Count.ShouldBe(1);

            var uploadedSavePath = await saveService.GetSavePathAsync(game.Id, user.Id);

            var exists = File.Exists(uploadedSavePath);

            exists.ShouldBeTrue();

            // Check file sizes
            var fileInfo = new FileInfo(uploadedSavePath);

            fileInfo.Length.ShouldBe(uploadedSave.Size);
            fileInfo.Length.ShouldBe(packedSize);

            // Check contents of file
            using (var fs = File.OpenRead(uploadedSavePath))
            using (var reader = ReaderFactory.Open(fs, new ReaderOptions()
                   {
                       LeaveStreamOpen = true,
                   }))
            {
                var savePath = manifest.SavePaths.First();
                var entries = new List<string>();

                while (reader.MoveToNextEntry())
                {
                    entries.Add(reader.Entry.Key);
                }

                foreach (var file in randomFiles)
                    entries.ShouldContain($"Files/{savePath.Id}/save/{file}");

                entries.ShouldContain(ManifestHelper.ManifestFilename);
                entries.Count.ShouldBe(11);
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            Directory.Delete(installDirectory, true);
            Directory.Delete(tempPath, true);
        }
    }
}