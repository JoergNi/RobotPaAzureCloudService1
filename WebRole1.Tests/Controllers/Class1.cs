using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebRole1.Controllers;

namespace WebRole1.Tests.Controllers
{
    [TestClass]
    public class TranslateControllerTest
    {
        [TestMethod]
        public void TestTranslation()
        {
            // Arrange
            TranslateController controller = new TranslateController();

            string result = controller.Translate("New York");
            Assert.AreEqual("NY", result);
        }

        
    }
}
