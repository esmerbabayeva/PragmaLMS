using Microsoft.AspNetCore.Authentication.Cookies;
using Pragma.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. MVC və HttpContextAccessor
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// 2. Cookie Autentifikasiyası
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "Pragma.Admin.Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// 3. Typed HttpClient (Pragma.API ilə rabitə)
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5298/api/";
if (!apiBaseUrl.EndsWith("/")) apiBaseUrl += "/";

builder.Services.AddHttpClient<IPragmaApiClient, PragmaApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// 4. HTTP Sorğu Boru Xətti (Pipeline)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
