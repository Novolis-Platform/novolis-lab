using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Raylib.Abstractions;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Hosting;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Shell;

var builder = RaylibHost.CreateApplicationBuilder(args);
builder.AddRaylib(o =>
{
    o.WindowTitle = "Hello Hosting";
    o.WindowWidth = 960;
    o.WindowHeight = 540;
});
builder.AddRaylibSystem<DemoRenderSystem>();

// Headless-friendly: skip blocking loop when NOVOLIS_RAYLIB_HEADLESS=1
if (string.Equals(Environment.GetEnvironmentVariable(RaylibRuntimeShell.HeadlessEnvironmentVariable), "1", StringComparison.OrdinalIgnoreCase))
{
    await builder.Build().StartAsync();
    await Task.Delay(10);
    return;
}

await builder.Build().RunAsync();

file sealed class DemoRenderSystem : IRenderSystem
{
    public void OnRender(float deltaSeconds, int screenWidth, int screenHeight)
    {
        Graphics.ClearBackground(RaylibColors.RayWhite);
        Graphics.DrawText("Hello from Novolis.Raylib.Hosting", 16, 16, 24, RaylibColors.DarkGray);
    }
}
