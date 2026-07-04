using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using HTX586CONTRACT.Infrastructure.Services;
using HTX586CONTRACT.Web.Components;
using HTX586CONTRACT.Web.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using HTX586CONTRACT.Web.Services;
using HTX586CONTRACT.Web.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Vps");

// Tương thích ngược với cấu hình cũ nếu server đang dùng key Vps.
// Các bản deploy IIS/VPS mới nên cấu hình ConnectionStrings:Default.
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = builder.Configuration.GetConnectionString("Vps");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Không tìm thấy connection string. Hãy cấu hình ConnectionStrings:Default bằng appsettings.Production.json, user-secrets hoặc biến môi trường ConnectionStrings__Default.");

var forwardedHeadersEnabled = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Dùng cho VPS chạy sau reverse proxy như Nginx/Cloudflare/IIS ARR.
        // Nếu cần siết bảo mật, hãy cấu hình KnownProxies/KnownNetworks cụ thể cho hạ tầng của bạn.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options =>
    {
        options.UseSqlServer(connectionString);
    });

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 6;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

var configuredCookieSecurePolicy = builder.Configuration["Authentication:CookieSecurePolicy"];
if (Enum.TryParse<CookieSecurePolicy>(configuredCookieSecurePolicy, ignoreCase: true, out var parsedCookieSecurePolicy))
{
    cookieSecurePolicy = parsedCookieSecurePolicy;
}

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "HTX586CONTRACT.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = cookieSecurePolicy;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/access-denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
});


builder.Services
    .AddAuthorizationBuilder()

    // Quyền truy cập route chỉ kiểm tra đăng nhập/phân quyền.
    // Việc bắt buộc đổi mật khẩu được khóa bằng overlay toàn cục trong MainLayout.
    .SetDefaultPolicy(
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build())

    // Page đổi mật khẩu chỉ cần đăng nhập.
    .AddPolicy(
        "PasswordChangeAllowed",
        policy =>
        {
            policy.RequireAuthenticatedUser();
        });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<
    IAuthorizationHandler,
    PasswordChangedHandler>();

builder.Services.AddScoped<
    IDriverAccountService,
    DriverAccountService>();



builder.Services.AddScoped<
    IAdminAccountService,
    AdminAccountService>();

builder.Services.AddScoped<
    ICompanyProfileService,
    CompanyProfileService>();

builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddSingleton<IUploadFileStorage, LocalUploadFileStorage>();
builder.Services.AddSingleton<PdfContractTemplateRenderer>();
builder.Services.AddScoped<PdfLayoutDesignerService>();
builder.Services.AddScoped<MasterSignatureService>();
builder.Services.AddScoped<IContractDocumentService, ContractDocumentService>();

var app = builder.Build();

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

var fileStorageOptions = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
if (fileStorageOptions.ServeUploadsAsStaticFiles)
{
    var uploadRootPath = UploadPathResolver.ResolveUploadRootPath(
        app.Environment.ContentRootPath,
        fileStorageOptions.UploadRootPath);
    Directory.CreateDirectory(uploadRootPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadRootPath),
        RequestPath = UploadPathResolver.NormalizeRequestPath(fileStorageOptions.PublicRequestPath)
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet(
    "/account/logout",
    async (
        SignInManager<ApplicationUser> signInManager,
        string? returnUrl) =>
    {
        await signInManager.SignOutAsync();

        var safeReturnUrl =
            string.IsNullOrWhiteSpace(returnUrl)
                ? "/account/login"
                : returnUrl;

        // Không cho redirect ra ngoài website.
        if (!safeReturnUrl.StartsWith('/'))
        {
            safeReturnUrl = "/account/login";
        }

        return Results.Redirect(safeReturnUrl);
    })
    .RequireAuthorization("PasswordChangeAllowed");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();
await DatabaseSeeder.SeedAsync(app.Services);
app.Run();
