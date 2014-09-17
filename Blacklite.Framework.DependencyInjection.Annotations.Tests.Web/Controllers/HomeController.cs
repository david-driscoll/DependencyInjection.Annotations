using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;

namespace Blacklite.Framework.DependencyInjection.Annotations.Tests.Web.Controllers
{
    public class HomeController(IService1 service1) : Controller
    {
        private IService1 _service1 = service1;

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewBag.Message = "Your application description page. " + _service1.Value;

            return View();
        }

        public IActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}