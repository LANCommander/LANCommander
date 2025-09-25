using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class GameClientTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    /*[Fact]
    public async Task ImportMetadataExportShouldWorkAsync()
    {
        var importer = GetService<ImportService<Server.Data.Models.Game>>();
        
        var tempPath = await EnsureStorageLocationsExistAsync();

        var game = await importer.ImportFromLocalFileAsync(Path.Combine("Files", "lotrbfme2.lcx"));

        try
        {
            game.Title.ShouldBe("The Lord of the Rings: The Battle for Middle-earth II - The Rise of the Witch-king");
            game.SortTitle.ShouldBe("Lord of the Rings: The Battle for Middle-earth II - The Rise of the Witch-king");
            game.Description.ShouldBe(
                "In this add-on for The Lord of the Rings: The Battle for Middle-Earth II it is your goal to lead the great armies of the Witch-King called the Angmar to victory. But there are also other mentionable additions and changes which come with this add-on:\n\nAll six factions from the main game now have access to new units and heroes like Prince Brand a Dwarven captain or the Uruk Deathbringers on the side of Isengard which are powerful two-handed swords-fighters.");
            game.Type.ShouldBe(GameType.StandaloneExpansion);
            game.ReleasedOn.Value.Year.ShouldBe(2006);
            game.ReleasedOn.Value.Month.ShouldBe(11);
            game.ReleasedOn.Value.Day.ShouldBe(28);
            game.Singleplayer.ShouldBeTrue();

            game.Genres.ShouldContain(g => g.Name == "Real Time Strategy (RTS)");

            game.Tags.ShouldContain(t => t.Name == "rts");
            game.Tags.ShouldContain(t => t.Name == "multiplayer");
            game.Tags.ShouldContain(t => t.Name == "fantasy");

            game.Publishers.ShouldContain(c => c.Name == "Electronic Arts");

            game.Developers.ShouldContain(c => c.Name == "EA Los Angeles");

            game.Collections.ShouldContain(c => c.Name == "Vintage Gaming");
            game.Collections.ShouldContain(c => c.Name == "Open Sauce");

            foreach (var action in game.Actions)
            {
                if (action.SortOrder == 0)
                {
                    action.Name.ShouldBe("Play Battle for Middle-earth II: Rise of the Witch King");
                    action.Arguments.ShouldBe("-xres 1920 -yres 1080");
                    action.Path.ShouldBe("lotrbfme2ep1.exe");
                    action.WorkingDirectory.ShouldBe("{InstallDir}");
                    action.PrimaryAction.ShouldBeTrue();
                }
                else
                {
                    action.SortOrder.ShouldBe(1);
                    action.Name.ShouldBe("World Builder");
                    action.Arguments.ShouldBeNullOrEmpty();
                    action.Path.ShouldBe("Worldbuilder.exe");
                    action.WorkingDirectory.ShouldBe("{InstallDir}");
                    action.PrimaryAction.ShouldBeFalse();
                }
            }

            var lanMultiplayer = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.LAN);
            var onlineMultiplayer = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Online);

            lanMultiplayer.MinPlayers.ShouldBe(2);
            lanMultiplayer.MaxPlayers.ShouldBe(8);
            lanMultiplayer.Description.ShouldBeNullOrEmpty();
            lanMultiplayer.NetworkProtocol.ShouldBe(NetworkProtocol.TCPIP);

            // Some multiplayer modes are only importing as LAN. This is a bug.
            
            // onlineMultiplayer.MinPlayers.ShouldBe(2);
            // onlineMultiplayer.MaxPlayers.ShouldBe(8);
            // onlineMultiplayer.Description.ShouldBeNullOrEmpty();
            // onlineMultiplayer.NetworkProtocol.ShouldBe(NetworkProtocol.TCPIP);

            foreach (var path in game.SavePaths)
            {
                switch (path.Id.ToString())
                {
                    case "201fcc6b-eb02-4617-85f7-649e3d07a0b0":
                        path.Type.ShouldBe(SavePathType.File);
                        path.Path.ShouldBe("options.ini");
                        path.WorkingDirectory.ShouldBe(
                            "%APPDATA%\\My The Lord of the Rings, The Rise of the Witch-king Files");
                        path.IsRegex.ShouldBeFalse();
                        break;

                    case "7662b2b7-f291-4146-aaa3-0866e80567b5":
                        path.Type.ShouldBe(SavePathType.File);
                        path.Path.ShouldBe("options.ini");
                        path.WorkingDirectory.ShouldBe(
                            "%APPDATA%\\My The Lord of the Rings, The Rise of the Witch-king Files");
                        path.IsRegex.ShouldBeFalse();
                        break;

                    case "60b94122-5c7c-4083-a265-d7221ae5ced9":
                        path.Type.ShouldBe(SavePathType.File);
                        path.Path.ShouldBe("save");
                        path.WorkingDirectory.ShouldBe("%APPDATA%\\My Battle for Middle-earth(tm) II Files");
                        path.IsRegex.ShouldBeFalse();
                        break;

                    case "bf3b6fa2-35a0-48ed-a036-a4d9ce5c04e5":
                        path.Type.ShouldBe(SavePathType.File);
                        path.Path.ShouldBe("options.ini");
                        path.WorkingDirectory.ShouldBe(
                            "%APPDATA%\\My The Lord of the Rings, The Rise of the Witch-king Files");
                        path.IsRegex.ShouldBeFalse();
                        break;
                }
            }

            foreach (var script in game.Scripts)
            {
                switch (script.Id.ToString())
                {
                    case "2c24fdfd-22e1-4cbb-b650-147dafe27622":
                        script.Type.ShouldBe(ScriptType.NameChange);
                        script.Name.ShouldBe("Name Change Script");
                        script.Description.ShouldBe(
                            "Recreates the NetworkPrefs.ini file in both game appdata directories using the new player's name. The name must be only 10 characters long and must be separated by _00.");
                        script.RequiresAdmin.ShouldBeFalse();
                        break;

                    case "48780389-3ede-44d6-8c7e-450bffc10f76":
                        script.Type.ShouldBe(ScriptType.KeyChange);
                        script.Name.ShouldBe("Key Change Script");
                        script.Description.ShouldBe("Changes the CD key stored in the registry");
                        script.RequiresAdmin.ShouldBeFalse();
                        break;

                    case "99f1c2a5-dcb3-44b2-b96e-0d4dacaf52eb":
                        script.Type.ShouldBe(ScriptType.Install);
                        script.Name.ShouldBe("Install");
                        script.Description.ShouldBeNullOrEmpty();
                        script.RequiresAdmin.ShouldBeFalse();
                        break;

                    case "4be2cccf-9a53-43a1-9c55-5e975da05381":
                        script.Type.ShouldBe(ScriptType.Uninstall);
                        script.Name.ShouldBe("Uninstall");
                        script.Description.ShouldBeNullOrEmpty();
                        script.RequiresAdmin.ShouldBeFalse();
                        break;
                }
            }

            foreach (var media in game.Media)
            {
                switch (media.Id.ToString())
                {
                    case "e5c696cf-ec79-4632-a454-8128c8863324":
                        media.FileId.ToString().ShouldBe("d766c0ac-cb56-45e9-a2aa-0d4d5d24b5b9");
                        media.Name.ShouldBeNullOrEmpty();
                        media.Type.ShouldBe(MediaType.Icon);
                        media.SourceUrl.ShouldBe("''");
                        media.MimeType.ShouldBe("image/png");
                        media.Crc32.ShouldBeNullOrEmpty();
                        break;

                    case "e4dd517c-44b4-4ba4-b884-0538dd9d1206":
                        media.FileId.ToString().ShouldBe("f29463d1-2c7c-4ea4-8268-95bd94baa0c4");
                        media.Name.ShouldBeNullOrEmpty();
                        media.Type.ShouldBe(MediaType.Cover);
                        media.SourceUrl.ShouldBe(
                            "https://cdn2.steamgriddb.com/grid/85837c77b7e43da5acd2976e4c8ed83a.png");
                        media.MimeType.ShouldBe("image/png");
                        media.Crc32.ShouldBeNullOrEmpty();
                        break;

                    case "b1b0342f-9b19-4bf5-9c1d-e78d4f83e6ce":
                        media.FileId.ToString().ShouldBe("9239df30-0152-42ab-bc39-059b2de50309");
                        media.Name.ShouldBeNullOrEmpty();
                        media.Type.ShouldBe(MediaType.Background);
                        media.SourceUrl.ShouldBe(
                            "https://cdn2.steamgriddb.com/hero/a81fd14f84d7df07fbc3c49bcd9e705f.jpg");
                        media.MimeType.ShouldBe("image/jpg");
                        media.Crc32.ShouldBeNullOrEmpty();
                        break;

                    case "9b389c27-bed1-4b95-910e-8ea7e65eedfa":
                        media.FileId.ToString().ShouldBe("668c5d19-dbe5-4e39-a387-ceefb52b6e48");
                        media.Name.ShouldBeNullOrEmpty();
                        media.Type.ShouldBe(MediaType.Logo);
                        media.SourceUrl.ShouldBe(
                            "https://cdn2.steamgriddb.com/logo/a787f02ed34fd886eb6d49e60d9c9120.png");
                        media.MimeType.ShouldBe("image/png");
                        media.Crc32.ShouldBeNullOrEmpty();
                        break;

                    case "cb42790c-3126-4f09-b482-3e4cb5d2a187":
                        media.FileId.ToString().ShouldBe("28e22c66-4eab-442a-9d19-688cbcfb6ebe");
                        media.Name.ShouldBeNullOrEmpty();
                        media.Type.ShouldBe(MediaType.Manual);
                        media.SourceUrl.ShouldBe("''");
                        media.MimeType.ShouldBe("application/pdf");
                        media.Crc32.ShouldBeNullOrEmpty();
                        break;
                }
            }
        }
        finally
        {
            Directory.Delete(tempPath, true);
        }
    }*/
}