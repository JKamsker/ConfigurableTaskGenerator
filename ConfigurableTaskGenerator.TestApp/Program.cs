using Microsoft.Extensions.DependencyInjection;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConfigurableTaskGenerator.TestApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var service = new SomeService();
        var result = await service
            .DoSomethingAsync(1)
            .SomeAsyncOperation("abc")
            .WithMyProperty(2)
            .WithSomeStuff("xyz")
            ;



        //var genservice = new SomeGenericService();
        //var genresult = await genservice
        //    .DoSomethingAsync(1)
        //    .SomeAsyncOperation("abc")
        //    .WithMyProperty(2)
        //    .WithSomeStuff("xyz")
        //    ;



        await HttpExample.Run();

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