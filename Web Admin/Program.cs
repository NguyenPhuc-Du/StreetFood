var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

var defaultFiles = new DefaultFilesOptions();
defaultFiles.DefaultFileNames.Clear();
defaultFiles.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFiles);

var staticFiles = new StaticFileOptions();
staticFiles.OnPrepareResponse = ctx =>
{
    var n = ctx.File.Name;
    if (n.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
};
app.UseStaticFiles(staticFiles);

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/html/loginPage.html"));

app.Run();
