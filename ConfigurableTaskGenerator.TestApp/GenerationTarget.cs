using System.Runtime.CompilerServices;

namespace ConfigurableTaskGenerator.TestApp;

public class CreateConfigurableTaskAttribute : System.Attribute
{
    public CreateConfigurableTaskAttribute()
    {
    }
}

[CreateConfigurableTask]
public class SomeArgs
{
    public string SomeStuff { get; set; }
    public string SomeStuff1 { get; set; }

    public SomeArgs WithSomeStuff(string someStuff)
    {
        SomeStuff = someStuff;
        return this;
    }

    public async Task<SomeArgs> SomeAsyncOperation(string myParam)
    {
        await Task.Delay(10);
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

    // Generated
    public SomeArgsAwaiter<string> DoSomethingAsync()
    {
        return new SomeArgsAwaiter<string>(DoSomething);
    }

    // Given
    private Task<string> DoSomething1(SomeArgs data, string additionalString)
    {
        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}: {additionalString}");
    }

    // Generated
    public SomeArgsAwaiter<string> DoSomething1Async(string additionalString)
    {
        return new SomeArgsAwaiter<string>(args => DoSomething1(args, additionalString));
    }
}

//// This is generated
//public class SomeArgsAwaiter
//{
//    private readonly SomeArgs _args;
//    private readonly Func<SomeArgs, Task> _taskFactory;

//    // Will only be generated if an empty constructor is present
//    public SomeArgsAwaiter(Func<SomeArgs, Task> taskFactory)
//        : this(new SomeArgs(), taskFactory)
//    {
//    }

//    public SomeArgsAwaiter(SomeArgs args, Func<SomeArgs, Task> taskFactory)
//    {
//        _args = args;
//        _taskFactory = taskFactory;
//    }

//    public async Task AwaitAsync()
//    {
//        await _taskFactory(_args);
//    }

//    public TaskAwaiter GetAwaiter()
//    {
//        return AwaitAsync().GetAwaiter();
//    }

//    // Relayed methods from SomeArgs

//    public SomeArgsAwaiter WithSomeStuff(string someStuff)
//    {
//        _args.SomeStuff = someStuff;
//        return this;
//    }
//}

//public class SomeArgsAwaiter<T>
//{
//    private readonly SomeArgs _args;
//    private readonly Func<SomeArgs, Task<T>> _taskFactory;

   

//    public SomeArgsAwaiter(Func<SomeArgs, Task<T>> taskFactory)
//        : this(new SomeArgs(), taskFactory)
//    {
//    }

//    public SomeArgsAwaiter(SomeArgs args, Func<SomeArgs, Task<T>> taskFactory)
//    {
//        _args = args;
//        _taskFactory = taskFactory;
//    }

//    public async Task<T> AwaitAsync()
//    {
//        foreach (var taskGenerator in _taskGenerators)
//        {
//            await taskGenerator();
//        }

//        return await _taskFactory(_args);
//    }

//    public TaskAwaiter<T> GetAwaiter()
//    {
//        return AwaitAsync().GetAwaiter();
//    }

//    // Relayed methods from SomeArgs

//    public SomeArgsAwaiter<T> WithSomeStuff(string someStuff)
//    {
//        _args.SomeStuff = someStuff;
//        return this;
//    }


//    private List<Func<Task>> _taskGenerators = new();
//    public SomeArgsAwaiter<T> SomeAsyncOperation(string myParam)
//    {
//        Func<Task> taskFactory = async () => await _args.SomeAsyncOperation(myParam);
//        _taskGenerators.Add(taskFactory);
//        return this;
//    }

//    public static implicit operator SomeArgsAwaiter<T>(Task<T> task)
//    {
//        return new SomeArgsAwaiter<T>(new(), _ => task);
//    }
//}