using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConfigurableTaskGenerator.TestApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        //var a = new SomeService();
        //await a.DoSomethingAsync(1)
        //    .SomeAsyncOperation("abc")
        //    .WithSomeStuff1("xyz")
        //   ;
        ////var b = await a.DoSomethingAsync().WithSomeStuff("abc");

        //await new Test();

        await HttpService.Instance.SendAsync(HttpMethod.Get, "https://www.google.com")
            .WithBody("abc")

            ;
    }
}

//// User facing
//public partial class Test1
//{
//    public partial SomeArgs SomeArgsFactory();
//}

//// Generated
//public partial class Test1
//{
//    //public partial SomeArgs SomeArgsFactory()
//    //{
//    //    return new SomeArgs();
//    //}
//}