using ConfigurableTask;

namespace ConfigurableTaskGenerator.TestApp;

[CreateConfigurableTask]
public class SomeArgs
{
    public string SomeStuff { get; set; }
    public string SomeStuff1 { get; set; }

    public int MyProperty { get; set; }
    public bool AsyncRan { get; private set; }

    public SomeArgs WithSomeStuff(string someStuff)
    {
        SomeStuff = someStuff;
        return this;
    }

    public async Task<SomeArgs> SomeAsyncOperation(string myParam)
    {
        await Task.Delay(10);
        AsyncRan = true;
        return this;
    }
}

public partial class SomeService
{
    // Given
    private Task<string> DoSomething(SomeArgs data)
    {
        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}");
    }

    // Given
    private Task<string> DoSomething1(SomeArgs data, string additionalString)
    {
        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}: {additionalString}");
    }

    private Task<string> DoSomethingAsync(SomeArgs data, int a)
    {
        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}");
    }
}
