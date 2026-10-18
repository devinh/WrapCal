using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using WrapCal.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Routes>("#app");
// PageTitle only reaches the document head if the outlet is mounted there.
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

await builder.Build().RunAsync();
