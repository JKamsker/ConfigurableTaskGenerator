using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ConfigurableTask;

namespace ConfigurableTaskGenerator.TestApp;

//[CreateConfigurableTask]
//public class SomeGenericArgs<T>
//{
//    public T SomeStuff { get; set; }
//    public T SomeStuff1 { get; set; }

//    public int MyProperty { get; set; }
//    public bool AsyncRan { get; private set; }

//    public SomeGenericArgs<T> WithSomeStuff(T someStuff)
//    {
//        SomeStuff = someStuff;
//        return this;
//    }

//    public async Task<SomeGenericArgs<T>> SomeAsyncOperation(T myParam)
//    {
//        await Task.Delay(10);
//        AsyncRan = true;
//        return this;
//    }
//}


//public partial class SomeGenericService
//{
//    private Task<string> DoSomethingAsync(SomeGenericArgs<string> data, int a)
//    {
//        return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}");
//    }
//}