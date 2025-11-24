using Bookify.Data.Models;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bookify.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public IActionResult Register()
    {
        try
        {
            _logger.LogDebug("Register page accessed");
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading register page");
            return RedirectToAction("Error", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        try
        {
            if (model == null)
            {
                _logger.LogWarning("Register attempt with null model");
                ModelState.AddModelError("", "Invalid registration data.");
                return View();
            }

            _logger.LogInformation("Registration attempt for email: {Email}", model.Email);

            if (ModelState.IsValid)
            {
                // Additional validation
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    _logger.LogWarning("Registration attempt with empty email");
                    ModelState.AddModelError("", "Email is required.");
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    _logger.LogWarning("Registration attempt with empty password");
                    ModelState.AddModelError("", "Password is required.");
                    return View(model);
                }

                if (model.Password != model.ConfirmPassword)
                {
                    _logger.LogWarning("Password mismatch during registration for {Email}", model.Email);
                    ModelState.AddModelError("", "Passwords do not match.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    _logger.LogInformation("User {Email} registered successfully", model.Email);
                    TempData["Success"] = "Registration successful! Welcome to Bookify.";
                    return RedirectToAction("Index", "Home");
                }

                _logger.LogWarning("Registration failed for {Email}. Errors: {Errors}",
                    model.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                _logger.LogWarning("Registration attempt with invalid model state for {Email}", model.Email);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", model?.Email);
            ModelState.AddModelError("", "An error occurred during registration. Please try again.");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        try
        {
            _logger.LogDebug("Login page accessed - ReturnUrl: {ReturnUrl}", returnUrl);
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading login page");
            return RedirectToAction("Error", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        try
        {

            if (model == null)
            {
                _logger.LogWarning("Login attempt with null model");
                ModelState.AddModelError("", "Invalid login data.");
                return View();
            }

            _logger.LogInformation("Login attempt for email: {Email}", model.Email);

            if (ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    _logger.LogWarning("Login attempt with empty email");
                    ModelState.AddModelError("", "Email is required.");
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    _logger.LogWarning("Login attempt with empty password");
                    ModelState.AddModelError("", "Password is required.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} logged in successfully", model.Email);
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out", model.Email);
                    TempData["Error"] = "Your account has been locked out. Please try again later.";
                    return View("Lockout");
                }

                if (result.IsNotAllowed)
                {
                    _logger.LogWarning("User {Email} login not allowed", model.Email);
                    ModelState.AddModelError("", "Login not allowed. Please contact support.");
                    return View(model);
                }

                _logger.LogWarning("Invalid login attempt for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            else
            {
                _logger.LogWarning("Login attempt with invalid model state for {Email}", model.Email);
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", model?.Email);
            ModelState.AddModelError("", "An error occurred during login. Please try again.");
            return View(model);
        }
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User {UserId} logged out", userId);
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogWarning("Access denied for user {UserId} - Path: {Path}", userId, Request.Path);
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AccessDenied page");
            return View();
        }
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        try
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                _logger.LogDebug("Redirecting to local URL: {ReturnUrl}", returnUrl);
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redirecting to local URL: {ReturnUrl}", returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}

