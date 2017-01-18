﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            Assert.AreEqual("New York, NY", result);

        }

        [TestMethod]
        public void TestRetrieve()
        {
            // Arrange
            TranslateController controller = new TranslateController();
            try
            {
                var retrieveTranslation = controller.RetrieveTranslation("New York");
                Assert.AreEqual("New York, NY", retrieveTranslation);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
            

        }


    }
}
