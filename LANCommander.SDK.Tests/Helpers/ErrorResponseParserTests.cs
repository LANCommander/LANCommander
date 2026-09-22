using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.Helpers;

public class ErrorResponseParserTests
{
    // ── Parse ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenBodyIsCamelCase_PopulatesDetails()
    {
        // The server's minimal APIs serialize with web defaults, so the launcher receives camelCase.
        const string body = """
            {
              "error": "UserRegistrationFailed",
              "message": "User registration failed.",
              "details": [
                { "key": "PasswordRequiresDigit", "message": "Passwords must have at least one digit ('0'-'9')." }
              ]
            }
            """;

        var result = ErrorResponseParser.Parse(body, 400, "Bad Request");

        Assert.Equal("UserRegistrationFailed", result.Error);
        Assert.Equal("User registration failed.", result.Message);
        Assert.Equal(
            "Passwords must have at least one digit ('0'-'9').",
            Assert.Single(result.DetailsMessages));
    }

    [Fact]
    public void Parse_WhenBodyIsPascalCase_PopulatesDetails()
    {
        const string body = """
            {
              "Error": "UserRegistrationFailed",
              "Message": "User registration failed.",
              "Details": [ { "Key": "InvalidUserName", "Message": "Username is unavailable" } ]
            }
            """;

        var result = ErrorResponseParser.Parse(body, 400, "Bad Request");

        Assert.Equal("UserRegistrationFailed", result.Error);
        Assert.Equal("Username is unavailable", Assert.Single(result.DetailsMessages));
    }

    [Fact]
    public void Parse_WhenBodyHasMultipleDetails_PreservesOrder()
    {
        const string body = """
            {
              "error": "UserRegistrationFailed",
              "message": "User registration failed.",
              "details": [
                { "key": "PasswordTooShort", "message": "Passwords must be at least 12 characters." },
                { "key": "PasswordRequiresDigit", "message": "Passwords must have at least one digit ('0'-'9')." },
                { "key": "PasswordRequiresUpper", "message": "Passwords must have at least one uppercase ('A'-'Z')." }
              ]
            }
            """;

        var result = ErrorResponseParser.Parse(body, 400, "Bad Request");

        Assert.Equal(
            new[]
            {
                "Passwords must be at least 12 characters.",
                "Passwords must have at least one digit ('0'-'9').",
                "Passwords must have at least one uppercase ('A'-'Z').",
            },
            result.DetailsMessages.ToArray());
    }

    [Fact]
    public void Parse_WhenBodyIsBareJsonString_UsesItAsTheMessage()
    {
        // Some server code paths respond with TypedResults.BadRequest(ex.Message).
        var result = ErrorResponseParser.Parse("\"Something went wrong\"", 400, "Bad Request");

        Assert.Equal("Something went wrong", result.Message);
        Assert.Empty(result.DetailsMessages ?? []);
    }

    [Fact]
    public void Parse_WhenBodyIsEmpty_FallsBackToTheStatusLine()
    {
        var result = ErrorResponseParser.Parse("   ", 401, "Unauthorized");

        Assert.Equal("The server responded with 401 Unauthorized.", result.Message);
    }

    [Fact]
    public void Parse_WhenBodyIsNotJson_UsesTheRawBodyAsTheMessage()
    {
        var result = ErrorResponseParser.Parse("<html>502 Bad Gateway</html>", 502, "Bad Gateway");

        Assert.Equal("<html>502 Bad Gateway</html>", result.Message);
    }

    [Fact]
    public void Parse_WhenBodyIsJsonOfAnUnrelatedShape_DoesNotReturnAnEmptyErrorResponse()
    {
        // This is the regression that produced "Could not register user": the payload binds
        // successfully but leaves every property null, so it must not be treated as usable.
        var result = ErrorResponseParser.Parse("""{"someOtherField":"value"}""", 400, "Bad Request");

        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Parse_WhenBodyIsVeryLong_TruncatesTheMessage()
    {
        var body = new string('x', 2000);

        var result = ErrorResponseParser.Parse(body, 500, "Internal Server Error");

        Assert.True(result.Message.Length < body.Length);
        Assert.EndsWith("…", result.Message);
    }

    // ── ParseAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ReadsTheResponseBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            ReasonPhrase = "Bad Request",
            Content = new StringContent(
                """{"error":"UserRegistrationFailed","message":"User registration failed.","details":[{"key":"PasswordTooShort","message":"Passwords must be at least 12 characters."}]}""",
                Encoding.UTF8,
                "application/json"),
        };

        var result = await ErrorResponseParser.ParseAsync(response);

        Assert.Equal("Passwords must be at least 12 characters.", Assert.Single(result.DetailsMessages));
    }

    [Fact]
    public async Task ParseAsync_WhenResponseHasNoBody_StillReturnsAMessage()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = "Unauthorized",
        };

        var result = await ErrorResponseParser.ParseAsync(response);

        Assert.Equal("The server responded with 401 Unauthorized.", result.Message);
    }
}
