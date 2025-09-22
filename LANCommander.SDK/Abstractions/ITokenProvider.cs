using System.Threading.Tasks;

namespace LANCommander.SDK.Abstractions;

public interface ITokenProvider
{
    void SetToken(string token);
    string GetToken();
}