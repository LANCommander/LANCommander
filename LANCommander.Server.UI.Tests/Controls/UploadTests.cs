using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class UploadTests : ControlsTestContext
{
    [Fact]
    public void RendersAFileInputInsideTheDropArea()
    {
        var upload = Render<Upload>(p => p.Add(x => x.Action, "/Upload/File").Add(x => x.Multiple, true).Add(x => x.Accept, ".zip"));

        var input = upload.Find("label.lc-upload-drop input[type=file]");

        Assert.True(input.HasAttribute("multiple"));
        Assert.Equal(".zip", input.GetAttribute("accept"));
        Assert.Equal(input.Id, upload.Find("label.lc-upload-drop").GetAttribute("for"));
    }

    [Fact]
    public void Disabled_DisablesTheInput()
    {
        var upload = Render<Upload>(p => p.Add(x => x.Action, "/Upload/File").Add(x => x.Disabled, true));

        Assert.True(upload.Find("input[type=file]").HasAttribute("disabled"));
        Assert.Contains("lc-upload-drop-disabled", upload.Find("label").ClassList);
    }

    [Fact]
    public async Task Progress_IsListed_ThenCompletionIsReported()
    {
        UploadedFile? uploaded = null;
        var completed = false;

        var upload = Render<Upload>(p => p
            .Add(x => x.Action, "/Upload/File")
            .Add(x => x.OnUploaded, f => uploaded = f)
            .Add(x => x.OnCompleted, () => completed = true));

        await upload.InvokeAsync(() => upload.Instance.OnStarted("upload-0", "arena.zip", 2048));
        await upload.InvokeAsync(() => upload.Instance.OnProgress("upload-0", 1024, 2048));

        Assert.Equal("arena.zip", upload.Find(".lc-upload-file-name").TextContent);
        Assert.Equal(50, upload.Instance.Files[0].Percent);
        Assert.False(completed);

        await upload.InvokeAsync(() => upload.Instance.OnFinished("upload-0", true, null));

        Assert.True(completed);
        Assert.Equal(UploadFileStatus.Done, uploaded?.Status);
        Assert.Single(upload.FindAll(".lc-upload-file-done"));
    }

    [Fact]
    public async Task Failure_IsReportedWithTheServerMessage()
    {
        UploadedFile? failed = null;

        var upload = Render<Upload>(p => p.Add(x => x.Action, "/Upload/File").Add(x => x.OnError, f => failed = f));

        await upload.InvokeAsync(() => upload.Instance.OnStarted("upload-0", "arena.zip", 2048));
        await upload.InvokeAsync(() => upload.Instance.OnFinished("upload-0", false, "Destination path does not exist."));

        Assert.Equal("Destination path does not exist.", failed?.Error);
        Assert.Equal("Destination path does not exist.", upload.Find(".lc-upload-file-failed .lc-upload-file-name").GetAttribute("title"));
    }

    [Fact]
    public async Task CompletedFires_OnlyOnceEveryUploadHasFinished()
    {
        var completed = 0;

        var upload = Render<Upload>(p => p.Add(x => x.Action, "/Upload/File").Add(x => x.OnCompleted, () => completed++));

        await upload.InvokeAsync(() => upload.Instance.OnStarted("upload-0", "a.zip", 1));
        await upload.InvokeAsync(() => upload.Instance.OnStarted("upload-1", "b.zip", 1));
        await upload.InvokeAsync(() => upload.Instance.OnFinished("upload-0", true, null));

        Assert.Equal(0, completed);

        await upload.InvokeAsync(() => upload.Instance.OnFinished("upload-1", true, null));

        Assert.Equal(1, completed);
    }
}
