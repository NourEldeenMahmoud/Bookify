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

                // Check if email already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("Registration attempt with existing email: {Email}", model.Email);
                    ModelState.AddModelError("Email", "This email address is already registered. Please use a different email or try logging in.");
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
                    try
                    {
                        // Add user to Customer role (if role doesn't exist, it will be created or ignored)
                        var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
                        if (!roleResult.Succeeded)
                        {
                            _logger.LogWarning("Failed to add user {Email} to Customer role. Errors: {Errors}",
                                model.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        }
                    }
                    catch (Exception roleEx)
                    {
                        _logger.LogWarning(roleEx, "Error adding user {Email} to Customer role. Continuing with registration.", model.Email);
                    }

                    try
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User {Email} registered successfully and signed in", model.Email);
                        TempData["Success"] = "Registration successful! Welcome to Bookify.";
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception signInEx)
                    {
                        _logger.LogError(signInEx, "Error signing in user {Email} after registration", model.Email);
                        // Even if sign-in fails, redirect to login page
                        TempData["Success"] = "Registration successful! Please log in.";
                        return RedirectToAction("Login", "Account");
                    }
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

                var user = await _userManager.FindByEmailAsync(model.Email);
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

                // Handle failed login attempt - show remaining attempts
                // PasswordSignInAsync automatically increments AccessFailedCount on failure
                // So we need to get the user again to get the updated count
                if (user != null)
                {
                    // Refresh user to get updated AccessFailedCount after the failed attempt
                    user = await _userManager.FindByEmailAsync(model.Email);
                    
                    // Get lockout settings from IdentityOptions
                    var lockoutOptions = _userManager.Options.Lockout;
                    int maxFailedAttempts = lockoutOptions.MaxFailedAccessAttempts;
                    int currentFailedCount = user?.AccessFailedCount ?? 0;
                    int remainingAttempts = maxFailedAttempts - currentFailedCount;
                    
                    if (remainingAttempts > 0)
                    {
                        _logger.LogWarning("Invalid login attempt for {Email}. {RemainingAttempts} attempts remaining", 
                            model.Email, remainingAttempts);
                        ModelState.AddModelError(string.Empty, 
                            $"Invalid email or password. You have {remainingAttempts} attempt(s) remaining before your account is locked.");
                    }
                    else
                    {
                        _logger.LogWarning("Invalid login attempt for {Email}. Account will be locked on next failed attempt", model.Email);
                        ModelState.AddModelError(string.Empty, 
                            "Invalid email or password. Your account will be locked on the next failed attempt.");
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid login attempt for {Email} - user not found", model.Email);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                }
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

            var emailSent = await _emailService.SendPasswordResetEmailAsync(
                user.Email!,
                $"{user.FirstName} {user.LastName}".Trim(),
                resetUrl);

            if (emailSent)
            {
                _logger.LogInformation("Password reset email sent successfully to {Email}", model.Email);
                TempData["Success"] = "Password reset link has been sent to your email address. Please check your inbox.";
            }
            else
            {
                _logger.LogWarning("Failed to send password reset email to {Email}", model.Email);
                TempData["Error"] = "Failed to send password reset email. Please check your email configuration or try again later.";
            }

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

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        try
        {
            _logger.LogDebug("Change password page accessed");
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading change password page");
            return RedirectToAction("Index", "Profile");
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        try
        {
            if (model == null)
            {
                _logger.LogWarning("Change password attempted with null model");
                ModelState.AddModelError("", "Invalid password data.");
                return View();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Change password submitted with invalid model state");
                return View(model);
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in claims for change password");
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for change password", userId);
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index", "Profile");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Your password has been changed successfully.";
                return RedirectToAction("Index", "Profile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            _logger.LogWarning("Change password failed for user {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change password");
            TempData["Error"] = "An error occurred while changing your password. Please try again.";
            return View(model);
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

