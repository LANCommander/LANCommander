using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Controls;

public class ChunkUploaderTests : ControlsTestContext
{
    [Fact]
    public void PickingAFile_ShowsIt_AndReportsIt()
    {
        IBrowserFile? picked = null;

        var uploader = Render<ChunkUploader>(p => p.Add(x => x.FileChanged, f => picked = f));

        uploader.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("zip bytes", "arena-blitz.zip"));

        Assert.Equal("arena-blitz.zip", picked?.Name);
        Assert.Equal("arena-blitz.zip", uploader.Find(".lc-upload-file-name").TextContent);
    }

    [Fact]
    public void PickingAFileOfTheWrongType_IsRefused()
    {
        IBrowserFile? picked = null;

        var uploader = Render<ChunkUploader>(p => p.Add(x => x.Accept, ".lcx").Add(x => x.FileChanged, f => picked = f));

        uploader.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("zip bytes", "arena-blitz.zip"));

        Assert.Null(picked);
        Assert.Empty(uploader.FindAll(".lc-upload-file"));
        Assert.Contains(Services.GetRequiredService<Radzen.NotificationService>().Messages, m => m.Summary!.Contains(".lcx"));
    }

    [Fact]
    public void Removing_ClearsThePickedFile()
    {
        IBrowserFile? picked = null;

        var uploader = Render<ChunkUploader>(p => p.Add(x => x.FileChanged, f => picked = f));

        uploader.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("zip bytes", "arena-blitz.zip"));
        uploader.Find(".lc-upload-file button[title=Remove]").Click();

        Assert.Null(picked);
        Assert.Empty(uploader.FindAll(".lc-upload-file"));
    }

    [Fact]
    public void WhileUploading_ShowsProgress_AndLocksTheFile()
    {
        var uploader = Render<ChunkUploader>(p => p.Add(x => x.Status, ChunkUploadStatus.Uploading));

        Assert.NotEmpty(uploader.FindAll(".lc-upload-drop .lc-progress, .lc-upload-drop [role=progressbar]"));
        Assert.True(uploader.Find("input[type=file]").HasAttribute("disabled"));
    }
}
