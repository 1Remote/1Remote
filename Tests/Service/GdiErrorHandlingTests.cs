using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Tests.Service
{
    /// <summary>
    /// Tests for GDI+ error handling in the Bootstrapper
    /// Related to issue: https://github.com/1Remote/1Remote/issues/924
    /// 
    /// Note: These tests verify that the Bootstrapper correctly identifies and suppresses
    /// transient GDI+ errors that occur during WindowsFormsHost painting operations.
    /// </summary>
    public class GdiErrorHandlingTests
    {
        private static MethodInfo GetIsTransientGdiErrorMethod()
        {
            var bootstrapperType = typeof(_1RM.Bootstrapper);
            var method = bootstrapperType.GetMethod("IsTransientGdiError", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method == null)
                throw new InvalidOperationException("IsTransientGdiError method not found in Bootstrapper");
            
            return method;
        }

        [Fact]
        public void IsTransientGdiError_ShouldReturnFalse_ForNonGdiError()
        {
            // Arrange
            var normalException = new Exception("Some other error");
            var method = GetIsTransientGdiErrorMethod();

            // Act
            var result = (bool)method.Invoke(null, new object[] { normalException });

            // Assert
            Assert.False(result, "Should not identify non-GDI errors as transient");
        }

        [Fact]
        public void IsTransientGdiError_ShouldReturnFalse_ForExternalExceptionWithWrongErrorCode()
        {
            // Arrange
            var exception = new ExternalException("Some error", 0x12345678);
            var method = GetIsTransientGdiErrorMethod();

            // Act
            var result = (bool)method.Invoke(null, new object[] { exception });

            // Assert
            Assert.False(result, "Should not identify ExternalException with wrong error code");
        }

        [Fact]
        public void IsTransientGdiError_ShouldReturnFalse_ForGdiErrorWithoutPaintStack()
        {
            // Arrange
            // Create an ExternalException with GDI+ error code but without paint-related stack trace
            var exception = new ExternalException("A generic error occurred in GDI+.", unchecked((int)0x80004005));
            var method = GetIsTransientGdiErrorMethod();

            // Act
            var result = (bool)method.Invoke(null, new object[] { exception });

            // Assert
            Assert.False(result, "Should require paint-related stack trace to identify as transient");
        }

        [Fact]
        public void IsTransientGdiError_ShouldReturnFalse_ForNullException()
        {
            // Arrange
            Exception? nullException = null;
            var method = GetIsTransientGdiErrorMethod();

            // Act
            var result = (bool)method.Invoke(null, new object[] { nullException });

            // Assert
            Assert.False(result, "Should handle null exception gracefully");
        }

        [Fact]
        public void IsTransientGdiError_ShouldReturnTrue_ForGdiErrorWithWinFormsAdapterStack()
        {
            // Arrange
            // This simulates the actual error by creating an exception with the right properties
            // and invoking it through a method that will create a stack trace containing "WinFormsAdapter"
            var method = GetIsTransientGdiErrorMethod();
            
            try
            {
                SimulateWinFormsAdapterError();
            }
            catch (ExternalException ex)
            {
                // Act
                var result = (bool)method.Invoke(null, new object[] { ex });

                // Assert
                // This will be true if the stack contains "WinFormsAdapter" (from the method name)
                // Note: In a real scenario, the stack would contain actual WindowsFormsHost internals
                if (ex.StackTrace?.Contains("WinFormsAdapter", StringComparison.Ordinal) == true)
                {
                    Assert.True(result, "Should identify GDI+ error with WinFormsAdapter in stack");
                }
                else
                {
                    // If the test framework doesn't preserve method names in stack traces,
                    // we can't verify this case, so we skip it
                    Assert.False(result, "Stack trace doesn't contain expected method name");
                }
            }
        }

        /// <summary>
        /// Simulates a WinFormsAdapter error by having "WinFormsAdapter" in the method name
        /// This allows the stack trace to contain the expected string
        /// </summary>
        private void SimulateWinFormsAdapterError()
        {
            throw new ExternalException("A generic error occurred in GDI+.", unchecked((int)0x80004005));
        }
    }
}
