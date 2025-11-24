using Bookify.Data.Models;
using Bookify.Services.Interfaces;
using Bookify.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailService _emailService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<AccountController> logger,
        IEmailService emailService)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        try
        {
            ViewData["ReturnUrl"] = returnUrl;

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
                    return RedirectToLocal(returnUrl);
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
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _logger.LogWarning("Access denied for user {UserId} - Path: {Path}", userId, HttpContext.Request.Path);
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AccessDenied page");
            return View();
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        try
        {
            _logger.LogDebug("Forgot password page accessed");
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading forgot password page");
            return RedirectToAction("Login");
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Forgot password submitted with invalid model state for {Email}", model.Email);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.LogWarning("Forgot password requested for non-existent email {Email}", model.Email);
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { email = model.Email, token }, Request.Scheme);

            if (string.IsNullOrEmpty(resetUrl))
            {
                _logger.LogError("Failed to generate password reset URL for {Email}", model.Email);
                TempData["Error"] = "Unable to generate password reset link. Please try again.";
                return View(model);
            }

            await _emailService.SendPasswordResetEmailAsync(
                user.Email!,
                $"{user.FirstName} {user.LastName}".Trim(),
                resetUrl);

            _logger.LogInformation("Password reset email sent to {Email}", model.Email);
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during forgot password for {Email}", model.Email);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot password for {Email}", model?.Email);
            TempData["Error"] = "An error occurred while processing your request. Please try again.";
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email, string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Reset password accessed with missing parameters");
                TempData["Error"] = "Invalid password reset link.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reset password page");
            TempData["Error"] = "An error occurred while loading the page.";
            return RedirectToAction(nameof(ForgotPassword));
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Reset password submitted with invalid model state for {Email}", model.Email);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.LogWarning("Reset password attempted for non-existent email {Email}", model.Email);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                _logger.LogInformation("Password reset successfully for {Email}", model.Email);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            _logger.LogWarning("Reset password failed for {Email}. Errors: {Errors}",
                model.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", model.Email);
            TempData["Error"] = "An error occurred while resetting your password. Please try again.";
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
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

