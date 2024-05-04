# ConfigurableTaskGenerator

## ⚠ Prealpha Notice
**⚠ Warning:** This library is currently in a *prealpha* stage. It is considered highly experimental and may be significantly unstable. Bugs are likely to occur, and future changes might not be backward compatible. Use this library in production environments with caution, and please contribute by reporting any issues you encounter.

## Introduction
The ConfigurableTaskGenerator library simplifies the creation of fluent APIs for asynchronous C# methods, allowing for an elegant and readable method chaining. This enhancement lets you write more intuitive code while dealing with asynchronous operations.

## Features
- **Fluent API Generation:** Automatically generate fluent interfaces for asynchronous methods.
- **Easy Integration:** Seamlessly integrates with existing C# projects and simplifies complex async code.
- **Customizable Task Wrappers:** Wraps async methods with custom fluent interfaces, enhancing code readability and maintainability.

## Installation
(TODO) Include the library in your project by adding it to your package manager or including it directly in your project files.

## Usage Example
The following example demonstrates how you can use the `ConfigurableTaskGenerator` to chain asynchronous operations fluently:

Usage:
```csharp
    var service = new SomeService();
    var result = await service
        .DoSomethingAsync(1)
        .SomeAsyncOperation("abc")
        .WithMyProperty(2)
        .WithSomeStuff("xyz")
        ;
```

User Code:
```csharp
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
    private Task<string> DoSomethingAsync(SomeArgs data, int a)
    {
        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}");
    }
}
```

Generated Code:
```csharp
public partial class SomeService
{
    private Func<SomeService, SomeArgs> _someArgsFactory = _ => new SomeArgs();
    public SomeArgsAwaiter<string> DoSomething()
    {
        return new SomeArgsAwaiter<string>(_someArgsFactory(this),args => DoSomething(args));
    }
}  

public class SomeArgsAwaiter<T>
{
    private readonly SomeArgs _args;
    private readonly Func<SomeArgs, Task<T>> _taskFactory;
    private readonly List<Func<Task>> _taskGenerators = new List<Func<Task>>();

    public SomeArgsAwaiter(Func<SomeArgs, Task<T>> taskFactory)
        : this(new SomeArgs(), taskFactory)
    {
    }

    public SomeArgsAwaiter(SomeArgs args, Func<SomeArgs, Task<T>> taskFactory)
    {
        _args = args;
        _taskFactory = taskFactory;
    }

    private async Task<T> AwaitAsync()
    {
        foreach (var taskGenerator in _taskGenerators)
        {
            await taskGenerator();
        }
        return await _taskFactory(_args);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter<T> GetAwaiter()
    {
        return AwaitAsync().GetAwaiter();
    }

    public SomeArgsAwaiter<T> WithSomeStuff(string someStuff)
    {
        _args.WithSomeStuff(someStuff);
        return this;
    }
    public SomeArgsAwaiter<T> SomeAsyncOperation(string myParam)
    {
        Func<Task> taskFactory = async () => await _args.SomeAsyncOperation(myParam);
        _taskGenerators.Add(taskFactory);
        return this;
    }
    public SomeArgsAwaiter<T> WithSomeStuff1(string someStuff1)
    {
        _args.SomeStuff1 = someStuff1;
        return this;
    }
    public SomeArgsAwaiter<T> WithMyProperty(int myProperty)
    {
        _args.MyProperty = myProperty;
        return this;
    }
    public static implicit operator SomeArgsAwaiter<T>(Task<T> task)
    {
        return new SomeArgsAwaiter<T>(new SomeArgs(), _ => task);
    }
}
```
