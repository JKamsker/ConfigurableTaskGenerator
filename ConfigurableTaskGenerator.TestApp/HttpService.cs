using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConfigurableTask;

using Microsoft.Extensions.DependencyInjection;

namespace ConfigurableTaskGenerator.TestApp;

public class HttpExample
{
    public static async Task Run()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<HttpService>(conf =>
        {
            conf.BaseAddress = new Uri("https://localhost:7233");
        });

        using var sp = services.BuildServiceProvider();
        var httpService = sp.GetRequiredService<HttpService>();

        var str = await httpService.PostAsync("/login")
            .WithBody(new { userName = "admin", password = "password" })
            //.WithHeader("Content-Type", "application/json")
            //.WithHeader("Authorization", "")
            //.WithBlaBla(1)
            ;
            /*.ReadAsJsonAsync(new { token = "" })*/;


        // var abc = (await (await bla.Do()).Hello());


    }
}


[CreateConfigurableTask]
public class HttpArgs
{
    private readonly HttpService _svc;

    public string Url { get; set; }
    //public int BlaBla { get; set; }



    [SkipSetterGeneration]
    public Dictionary<string, string> Headers { get; set; }
    public object Body { get; set; }

    public HttpArgs WithHeader(string key, string value)
    {
        Headers ??= new Dictionary<string, string>();
        Headers.Add(key, value);
        return this;
    }

    public HttpArgs()
    {

    }

    public HttpArgs(HttpService svc)
    {
        _svc = svc;
    }
}


public partial class HttpService
{
    private readonly HttpClient _client;

    public HttpService(HttpClient client)
    {
        _httpArgsFactory = _ => new(this);
        _client = client;
    }

    private async Task<HttpResponseMessage> PostAsync(HttpArgs args, string url)
    {
        url = string.IsNullOrWhiteSpace(args.Url) ? url : args.Url;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (args.Headers != null)
        {
            foreach (var header in args.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
        if (args.Body != null)
        {
            var json = JsonSerializer.Serialize(args.Body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return await _client.SendAsync(request);
    }
}

public static class HttpArgsAwaiterExtensions
{
    public static async Task<string> ReadAsStringAsync(this HttpArgsAwaiter<HttpResponseMessage> awaiter)
    {
        var response = await awaiter;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ReadAsJsonAsync
    public static async Task<T?> ReadAsJsonAsync<T>(this HttpArgsAwaiter<HttpResponseMessage> awaiter, T shape)
        => await awaiter.ReadAsJsonAsync<T>();

    public static async Task<T?> ReadAsJsonAsync<T>(this HttpArgsAwaiter<HttpResponseMessage> awaiter)
    {
        var response = await awaiter;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<T>();
        return json;
    }
}