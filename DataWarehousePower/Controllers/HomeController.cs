using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using DataWarehousePower.Models;

namespace DataWarehousePower.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Dashboard");
        //return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    public new IActionResult Unauthorized()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult NoPermission()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string? message = null)
    {
        IExceptionHandlerPathFeature? exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        string selectedMessage = string.IsNullOrWhiteSpace(message)
            ? BuildFriendlyMessage(exceptionFeature?.Error)
            : message.Trim();

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            RequestPath = exceptionFeature?.Path,
            FriendlyMessage = selectedMessage
        });
    }

    private static string BuildFriendlyMessage(Exception? ex)
    {
        if (ex is null)
        {
            return "Something went wrong while processing your request. Please try again.";
        }

        if (ex is ArgumentException argumentException)
        {
            string message = argumentException.Message;
            string parameterMarker = " (Parameter '";
            int markerIndex = message.IndexOf(parameterMarker, StringComparison.Ordinal);
            return markerIndex > 0 ? message[..markerIndex].Trim() : message;
        }

        if (ex is InvalidOperationException)
        {
            return ex.Message;
        }

        return "An unexpected error occurred. Please try again in a moment. If the problem continues, contact support and include the request ID below.";
    }
}
