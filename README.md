# Nice.HttpProxy
基于Yarp裁剪的支持**请求转发** 和 **影子流量** 的HTTP代理组件

1. Service Register
```csharp
builder.Services.AddHttpForwarder(options => {
    options.MaxRetries = 3;
    options.ShadowSendExceptionHandler = async (url, times,request, error) =>
    {
        await global::System.Console.Out.WriteLineAsync($"地址:{url}的请求发生第{times}次异常 原因:{error}");
        await ValueTask.CompletedTask;
    };
});
```

2. Using `IHttpForwarder`

```csharp
app.Run(async (ctx) =>
{
    ctx.Request.EnableBuffering();
    var forward = ctx.RequestServices.GetRequiredService<IHttpForwarder>();
    await forward.SendAsync(ctx, new[] { "http://demo1.example.com", "http://demo2.example.com" }, (u, m) =>
    {
        Console.WriteLine("此处可以调整请求相关参数，注意：一次修改主请求和影子请求都生效(注意不要重复设置)");
        return ValueTask.CompletedTask;
    });
});
```