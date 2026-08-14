var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "OK");
app.MapGet("/args", () => string.Join(";", args));

app.Run();
