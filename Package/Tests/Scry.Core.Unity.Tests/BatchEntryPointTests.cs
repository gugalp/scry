using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scry.Core.Unity.Tests
{
    public class BatchEntryPointTests
    {
        [Test]
        public void GetArgValue_ReturnsValueFollowingFlag()
        {
            var args = new[] { "Unity.exe", "-batchmode", "-scryType", "My.Namespace.MyType, MyAssembly" };

            var value = BatchEntryPoint.GetArgValue(args, "-scryType");

            Assert.AreEqual("My.Namespace.MyType, MyAssembly", value);
        }

        [Test]
        public void GetArgValue_ReturnsNull_WhenFlagMissing()
        {
            var args = new[] { "Unity.exe", "-batchmode" };

            Assert.IsNull(BatchEntryPoint.GetArgValue(args, "-scryType"));
        }

        [Test]
        public void GetArgValue_ReturnsNull_WhenFlagIsLastArgument()
        {
            var args = new[] { "Unity.exe", "-scryType" };

            Assert.IsNull(BatchEntryPoint.GetArgValue(args, "-scryType"));
        }

        [Test]
        public void Run_ReturnsToolError_WhenTypeArgMissing()
        {
            var args = new[] { "Unity.exe", "-batchmode" };

            LogAssert.Expect(LogType.Error, "Scry: missing required -scryType <AssemblyQualifiedName> argument.");
            Assert.AreEqual(BatchEntryPoint.ExitCodeToolError, BatchEntryPoint.Run(args));
        }

        [Test]
        public void Run_ReturnsToolError_WhenTypeCannotBeResolved()
        {
            var args = new[] { "-scryType", "Does.Not.Exist, Nowhere" };

            LogAssert.Expect(LogType.Error, "Scry: could not resolve type 'Does.Not.Exist, Nowhere'.");
            Assert.AreEqual(BatchEntryPoint.ExitCodeToolError, BatchEntryPoint.Run(args));
        }

        [Test]
        public void Run_ReturnsSuccess_WhenTypeResolvesAndScanSucceeds()
        {
            var args = new[] { "-scryType", typeof(Fixtures.TestItemData).AssemblyQualifiedName };

            Assert.AreEqual(BatchEntryPoint.ExitCodeSuccess, BatchEntryPoint.Run(args));
        }
    }
}
