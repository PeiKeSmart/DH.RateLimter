using DH.RateLimter;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { 
        Title = "DH.RateLimter 示例 API", 
        Version = "v1",
        Description = "演示 DH.RateLimter 限流组件的各种使用场景"
    });
    
    // 包含 XML 注释
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// 添加限流服务
builder.Services.AddRateLimter(options =>
{
    // 可以自定义用户身份和IP获取方式
    // options.OnUserIdentity = context => ...
    // options.OnIpAddress = context => ...
});

var app = builder.Build();

// 配置 HTTP 请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DH.RateLimter Sample API V1");
        c.RoutePrefix = string.Empty; // 设置 Swagger UI 在根路径
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
