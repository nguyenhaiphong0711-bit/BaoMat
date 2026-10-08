using LMS.Data;
using LMS.Middleware;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;

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

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options => options.Filters.AddService<ActionPermissionFilter>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

await SeedData.InitializeAsync(app.Services);
await DemoDataSeeder.InitializeAsync(app.Services);
app.Run();
