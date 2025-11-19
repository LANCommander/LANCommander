using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Abstractions;

public interface ITokenProvider
{
    void SetToken(AuthToken token);
    AuthToken GetToken();
}