using System.Diagnostics;
using ECommerceCsvImport.Controllers;
using ECommerceCsvImport.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerceCsvImport.Tests
{
    public class HomeControllerTests
    {
        private static HomeController CreateController(string traceIdentifier = "trace-1")
        {
            return new HomeController(NullLogger<HomeController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier }
                }
            };
        }

        [Fact]
        public void IndexAndPrivacy_ReturnViewResult()
        {
            var controller = CreateController();

            Assert.IsType<ViewResult>(controller.Index());
            Assert.IsType<ViewResult>(controller.Privacy());
        }

        [Fact]
        public void Error_WithoutActivity_ReturnsModelWithTraceIdentifier()
        {
            var previous = Activity.Current;

            try
            {
                Activity.Current = null;

                var result = CreateController("trace-42").Error();

                var view = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<ErrorViewModel>(view.Model);
                Assert.Equal("trace-42", model.RequestId);
                Assert.True(model.ShowRequestId);
            }
            finally
            {
                Activity.Current = previous;
            }
        }
    }
}
