using System.Security.Claims;
using ApexPost.Application.Abstractions;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class AccountController(
    ApexPost.Application.Abstractions.IAuthenticationService authenticationService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginInputModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var staff = await authenticationService.AuthenticateAsync(
            input.Email,
            input.Password,
            cancellationToken);

        if (staff is null)
        {
            ModelState.AddModelError(string.Empty, "The email or password is incorrect, or this account is inactive.");
            return View(input);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.StaffId.ToString()),
            new(ClaimTypes.Name, staff.FullName),
            new(ClaimTypes.Email, staff.Email),
            new(ClaimTypes.Role, staff.Role.ToString())
        };

        if (staff.BranchId is { } branchId)
        {
            claims.Add(new Claim("branch_id", branchId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(staff.BranchCode))
        {
            claims.Add(new Claim("branch_code", staff.BranchCode));
        }

        if (!string.IsNullOrWhiteSpace(staff.BranchName))
        {
            claims.Add(new Claim("branch_name", staff.BranchName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = input.RememberMe,
                AllowRefresh = true
            });

        if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
        {
            return LocalRedirect(input.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
