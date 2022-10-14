using Nice.HttpProxy;
using Nice.HttpProxy.Abstractions;
using System;
using System.Diagnostics;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpForwarder(options => {
    options.MaxRetries = 3;
    options.ShadowSendExceptionHandler = async (url, times,request, error) =>
    {
        await global::System.Console.Out.WriteLineAsync($"地址:{url}的请求发生第{times}次异常 原因:{error}");
        await ValueTask.CompletedTask;
    };
});
var app = builder.Build();

// Configure the HTTP request pipeline.


app.Run(async (ctx) =>
{
    ctx.Request.EnableBuffering();
    var forward = ctx.RequestServices.GetRequiredService<IHttpForwarder>();
    await forward.SendAsync(ctx, new[] { "http://demo1.example.com", "http://demo2.example.com" }, (u, m) =>
    {
        global::System.Console.WriteLine("此处可以调整请求相关参数，注意：一次修改主请求和影子请求都生效(注意不要重复设置)");
        return ValueTask.CompletedTask;
    });
});

app.Run();
Console.WriteLine( "");
