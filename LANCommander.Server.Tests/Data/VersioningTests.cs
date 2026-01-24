using LANCommander.SDK.Services;
using Shouldly;
using LANCommander.Server.Services;

namespace LANCommander.Server.Tests.Data;

[Collection("Application")]
public class VersioningTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task CreatedByShouldWork()
    {
        // Simple service that's not bound to change much
        var tagService = GetService<TagService>();
        var authenticationClient = GetService<AuthenticationClient>();
            
        var user = await EnsureAdminUserCreatedAsync();
        
        await authenticationClient.AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var response = await Client.Tags.CreateAsync(new SDK.Models.Tag
        {
            Name = "Test Tag",
        });

        var tag = await tagService
            .Include(t => t.CreatedBy)
            .GetAsync(response.Id);
        
        tag.Name.ShouldBe("Test Tag");
        tag.CreatedById.ShouldBe(user.Id);
        tag.CreatedBy.ShouldNotBeNull();
        tag.CreatedBy.UserName.ShouldBe(user.UserName);
        tag.CreatedBy.Id.ShouldBe(user.Id);
    }
}