using LMS.Data;
using LMS.Middleware;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;

DotNetEnv.Env.NoClobber().Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<VnpaySettings>(builder.Configuration.GetSection("Vnpay"));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<SubjectService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AcademicService>();
builder.Services.AddScoped<StudentAcademicService>();
builder.Services.AddScoped<VnpayPaymentService>();
builder.Services.AddScoped<GmailEmailSender>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<PasswordResetRateLimiter>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<ActionPermissionFilter>();
builder.Services.AddScoped<AppCookieAuthenticationEvents>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AppCookieAuthenticationEvents);
    });

builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options => options.Filters.AddService<ActionPermissionFilter>());

var useReactDevServer = builder.Environment.IsDevelopment() && builder.Configuration.GetValue("Frontend:UseDevServer", true);
if (useReactDevServer) builder.Services.AddHostedService<ReactDevelopmentServer>();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});
app.UseStaticFiles();
var reactRoot = Path.Combine(app.Environment.ContentRootPath, "ClientApp", "dist");
if (!Directory.Exists(reactRoot)) reactRoot = Path.Combine(app.Environment.WebRootPath, "app");
PhysicalFileProvider? reactFiles = Directory.Exists(reactRoot) ? new PhysicalFileProvider(reactRoot) : null;
if (reactFiles is not null && !useReactDevServer)
    app.UseStaticFiles(new StaticFileOptions { FileProvider = reactFiles, RequestPath = "/app" });
app.Use(async (context, next) =>
{
    if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
        context.Request.Path.Equals("/app", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/app/" + context.Request.QueryString);
        return;
    }
    await next();
});
if (useReactDevServer)
{
    app.UseWebSockets();
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/app"), frontend =>
        frontend.UseSpa(spa => spa.UseProxyToSpaDevelopmentServer($"http://127.0.0.1:{ReactDevelopmentServer.Port(app.Configuration)}")));
}
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
// Browsers authenticate with cookies: protect API writes, including the original REST endpoints.
// Non-browser clients (Postman/server integrations) retain their existing cookie workflow.
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method) &&
        (request.Headers.ContainsKey("Origin") || request.Headers.ContainsKey("Sec-Fetch-Site")))
    {
        var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        try { await antiforgery.ValidateRequestAsync(context); }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            await Results.Problem(statusCode: 400, title: "Phiên xác thực biểu mẫu không hợp lệ. Vui lòng tải lại trang.").ExecuteAsync(context);
            return;
        }
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/app/"));
if (!useReactDevServer)
{
    if (reactFiles is not null)
        app.MapFallbackToFile("/app/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = reactFiles });
    else
        app.MapGet("/app/{**path}", () => Results.Problem("React assets are missing. Run dotnet build or dotnet publish.", statusCode: 503));
}
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

await SeedData.InitializeAsync(app.Services);
await DemoDataSeeder.InitializeAsync(app.Services);
app.Run();
