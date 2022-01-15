using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using MindChat.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://*:5000", "https://*:5001");

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<MindReader>();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services
    .AddSignalR(o =>
    {
        o.MaximumReceiveMessageSize = null; // no limit
        o.EnableDetailedErrors = true;
    })
    .AddMessagePackProtocol();


var app = builder.Build();

app.UseResponseCompression();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

//app.MapBlazorHub();

app.MapRazorPages();
app.MapHub<VideoHub>("/chat", options => {
    options.ApplicationMaxBufferSize = 1024000;
    // Set to 0 for no limit, or to some non-zero value (in bytes) to set a different buffer limit
    options.TransportMaxBufferSize = 0;
});
app.MapFallbackToFile("index.html");


app.Run();
