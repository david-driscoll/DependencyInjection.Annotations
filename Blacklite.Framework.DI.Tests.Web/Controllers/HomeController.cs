using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;

namespace Blacklite.Framework.DI.Tests.Web.Controllers
{
    public class HomeController(IService1 service1, IOpenProviderB<string> openProviderB, IOpenProviderC<string> openProviderC) : Controller
    {
        private IService1 _service1 = service1;
        private IOpenProviderB<string> _openProviderB = openProviderB;
        private IOpenProviderC<string> _openProviderC = openProviderC;

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewBag.Message = "Your application "+ _openProviderB.ItemA + " description " + _openProviderC.ItemB + " page. " + _service1.Value;

            return View();
        }

        public IActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}