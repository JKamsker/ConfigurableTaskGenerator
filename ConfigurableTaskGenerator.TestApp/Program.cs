using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConfigurableTaskGenerator.TestApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var a = new SomeService();
        await a.DoSomethingAsync(1)
            .SomeAsyncOperation("abc")
            .WithSomeStuff1("xyz")
           ;
        //var b = await a.DoSomethingAsync().WithSomeStuff("abc");

        await new Test();
    }
}

public class Test
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter<string> GetAwaiter()
    {
        return Task.FromResult("abc").GetAwaiter();
    }
}
