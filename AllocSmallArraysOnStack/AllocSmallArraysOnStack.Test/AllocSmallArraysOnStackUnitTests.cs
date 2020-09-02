using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using AllocSmallArraysOnStack;

namespace AllocSmallArraysOnStack.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void EmptyText_NoDiagnostics()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }
        [TestMethod]
        public void NoDiagnostic_LargeArray()
        {
            var types = new string[] { "byte", "short","int","long", "object" };
            foreach(var type in types)
            {
                var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe void Test()
            {
                var b = new " + type  + @"[1111];
            }
        }
    }";

                VerifyCSharpDiagnostic(test);
            }
            
        }

        [TestMethod]
        public void NoDiagnostic_NoConstantSize()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe void Test()
            {
                var b = new byte[new Random().Next(9,11)];
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_EscapedByMethod()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public void TestEscape(byte[] b) {}
            public unsafe void Test()
            {
                    var b = new byte[1];
                    TestEscape(b);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_EscapedByCtor()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            class TestEscape{ public TestEscape(byte[] b) {}}
            public unsafe void Test()
            {
                    var b = new byte[1];
                    new TestEscape(b);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_EscapedByAssign()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            class TestEscape{ public static byte[] b;}
            public unsafe void Test()
            {
                    var b = new byte[1];
                    TestEscape.b = b;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_ForLoop()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe void Test()
            {
                for(int i = 0; i < 100; i++)
                    var b = new byte[1];
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }
        [TestMethod]
        public void NoDiagnostic_Escapes()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe byte[] Test()
            {
                var b = new byte[1];
                return b;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_EscapesDirectly()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe byte[] Test()
            {
                return new byte[1];
            }
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_AsyncContext()
        {
                var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe async Task Test()
            {
                var b = new byte[1111];
            }
        }
    }";

                VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void NoDiagnostic_MemberArray()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            byte[] b = new byte[1111];
        }
    }";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void SingleDiagnostic_SmallByte()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe void Test()
            {
                var b = new byte[1];
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "AllocSmallArraysOnStack",
                Message = String.Format("Array '{0}' can be allocated on the stack", "new byte[1]"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void SingleDiagnostic_SmallByteAsConstMember()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            const int i = 1;
            public unsafe void Test()
            {
                var b = new byte[i];
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "AllocSmallArraysOnStack",
                Message = String.Format("Array '{0}' can be allocated on the stack", "new byte[i]"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void SingleDiagnostic_InitializerExp()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public unsafe void Test()
            {
                var b = new byte[]{1};
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "AllocSmallArraysOnStack",
                Message = String.Format("Array '{0}' can be allocated on the stack", "new byte[]{1}"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AllocSmallArraysOnStackAnalyzer();
        }
    }
}
