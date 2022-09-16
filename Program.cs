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

    var (contentType, stream) = await ChannelService.ReadStream(channelId);
    ctx.Response.ContentType = contentType;
    await stream.CopyToAsync(ctx.Response.Body);
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

    await ChannelService.WriteStream(channelId, ctx.Request.ContentType ?? "text/plain", ctx.Request.Body);
    ctx.Response.StatusCode = 200;
    await ctx.Response.CompleteAsync();
});

app.Run();
