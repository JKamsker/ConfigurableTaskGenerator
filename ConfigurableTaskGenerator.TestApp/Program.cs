using Microsoft.Extensions.DependencyInjection;

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


        //await new HttpService().PostAsync("https://www.google.com")
        //    .WithBody(new { a = "yolo" })

        //    ;


        /*
         curl -X 'POST' \
              'https://localhost:7233/login' \
              -H 'Content-Type: application/json' \
              -d '{
              "userName": "admin",
              "password": "password"
            }'
         */

        //var service = new HttpService();
        //var response = await service.PostAsync("https://localhost:7233/login")
        //    .WithBody(new { userName = "admin", password = "password" });

        //setup httpservice using dependency injection

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