using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PanoramicData.SoundPrint.Demo;
using PanoramicData.SoundPrint.Demo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<DemoCatalogue>();
builder.Services.AddScoped<IdentifySession>();
builder.Services.AddSingleton<ExplainLab>();
await builder.Build().RunAsync();
