using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PokerToolkit.Web;
using PokerToolkit.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One shared copy of the saved ranges for the whole app.
builder.Services.AddScoped<RangeStore>();

await builder.Build().RunAsync();
