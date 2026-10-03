var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<BlazorMarkupLab.Components.App>()
    .AddInteractiveServerRenderMode();
app.Run();
