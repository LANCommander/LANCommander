using System.Net.Http;

namespace LANCommander.SDK.Models;

public class ApiResponseMessage<TResult>
    where TResult : class
{
    public HttpResponseMessage Response { get; set; }
    public TResult Data { get; set; }
    public string ErrorMessage { get; set; }
}