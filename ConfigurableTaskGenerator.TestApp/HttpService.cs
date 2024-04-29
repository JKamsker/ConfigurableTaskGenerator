using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ConfigurableTask;

namespace ConfigurableTaskGenerator.TestApp;

[CreateConfigurableTask]
public class HttpArgs
{
    public string Url { get; set; }
    public string Method { get; set; }
    public string Body { get; set; }
    public string ContentType { get; set; }
    public string Accept { get; set; }
    public string Authorization { get; set; }

    public HttpArgs WithUrl(string url)
    {
        Url = url;
        return this;
    }
}

public partial class HttpService
{
    public static HttpService Instance { get; } = new HttpService();

    private Task<string> SendAsync(HttpArgs data, HttpMethod method, string url)
    {
        return Task.FromResult($"Sending {data.Method} request to {data.Url}");
    }
}
