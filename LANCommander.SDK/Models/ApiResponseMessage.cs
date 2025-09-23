using System.Net.Http;

namespace LANCommander.SDK.Models;

public class ApiResponseMessage<TResult> : HttpResponseMessage
    where TResult : class
{
    public TResult Data { get; set; }
    public string ErrorMessage { get; set; }
}