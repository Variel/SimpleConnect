var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var app = builder.Build();

app.UseCors((builder) => {
    builder.SetIsOriginAllowed((origin) => true);
    builder.AllowAnyMethod();
    builder.AllowAnyHeader();
    builder.AllowCredentials();
});

app.MapGet("/channels/{channelId}", async (ctx) => {
    var channelId = ctx.Request.RouteValues["channelId"]?.ToString();
    if (channelId == null)
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.CompleteAsync();
        return;
    }

    var (contentType, data) = await ChannelService.ReadData(channelId);
    ctx.Response.ContentType = contentType;
    await ctx.Response.Body.WriteAsync(data, 0, data.Length);
    await ctx.Response.CompleteAsync();
});

app.MapPost("/channels/{channelId}", async (ctx) => {
    var channelId = ctx.Request.RouteValues["channelId"]?.ToString();
    if (channelId == null)
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.CompleteAsync();
        return;
    }

    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);

    await ChannelService.WriteData(channelId, ctx.Request.ContentType ?? "text/plain", ms.ToArray());
    ctx.Response.StatusCode = 200;
    await ctx.Response.CompleteAsync();
});

app.Run();
