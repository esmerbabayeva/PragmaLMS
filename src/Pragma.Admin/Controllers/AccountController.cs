using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Pragma.Admin.Models;
using Pragma.Admin.Services;
using Pragma.Domain.Enums;

namespace Pragma.Admin.Controllers;

public class AccountController : Controller
{
    private readonly IPragmaApiClient _apiClient;

    public AccountController(IPragmaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var authResult = await _apiClient.LoginAsync(model.Email, model.Password);
        if (authResult == null)
        {
            model.ErrorMessage = "Email və ya şifrə yanlışdır.";
            return View(model);
        }

        // Təhlükəsizlik: Admin Panelinə yalnız Admin və Müəllim daxil ola bilər
        if (!authResult.User.Roles.Contains(UserRoles.Admin) && !authResult.User.Roles.Contains(UserRoles.Instructor))
        {
            model.ErrorMessage = "Bu panelə daxil olmaq üçün Admin və ya Müəllim hüququnuz olmalıdır.";
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authResult.User.Id.ToString()),
            new(ClaimTypes.Name, authResult.User.FullName),
            new(ClaimTypes.Email, authResult.User.Email),
            new("AccessToken", authResult.AccessToken)
        };

        foreach (var role in authResult.User.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(authResult.ExpiresInMinutes)
            });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
