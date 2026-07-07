using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
    public IActionResult Unauthorized()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult NoPermission()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
