// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class CodeGenExprLambdaTests : CSharpTestBase
    {
        #region A string containing expression-tree dumping utilities
        const string ExpressionTestLibrary = @"
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;

public class TestBase
{
    protected static void DCheck<T>(Expression<T> e, string expected) { Check(e.Dump(), expected); }
    protected static void Check<T>(Expression<Func<T>> e, string expected) { Check(e.Dump(), expected); }
    protected static void Check<T1, T2>(Expression<Func<T1, T2>> e, string expected) { Check(e.Dump(), expected); }
    protected static void Check<T1, T2, T3>(Expression<Func<T1, T2, T3>> e, string expected) { Check(e.Dump(), expected); }
    protected static void Check<T1, T2, T3, T4>(Expression<Func<T1, T2, T3, T4>> e, string expected) { Check(e.Dump(), expected); }
    protected static string ToString<T>(Expression<Func<T>> e) { return e.Dump(); }
    protected static string ToString<T1, T2>(Expression<Func<T1, T2>> e) { return e.Dump(); }
    protected static string ToString<T1, T2, T3>(Expression<Func<T1, T2, T3>> e) { return e.Dump(); }
    private static void Check(string actual, string expected)
    {
        if (expected != actual)
        {
            Console.WriteLine(""FAIL"");
            Console.WriteLine(""expected: "" + expected);
            Console.WriteLine(""actual:   "" + actual);
//            throw new Exception(""expected='"" + expected + ""'; actual='"" + actual + ""'"");
        }
    }
}

public static class ExpressionExtensions
{
    public static string Dump<T>(this Expression<T> self)
    {
        return ExpressionPrinter.Print(self.Body);
    }
}

class ExpressionPrinter : System.Linq.Expressions.ExpressionVisitor
{
    private StringBuilder s = new StringBuilder();

    public static string Print(Expression e)
    {
        var p = new ExpressionPrinter();
        p.Visit(e);
        return p.s.ToString();
    }

    public override Expression Visit(Expression node)
    {
        if (node == null) { s.Append(""null""); return null; }
        s.Append(node.NodeType.ToString());
        s.Append(""("");
        base.Visit(node);
        s.Append("" Type:"" + node.Type);
        s.Append("")"");
        return null;
    }

    protected override MemberBinding VisitMemberBinding(MemberBinding node)
    {
        if (node == null) { s.Append(""null""); return null; }
        return base.VisitMemberBinding(node);
    }

    protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
    {
        s.Append(""MemberMemberBinding(Member="");
        s.Append(node.Member.ToString());
        foreach (var b in node.Bindings)
        {
            s.Append("" "");
            VisitMemberBinding(b);
        }
        s.Append("")"");
        return null;
    }

    protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
    {
        s.Append(""MemberListBinding(Member="");
        s.Append(node.Member.ToString());
        foreach (var i in node.Initializers)
        {
            s.Append("" "");
            VisitElementInit(i);
        }
        s.Append("")"");
        return null;
    }

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
    {
        s.Append(""MemberAssignment(Member="");
        s.Append(node.Member.ToString());
        s.Append("" Expression="");
        Visit(node.Expression);
        s.Append("")"");
        return null;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        s.Append(""NewExpression: "");
        Visit(node.NewExpression);
        s.Append("" Bindings:["");
        bool first = true;
        foreach (var b in node.Bindings)
        {
            if (!first) s.Append("" "");
            VisitMemberBinding(b);
            first = false;
        }
        s.Append(""]"");
        return null;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Visit(node.Left);
        s.Append("" "");
        Visit(node.Right);
        if (node.Conversion != null)
        {
            s.Append("" Conversion:"");
            Visit(node.Conversion);
        }
        if (node.IsLifted) s.Append("" Lifted"");
        if (node.IsLiftedToNull) s.Append("" LiftedToNull"");
        if (node.Method != null) s.Append("" Method:["" + node.Method + ""]"");
        return null;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        Visit(node.Test);
        s.Append("" ? "");
        Visit(node.IfTrue);
        s.Append("" : "");
        Visit(node.IfFalse);
        return null;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // s.Append(node.Value == null ? ""null"" : node.Value.ToString());
        s.Append(node.Value == null ? ""null"" : GetCultureInvariantString(node.Value));
        return null;
    }

    protected override Expression VisitDefault(DefaultExpression node)
    {
        return null;
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        Visit(node.Object);
        s.Append(""["");
        int n = node.Arguments.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("" "");
            Visit(node.Arguments[i]);
        }
        s.Append(""]"");
        if (node.Indexer != null) s.Append("" Indexer:"" + node.Indexer);
        return null;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        Visit(node.Expression);
        s.Append(""("");
        int n = node.Arguments.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("" "");
            Visit(node.Arguments[i]);
        }
        s.Append("")"");
        return null;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        s.Append(""("");
        int n = node.Parameters.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("" "");
            Visit(node.Parameters[i]);
        }
        s.Append("") => "");
        if (node.Name != null) s.Append(node.Name);
        Visit(node.Body);
        if (node.ReturnType != null) s.Append("" ReturnType:"" + node.ReturnType);
        if (node.TailCall) s.Append("" TailCall"");
        return null;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        Visit(node.NewExpression);
        s.Append(""{"");
        int n = node.Initializers.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("" "");
            Visit(node.Initializers[i]);
        }
        s.Append(""}"");
        return null;
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
        Visit(node);
        return null;
    }

    private void Visit(ElementInit node)
    {
        s.Append(""ElementInit("");
        s.Append(node.AddMethod);
        int n = node.Arguments.Count;
        for (int i = 0; i < n; i++)
        {
            s.Append("" "");
            Visit(node.Arguments[i]);
        }
        s.Append("")"");
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        Visit(node.Expression);
        s.Append(""."");
        s.Append(node.Member.Name);
        return null;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        Visit(node.Object);
        s.Append("".["" + node.Method + ""]"");
        s.Append(""("");
        int n = node.Arguments.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("", "");
            Visit(node.Arguments[i]);
        }
        s.Append("")"");
        return null;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        s.Append((node.Constructor != null) ? ""["" + node.Constructor + ""]"" : ""<.ctor>"");
        s.Append(""("");
        int n = node.Arguments.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("", "");
            Visit(node.Arguments[i]);
        }
        s.Append("")"");
        if (node.Members != null)
        {
            n = node.Members.Count;
            if (n != 0)
            {
                s.Append(""{"");
                for (int i = 0; i < n; i++)
                {
                    var info = node.Members[i];
                    if (i != 0) s.Append("" "");
                    s.Append(info);
                }
                s.Append(""}"");
            }
        }
        return null;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        s.Append(""["");
        int n = node.Expressions.Count;
        for (int i = 0; i < n; i++)
        {
            if (i != 0) s.Append("" "");
            Visit(node.Expressions[i]);
        }
        s.Append(""]"");
        return null;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        s.Append(node.Name);
        if (node.IsByRef) s.Append("" ByRef"");
        return null;
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        Visit(node.Expression);
        s.Append("" TypeOperand:"" + node.TypeOperand);
        return null;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        Visit(node.Operand);
        if (node.IsLifted) s.Append("" Lifted"");
        if (node.IsLiftedToNull) s.Append("" LiftedToNull"");
        if (node.Method != null) s.Append("" Method:["" + node.Method + ""]"");
        return null;
    }

    public static string GetCultureInvariantString(object value)
    {
        var valueType = value.GetType();
        if (valueType == typeof(string))
        {
            return value as string;
        }

        if (valueType == typeof(DateTime))
        {
            return ((DateTime)value).ToString(""M/d/yyyy h:mm:ss tt"", CultureInfo.InvariantCulture);
        }

        if (valueType == typeof(float))
        {
            return ((float)value).ToString(CultureInfo.InvariantCulture);
        }

        if (valueType == typeof(double))
        {
            return ((double)value).ToString(CultureInfo.InvariantCulture);
        }

        if (valueType == typeof(decimal))
        {
            return ((decimal)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString();
    }
}
";
        #endregion A string containing expression-tree dumping utilities

        [WorkItem(544283, "DevDiv")]
        [Fact]
        public void MissingLibrary()
        {
            string program = @"
using System;
using System.Linq.Expressions;

class Program
{
    static void Main()
    {
        Expression<Func<int>> e = () => 1;
    }
}

namespace System.Linq.Expressions
{
    class Expression<T> { }
}";
            CreateCompilationWithMscorlibAndSystemCore(program).Emit(new System.IO.MemoryStream()).Diagnostics
                .Verify(
                // (9,9): warning CS0436: The type 'System.Linq.Expressions.Expression<T>' in '' conflicts with the imported type 'System.Linq.Expressions.Expression<TDelegate>' in 'System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'. Using the type defined in ''.
                //         Expression<Func<int>> e = () => 1;
                Diagnostic(ErrorCode.WRN_SameFullNameThisAggAgg, "Expression<Func<int>>").WithArguments("", "System.Linq.Expressions.Expression<T>", "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "System.Linq.Expressions.Expression<TDelegate>"),
                // (9,35): error CS0656: Missing compiler required member 'System.Linq.Expressions.Expression.Lambda'
                //         Expression<Func<int>> e = () => 1;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "() => 1").WithArguments("System.Linq.Expressions.Expression", "Lambda")
                );
        }

        [WorkItem(543322, "DevDiv")]
        [Fact]
        public void CaptureParameterCallAddition()
        {
            string program =
@"using System;

delegate D D(int x);

class Program : TestBase
{
    public static D F(int n)
    {
        Console.WriteLine(n);
        return null;
    }
    public static void Main(string[] args)
    {
        int z = 1;
        DCheck<D>(
            x => y => F(x+y+z),
            ""Lambda((Parameter(y Type:System.Int32)) => Call(null.[D F(Int32)](Add(Add(Parameter(x Type:System.Int32) Parameter(y Type:System.Int32) Type:System.Int32) MemberAccess(Constant(Program+<>c__DisplayClass0 Type:Program+<>c__DisplayClass0).z Type:System.Int32) Type:System.Int32)) Type:D) ReturnType:D Type:D)"");
        Console.Write('k');
    }
}";
            CompileAndVerify(
                sources: new string[] { program, ExpressionTestLibrary },
                additionalRefs: new[] { SystemCoreRef },
                expectedOutput: @"k")
                .VerifyDiagnostics();
        }

        [WorkItem(543322, "DevDiv")]
        [Fact]
        public void ExpressionConversionInExpression()
        {
            string program =
@"using System;
using System.Linq.Expressions;

delegate Expression<D> D(int x);

class Program : TestBase
{
    public static Expression<D> F(int n)
    {
        Console.WriteLine(n);
        return null;
    }
    public static void Main(string[] args)
    {
        int z = 1;
        DCheck<D>(
            x => y => F(x + y + z),
            ""Quote(Lambda((Parameter(y Type:System.Int32)) => Call(null.[System.Linq.Expressions.Expression`1[D] F(Int32)](Add(Add(Parameter(x Type:System.Int32) Parameter(y Type:System.Int32) Type:System.Int32) MemberAccess(Constant(Program+<>c__DisplayClass0 Type:Program+<>c__DisplayClass0).z Type:System.Int32) Type:System.Int32)) Type:System.Linq.Expressions.Expression`1[D]) ReturnType:System.Linq.Expressions.Expression`1[D] Type:D) Type:System.Linq.Expressions.Expression`1[D])"");
        Console.Write('k');
    }
}";
            CompileAndVerify(
                sources: new string[] { program, ExpressionTestLibrary },
                additionalRefs: new[] { SystemCoreRef },
                expectedOutput: @"k")
                .VerifyDiagnostics();
        }

        [Fact]
        public void Addition()
        {
            var source =
@"using System;

class UD
{
    public static UD operator +(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator +(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l + r,
            ""Add(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l + r,
            ""Add(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Addition(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l + r,
                ""AddChecked(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l + r,
                ""AddChecked(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l + r,
                ""Add(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Addition(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l + r,
            ""Add(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l + r,
            ""Add(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_Addition(UDS, UDS)] Type:UDS)"");
        Check<decimal, decimal, decimal>(
            (x, y) => x + y,
            ""Add(Parameter(x Type:System.Decimal) Parameter(y Type:System.Decimal) Method:[System.Decimal op_Addition(System.Decimal, System.Decimal)] Type:System.Decimal)"");
        Check<string, string, string>(
            (x, y) => x + y,
            ""Add(Parameter(x Type:System.String) Parameter(y Type:System.String) Method:[System.String Concat(System.String, System.String)] Type:System.String)"");
        Check<string, int, string>(
            (x, y) => x + y,
            ""Add(Parameter(x Type:System.String) Convert(Parameter(y Type:System.Int32) Type:System.Object) Method:[System.String Concat(System.Object, System.Object)] Type:System.String)"");
        Check<int, string, string>(
            (x, y) => x + y,
            ""Add(Convert(Parameter(x Type:System.Int32) Type:System.Object) Parameter(y Type:System.String) Method:[System.String Concat(System.Object, System.Object)] Type:System.String)"");
        Check<Action, Action, Action>(
            (x, y) => x + y,
            ""Convert(Add(Parameter(x Type:System.Action) Parameter(y Type:System.Action) Method:[System.Delegate Combine(System.Delegate, System.Delegate)] Type:System.Delegate) Type:System.Action)"");
        Check<int?, int?>(
            x => x + null,
            ""Add(Parameter(x Type:System.Nullable`1[System.Int32]) Constant(null Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new string[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544027, "DevDiv")]
        [Fact]
        void AnonymousCreation()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, string, object>(
            (i, s) => new { A = i, B = s },
            ""New([Void .ctor(Int32, System.String)](Parameter(i Type:System.Int32), Parameter(s Type:System.String)){Int32 A System.String B} Type:<>f__AnonymousType0`2[System.Int32,System.String])"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544028, "DevDiv")]
        [Fact]
        void ArrayIndex()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, string>(
            i => args[i],
            ""ArrayIndex(MemberAccess(Constant(Program+<>c__DisplayClass0 Type:Program+<>c__DisplayClass0).args Type:System.String[]) Parameter(i Type:System.Int32) Type:System.String)"");
        string[,] s2 = new string[2, 2];
        Check<int, string>(
            i => s2[i,i],
            ""Call(MemberAccess(Constant(Program+<>c__DisplayClass0 Type:Program+<>c__DisplayClass0).s2 Type:System.String[,]).[System.String Get(Int32, Int32)](Parameter(i Type:System.Int32), Parameter(i Type:System.Int32)) Type:System.String)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544029, "DevDiv")]
        [Fact]
        void ArrayCreation()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int[]>(
            i => new int[i],
            ""NewArrayBounds([Parameter(i Type:System.Int32)] Type:System.Int32[])"");
        Check<int, int[,]>(
            i => new int[i,i],
            ""NewArrayBounds([Parameter(i Type:System.Int32) Parameter(i Type:System.Int32)] Type:System.Int32[,])"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544030, "DevDiv")]
        [Fact]
        void ArrayInitialization()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int[]>(
            i => new[] { i, i },
            ""NewArrayInit([Parameter(i Type:System.Int32) Parameter(i Type:System.Int32)] Type:System.Int32[])"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544112, "DevDiv")]
        [Fact]
        void CS0838ERR_ExpressionTreeContainsMultiDimensionalArrayInitializer()
        {
            var source =
@"using System;
using System.Linq.Expressions;
class Program
{
    public static void Main(string[] args)
    {
        Expression<Func<int, int[,]>> x = i => new[,] {{ i }};
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                    // (7,48): error CS0838: An expression tree may not contain a multidimensional array initializer
                    //         Expression<Func<int, int[,]>> x = i => new[,] {{ i }};
                    Diagnostic(ErrorCode.ERR_ExpressionTreeContainsMultiDimensionalArrayInitializer, "new[,] {{ i }}")
                );
        }

        [WorkItem(544031, "DevDiv")]
        [Fact]
        void ArrayLength()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int[], int>(
            s => s.Length,
            ""ArrayLength(Parameter(s Type:System.Int32[]) Type:System.Int32)"");
        Check<int[,], int>(
            a => a.Length,
            ""MemberAccess(Parameter(a Type:System.Int32[,]).Length Type:System.Int32)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"k");
        }

        [WorkItem(544032, "DevDiv")]
        [Fact]
        void AsOperator()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<object, string>(
            o => (o as string),
            ""TypeAs(Parameter(o Type:System.Object) Type:System.String)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544034, "DevDiv")]
        [Fact]
        void BaseReference()
        {
            var source =
@"using System;

class Program0 : TestBase
{
    protected virtual string M() { return ""base""; }
}
class Program : Program0
{
    protected override string M() { return ""derived""; }
    public static void Main(string[] args)
    {
        new Program().Main();
    }
    void Main()
    {
        Check<string>(
            () => base.M(), """");
        Console.Write('k');
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(new[] { Parse(source), Parse(ExpressionTestLibrary) }).VerifyDiagnostics(
                    // (265,19): error CS0831: An expression tree may not contain a base access
                    //             () => base.M(), "");
                    Diagnostic(ErrorCode.ERR_ExpressionTreeContainsBaseAccess, "base")
                );
        }

        [Fact(Skip = "BadTestCode")]
        void AsyncLambda()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<Task<int>, Task<int>>(
            async x => (await x), "");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }).VerifyDiagnostics(
                // error CS1989: Async lambda expressions cannot be converted to expression trees
                Diagnostic((ErrorCode)1989)
                );
        }

        [WorkItem(544035, "DevDiv")]
        [Fact]
        void Multiply()
        {
            var source =
@"using System;

class UD
{
    public static UD operator *(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator *(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l * r,
            ""Multiply(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l * r,
            ""Multiply(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Multiply(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l * r,
                ""MultiplyChecked(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l * r,
                ""MultiplyChecked(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l * r,
                ""Multiply(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Multiply(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l * r,
            ""Multiply(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l * r,
            ""Multiply(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_Multiply(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544036, "DevDiv")]
        [Fact]
        void Subtract()
        {
            var source =
@"using System;

class UD
{
    public static UD operator -(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator -(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l - r,
            ""Subtract(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l - r,
            ""Subtract(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Subtraction(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l - r,
                ""SubtractChecked(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l - r,
                ""SubtractChecked(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l - r,
                ""Subtract(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Subtraction(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l - r,
            ""Subtract(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l - r,
            ""Subtract(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_Subtraction(UDS, UDS)] Type:UDS)"");
        Check<Action, Action, Action>(
            (x, y) => x - y,
            ""Convert(Subtract(Parameter(x Type:System.Action) Parameter(y Type:System.Action) Method:[System.Delegate Remove(System.Delegate, System.Delegate)] Type:System.Delegate) Type:System.Action)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544037, "DevDiv")]
        [Fact]
        void Divide()
        {
            var source =
@"using System;

class UD
{
    public static UD operator /(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator /(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l / r,
            ""Divide(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l / r,
            ""Divide(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Division(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l / r,
                ""Divide(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l / r,
                ""Divide(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l / r,
                ""Divide(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Division(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l / r,
            ""Divide(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l / r,
            ""Divide(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_Division(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544038, "DevDiv")]
        [Fact]
        void Remainder()
        {
            var source =
@"using System;

class UD
{
    public static UD operator %(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator %(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l % r,
            ""Modulo(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l % r,
            ""Modulo(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Modulus(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l % r,
                ""Modulo(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l % r,
                ""Modulo(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l % r,
                ""Modulo(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_Modulus(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l % r,
            ""Modulo(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l % r,
            ""Modulo(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_Modulus(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544041, "DevDiv")]
        [Fact]
        void And()
        {
            var source =
@"using System;

class UD
{
    public static UD operator &(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator &(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l & r,
            ""And(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l & r,
            ""And(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_BitwiseAnd(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l & r,
                ""And(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l & r,
                ""And(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l & r,
                ""And(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_BitwiseAnd(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l & r,
            ""And(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l & r,
            ""And(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_BitwiseAnd(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544042, "DevDiv")]
        [Fact]
        void ExclusiveOr()
        {
            var source =
@"using System;

class UD
{
    public static UD operator ^(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator ^(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l ^ r,
            ""ExclusiveOr(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l ^ r,
            ""ExclusiveOr(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_ExclusiveOr(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l ^ r,
                ""ExclusiveOr(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l ^ r,
                ""ExclusiveOr(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l ^ r,
                ""ExclusiveOr(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_ExclusiveOr(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l ^ r,
            ""ExclusiveOr(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l ^ r,
            ""ExclusiveOr(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_ExclusiveOr(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544043, "DevDiv")]
        [Fact]
        void BitwiseOr()
        {
            var source =
@"using System;

class UD
{
    public static UD operator |(UD l, UD r) { return null; }
}
struct UDS
{
    public static UDS operator |(UDS l, UDS r) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => l | r,
            ""Or(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
        Check<UD, UD, UD>(
            (l, r) => l | r,
            ""Or(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_BitwiseOr(UD, UD)] Type:UD)"");
        checked
        {
            Check<int, int, int>(
                (l, r) => l | r,
                ""Or(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32)"");
            Check<int?, int?, int?>(
                (l, r) => l | r,
                ""Or(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
            Check<UD, UD, UD>(
                (l, r) => l | r,
                ""Or(Parameter(l Type:UD) Parameter(r Type:UD) Method:[UD op_BitwiseOr(UD, UD)] Type:UD)"");
        }
        Check<int?, int?, int?>(
            (l, r) => l | r,
            ""Or(Parameter(l Type:System.Nullable`1[System.Int32]) Parameter(r Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<UDS, UDS, UDS>(
            (l, r) => l | r,
            ""Or(Parameter(l Type:UDS) Parameter(r Type:UDS) Method:[UDS op_BitwiseOr(UDS, UDS)] Type:UDS)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544039, "DevDiv"), WorkItem(544040, "DevDiv")]
        [Fact]
        void MoreBinaryOperators()
        {
            var source =
@"using System;
struct S { }
class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int, int>(
            (l, r) => (l<<r) + (l>>r),
            ""Add(LeftShift(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32) RightShift(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Int32) Type:System.Int32)"");
        Check<int, int, bool>(
            (l, r) => (l == r),
            ""Equal(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int, int, bool>(
            (l, r) => (l != r),
            ""NotEqual(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int, int, bool>(
            (l, r) => (l < r),
            ""LessThan(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int, int, bool>(
            (l, r) => (l <= r),
            ""LessThanOrEqual(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int, int, bool>(
            (l, r) => (l > r),
            ""GreaterThan(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int, int, bool>(
            (l, r) => (l >= r),
            ""GreaterThanOrEqual(Parameter(l Type:System.Int32) Parameter(r Type:System.Int32) Type:System.Boolean)"");
        Check<int?, bool>(
            x => x == null,
            ""Equal(Parameter(x Type:System.Nullable`1[System.Int32]) Constant(null Type:System.Nullable`1[System.Int32]) Lifted Type:System.Boolean)"");
        Check<S?, bool>(
            x => x == null,
            ""Equal(Parameter(x Type:System.Nullable`1[S]) Constant(null Type:System.Nullable`1[S]) Lifted Type:System.Boolean)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544059, "DevDiv")]
        [Fact]
        void UnaryOperators()
        {
            var source =
@"using System;

class UD
{
    public static UD operator +(UD l) { return null; }
}
struct UDS
{
    public static UDS operator +(UDS l) { return default(UDS); }
}

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<int, int>(
            x => +x,
            ""Parameter(x Type:System.Int32)"");
        Check<UD, UD>(
            x => +x,
            ""UnaryPlus(Parameter(x Type:UD) Method:[UD op_UnaryPlus(UD)] Type:UD)"");
        Check<UDS, UDS>(
            x => +x,
            ""UnaryPlus(Parameter(x Type:UDS) Method:[UDS op_UnaryPlus(UDS)] Type:UDS)"");
        Check<int, int>(
            x => -x,
            ""Negate(Parameter(x Type:System.Int32) Type:System.Int32)"");
        Check<int, int>(
            x => checked (-x),
            ""NegateChecked(Parameter(x Type:System.Int32) Type:System.Int32)"");
        Check<int, int>(
            x => ~x,
            ""Not(Parameter(x Type:System.Int32) Type:System.Int32)"");
        Check<bool, bool>(
            x => !x,
            ""Not(Parameter(x Type:System.Boolean) Type:System.Boolean)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [Fact]
        void GrabBag01()
        {
            var source =
@"using System;
using System.Linq.Expressions;

struct S { }
class C { }
delegate int D(int x);
class Program : TestBase
{
    static int M(int x) { Console.Write(x); return x+1; }
    public static void Main(string[] args)
    {
        Check<bool, int, int, int>(
            (x, y, z) => x ? y : z,
            ""Conditional(Parameter(x Type:System.Boolean) ? Parameter(y Type:System.Int32) : Parameter(z Type:System.Int32) Type:System.Int32)"");
        Check<int>(
            () => default(int),
            ""Constant(0 Type:System.Int32)"");
        Main2<int>(""Constant(0 Type:System.Int32)"");
        Check<C>(
            () => default(C),
            ""Constant(null Type:C)"");
        Main2<C>(""Constant(null Type:C)"");
        Check<S>(
            () => default(S),
            ""Constant(S Type:S)"");
        Main2<S>(""Constant(S Type:S)"");
        Check<Func<int>, int>(
            x => x(),
            ""Invoke(Parameter(x Type:System.Func`1[System.Int32])() Type:System.Int32)"");
//        // The precise form of a delegate creation depends on your platform version!
//        Check<Func<int, int>>(
//            () => M,
//            ""Convert(Call(null.CreateDelegate(Constant(System.Func`2[System.Int32,System.Int32] Type:System.Type), Constant(null Type:System.Object), Constant(Int32 M(Int32) Type:MethodInfo)) Type:Delegate) Type:Func`2)"");
        Expression<Func<Func<int, int>>> f = () => M;
        f.Compile()()(1);
//        Check<Func<int, int>>(
//            () => new Func<int, int>(M),
//            ""Convert(Call(null.CreateDelegate(Constant(System.Func`2[System.Int32,System.Int32] Type:System.Type), Constant(null Type:System.Object), Constant(Int32 M(Int32) Type:MethodInfo)) Type:Delegate) Type:Func`2)"");
        f = () => new Func<int, int>(M);
        f.Compile()()(2);
//        Check<D, Func<int, int>>(
//            d => new Func<int, int>(d),
//            ""Convert(Call(null.CreateDelegate(Constant(System.Func`2[System.Int32,System.Int32] Type:System.Type), Convert(Parameter(d Type:D) Type:System.Object), Constant(Int32 Invoke(Int32) Type:MethodInfo)) Type:Delegate) Type:Func`2)"");
        D q = M;
        f = () => new Func<int, int>(q);
        f.Compile()()(3);
        Console.Write('k');
    }
    public static void Main2<T>(string expected)
    {
        Check<T>(() => default(T), expected);
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "123k");
        }

        [WorkItem(546147, "DevDiv")]
        [Fact]
        void DelegateInvoke()
        {
            var source =
@"using System;
using System.Linq.Expressions;
class P
{
    static void Main()
    {
        Func<int, int> f = c => c + 1;
        Expression<Func<int>> expr = () => f(12);
        Console.WriteLine(expr.Dump());
        Console.WriteLine(expr);
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput:
@"Invoke(MemberAccess(Constant(P+<>c__DisplayClass0 Type:P+<>c__DisplayClass0).f Type:System.Func`2[System.Int32,System.Int32])(Constant(12 Type:System.Int32)) Type:System.Int32)
() => Invoke(value(P+<>c__DisplayClass0).f, 12)");
        }

        [Fact]
        void GrabBag02()
        {
            var source =
@"using System;

class Array
{
    public int this[int x]
    {
        get
        {
            return 0;
        }
    }
}
struct S { }
class C
{
    public static C operator &(C c1, C c2) { return c1; }
    public static C operator |(C c1, C c2) { return c1; }
    public static bool operator true(C c) { return false; }
    public static bool operator false(C c) { return false; }
}
class Program : TestBase
{
    public event Action InstanceEvent;
    public static event Action StaticEvent;
    public static void Main2<T>(string expected) where T : new()
    {
        Check<T>(
            () => new T(),
            expected);
    }
    public static void Main(string[] args)
    {
        Check<Array, int, int>(
            (a, i) => a[i],
            ""Call(Parameter(a Type:Array).[Int32 get_Item(Int32)](Parameter(i Type:System.Int32)) Type:System.Int32)"");
        Check<object, bool>(
            o => o is string,
            ""TypeIs(Parameter(o Type:System.Object) TypeOperand:System.String Type:System.Boolean)"");
        Main2<int>(""New(<.ctor>() Type:System.Int32)"");
        Main2<object>(""New([Void .ctor()]() Type:System.Object)"");
        Check<string, object, object>(
            (a, b) => a ?? b,
            ""Coalesce(Parameter(a Type:System.String) Parameter(b Type:System.Object) Type:System.Object)"");
        Check<string, Exception>(
            (s) => new Exception(s),
            ""New([Void .ctor(System.String)](Parameter(s Type:System.String)) Type:System.Exception)"");
        Check<int>(
            () => new int(),
            ""Constant(0 Type:System.Int32)"");
        Check<S>(
            () => new S(),
            ""New(<.ctor>() Type:S)"");
        Check<Type>(
            () => typeof(string),
            ""Constant(System.String Type:System.Type)"");
        Check<C, C, C>(
            (l, r) => l && r,
            ""AndAlso(Parameter(l Type:C) Parameter(r Type:C) Method:[C op_BitwiseAnd(C, C)] Type:C)"");
        Check<C, C, C>(
            (l, r) => l || r,
            ""OrElse(Parameter(l Type:C) Parameter(r Type:C) Method:[C op_BitwiseOr(C, C)] Type:C)"");
        Check<int[]>(
            () => new int[] { 1, 2, 3 },
            ""NewArrayInit([Constant(1 Type:System.Int32) Constant(2 Type:System.Int32) Constant(3 Type:System.Int32)] Type:System.Int32[])"");
        Check<Program, Action>(
            p => p.InstanceEvent,
            ""MemberAccess(Parameter(p Type:Program).InstanceEvent Type:System.Action)"");
        Check<Action>(
            () => Program.StaticEvent,
            ""MemberAccess(null.StaticEvent Type:System.Action)"");
        Check<string, string, bool>(
            (x, y) => x == y,
            ""Equal(Parameter(x Type:System.String) Parameter(y Type:System.String) Method:[Boolean op_Equality(System.String, System.String)] Type:System.Boolean)"");
        DCheck<Action>(
            () => Console.WriteLine((object)null),
            ""Call(null.[Void WriteLine(System.Object)](Constant(null Type:System.Object)) Type:System.Void)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [Fact]
        void UnsafeExprTree()
        {
            var source =
@"using System;
using System.Linq.Expressions;

struct S {}
class Program
{
    public unsafe static void Main(string[] args)
    {
        int* p = null;
        Expression<Func<int>> efi = () => *p;
        Expression<Func<int>> efi2 = () => sizeof(S);
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(
                source,
                compOptions: Microsoft.CodeAnalysis.CSharp.Test.Utilities.TestOptions.UnsafeDll)
            .VerifyDiagnostics(
                // (9,43): error CS1944: An expression tree may not contain an unsafe pointer operation
                //         Expression<Func<int>> efi = () => *p;
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsPointerOp, "*p"),
                // (10,44): error CS1944: An expression tree may not contain an unsafe pointer operation
                //         Expression<Func<int>> efi2 = () => sizeof(S);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsPointerOp, "sizeof(S)")
            );
        }

        [WorkItem(544044, "DevDiv")]
        [Fact]
        void CollectionInitialization()
        {
            var source =
@"using System;

class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<System.Collections.Generic.List<int>>(
            () => new System.Collections.Generic.List<int>  { 1, 2, 3 },
            ""ListInit(New([Void .ctor()]() Type:System.Collections.Generic.List`1[System.Int32]){ElementInit(Void Add(Int32) Constant(1 Type:System.Int32)) ElementInit(Void Add(Int32) Constant(2 Type:System.Int32)) ElementInit(Void Add(Int32) Constant(3 Type:System.Int32))} Type:System.Collections.Generic.List`1[System.Int32])"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544390, "DevDiv")]
        [Fact]
        void ObjectInitialization()
        {
            var source =
@"using System;
using System.Collections.Generic;
using System.Linq.Expressions;

public class Node
{
    public Node A;
    public Node B { set; get; }
    public List<Node> C = new List<Node>();
    public List<Node> D { set; get; }
}

class Program : TestBase
{
    public static void N(Expression<Func<Node, Node>> e) { Console.WriteLine(e.Dump()); }
    public static void Main(string[] args)
    {
        N(x => new Node { A = x });
        N(x => new Node { B = x });
        N(x => new Node { A = { A = { A = x } } });
        N(x => new Node { B = { B = { B = x } } });
        N(x => new Node { B = { B = { C = { x, x } } } });
        N(x => new Node { C = { x, x } });
        N(x => new Node { D = { x, x } });
    }
}";
            var expectedOutput =
@"MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberAssignment(Member=Node A Expression=Parameter(x Type:Node))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberAssignment(Member=Node B Expression=Parameter(x Type:Node))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberMemberBinding(Member=Node A MemberMemberBinding(Member=Node A MemberAssignment(Member=Node A Expression=Parameter(x Type:Node))))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberMemberBinding(Member=Node B MemberMemberBinding(Member=Node B MemberAssignment(Member=Node B Expression=Parameter(x Type:Node))))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberMemberBinding(Member=Node B MemberMemberBinding(Member=Node B MemberListBinding(Member=System.Collections.Generic.List`1[Node] C ElementInit(Void Add(Node) Parameter(x Type:Node)) ElementInit(Void Add(Node) Parameter(x Type:Node)))))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberListBinding(Member=System.Collections.Generic.List`1[Node] C ElementInit(Void Add(Node) Parameter(x Type:Node)) ElementInit(Void Add(Node) Parameter(x Type:Node)))] Type:Node)
MemberInit(NewExpression: New([Void .ctor()]() Type:Node) Bindings:[MemberListBinding(Member=System.Collections.Generic.List`1[Node] D ElementInit(Void Add(Node) Parameter(x Type:Node)) ElementInit(Void Add(Node) Parameter(x Type:Node)))] Type:Node)";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [Fact]
        void Lambda()
        {
            var source =
@"using System;
using System.Linq.Expressions;

class L : TestBase
{
    public L Select(Expression<Func<int, int>> f) {
        Check(f, ""Parameter(y Type:System.Int32)"");
        return this;
    }
}

partial class Program
{
    public static void Main(string[] args)
    {
        L el = new L();
        var x = from y in el select y;
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [WorkItem(544218, "DevDiv")]
        [Fact]
        void Linq()
        {
            var source =
@"using System;
using System.Linq;
using System.Linq.Expressions;
 
class A
{
    static void Main()
    {
        Expression<Func<string[], object>> e = s => from x in s
                                                    from y in s
                                                    orderby x descending
                                                    select x;
        Console.WriteLine(e.ToString());
    }
}";
            var compilation = CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "s => s.SelectMany(x => s, (x, y) => new <>f__AnonymousType0`2(x = x, y = y)).OrderByDescending(<>h__TransparentIdentifier0 => <>h__TransparentIdentifier0.x).Select(<>h__TransparentIdentifier0 => <>h__TransparentIdentifier0.x)");
        }

        [Fact]
        void Enum()
        {
            var source =
@"using System;

enum Color { Red }

partial class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<Color, int, Color>(
            (x, y) => x + y,
            ""Convert(Add(Convert(Parameter(x Type:Color) Type:System.Int32) Parameter(y Type:System.Int32) Type:System.Int32) Type:Color)"");
        Check<int, Color, Color>(
            (x, y) => x + y,
            ""Convert(Add(Parameter(x Type:System.Int32) Convert(Parameter(y Type:Color) Type:System.Int32) Type:System.Int32) Type:Color)"");
        Check<Color?, int?, Color?>(
            (x, y) => x + y,
            ""Convert(Add(Convert(Parameter(x Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Parameter(y Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");
        Check<int?, Color?, Color?>(
            (x, y) => x + y,
            ""Convert(Add(Parameter(x Type:System.Nullable`1[System.Int32]) Convert(Parameter(y Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");
        Check<Color, Color, bool>(
            (x, y) => x < y,
            ""LessThan(Convert(Parameter(x Type:Color) Type:System.Int32) Convert(Parameter(y Type:Color) Type:System.Int32) Type:System.Boolean)"");
        Check<Color?, Color?, bool>(
            (x, y) => x < y,
            ""LessThan(Convert(Parameter(x Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(y Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted Type:System.Boolean)"");
        Console.Write('k');
    }
}";
            var compilation = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "k");
        }

        [Fact]
        public void CoalesceAndConversions()
        {
            var text =
@"using System;

class D
{
    public static implicit operator int(D d) { return 0; }
    public static implicit operator D(int i) { return null; }
}

public struct S
{
    public static implicit operator S(decimal d) { return new S(); }
}

partial class Program : TestBase
{
    public static void Main(string[] args)
    {
        Check<D, D, D>(
            (x, y) => x ?? y,
            ""Coalesce(Parameter(x Type:D) Parameter(y Type:D) Type:D)"");
        Check<int?, int, int>(
            (x, y) => x ?? y,
            ""Coalesce(Parameter(x Type:System.Nullable`1[System.Int32]) Parameter(y Type:System.Int32) Type:System.Int32)"");
        Check<int?, int?, int?>(
            (x, y) => x ?? y,
            ""Coalesce(Parameter(x Type:System.Nullable`1[System.Int32]) Parameter(y Type:System.Nullable`1[System.Int32]) Type:System.Nullable`1[System.Int32])"");
        Check<D, int, int>(
            (x, y) => x ?? y,
            ""Convert(Coalesce(Parameter(x Type:D) Convert(Parameter(y Type:System.Int32) Method:[D op_Implicit(Int32)] Type:D) Type:D) Method:[Int32 op_Implicit(D)] Type:System.Int32)"");
        Check<D, int?, int?>(
            (x, y) => x ?? y,
            ""Convert(Convert(Coalesce(Parameter(x Type:D) Convert(Parameter(y Type:System.Nullable`1[System.Int32]) Lifted Method:[D op_Implicit(Int32)] Type:D) Type:D) Method:[Int32 op_Implicit(D)] Type:System.Int32) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])"");
        Check<int?, D, long?>(
            (x, y) => x ?? y,
            ""Convert(Convert(Coalesce(Parameter(x Type:System.Nullable`1[System.Int32]) Convert(Parameter(y Type:D) Method:[Int32 op_Implicit(D)] Type:System.Int32) Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])"");
        Check<short?, int, long?>(
            (x, y) => x ?? y,
            ""Convert(Convert(Coalesce(Parameter(x Type:System.Nullable`1[System.Int16]) Parameter(y Type:System.Int32) Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])"");
        Check<IntPtr, DayOfWeek>( // 12549
            x => (DayOfWeek)x,
            ""Convert(Convert(Parameter(x Type:System.IntPtr) Method:[Int32 op_Explicit(IntPtr)] Type:System.Int32) Type:System.DayOfWeek)"");
        Check<int, S?>(
            x => x,
            ""Convert(Convert(Convert(Parameter(x Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal) Method:[S op_Implicit(System.Decimal)] Type:S) Lifted LiftedToNull Type:System.Nullable`1[S])"");
        // the native compiler gets the following wrong (thereby generating bad code!)  We therefore handle the expression tree differently
        Func<int?, S?> f = x => x;
        Console.WriteLine(P(f(null)));
        Console.WriteLine(P(f(1)));
    }
    static string P(S? s)
    {
        return (s == null) ? ""null"" : ""S"";
    }
}";
            var compilation = CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: @"null
S");
        }

        #region Regression Tests

        [WorkItem(544159, "DevDiv")]
        [Fact]
        public void BinaryAddOperandTypesEnumAndInt()
        {
            var text = @"
using System;
using System.Linq.Expressions;

public enum color { Red, Green, Blue };

class Test
{
    static void Main()
    {
        Expression<Func<color, int, color>> testExpr = (x, y) => x + y;
        var result = testExpr.Compile()(color.Red, 1);
        Console.WriteLine(result);
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "Green");
        }

        [WorkItem(544207, "DevDiv")]
        [Fact]
        public void BinaryAddOperandTypesStringAndString()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

class Test
{
    public static void Main()
    {
        Expression<Func<string, string, string>> testExpr = (x, y) => x + y;
        var result = testExpr.Compile()(""Hello "", ""World!"");
        Console.WriteLine(result);
    }
}
";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "Hello World!");
        }

        [WorkItem(544226, "DevDiv")]
        [Fact]
        public void BinaryAddOperandTypesDelegate()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

public delegate string Del(int i);
class Test
{
    public static void Main()
    {
        Expression<Func<Del, Del, Del>> testExpr = (x, y) => x + y;
        Console.WriteLine(testExpr);
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "(x, y) => Convert((x + y))");
        }

        [WorkItem(544187, "DevDiv")]
        [Fact]
        public void EnumLogicalOperators()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

public enum color { Red, Green, Blue };

class Test
{
    public static void Main()
    {
        Expression<Func<color, color, color>> testExpr1 = (x, y) => x & y;
        var result1 = testExpr1.Compile()(color.Red, color.Green);

        Expression<Func<color, color, color>> testExpr2 = (x, y) => x | y;
        var result2 = testExpr2.Compile()(color.Red, color.Green);

        Expression<Func<color, color, color>> testExpr3 = (x, y) => x ^ y;
        var result3 = testExpr3.Compile()(color.Red, color.Green);

        Console.WriteLine(""{0}, {1}, {2}"", result1, result2, result3);
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "Red, Green, Green");
        }

        [WorkItem(544171, "DevDiv")]
        [Fact]
        public void GenericInterfacePropertyAccess()
        {
            var text = @"
using System.Linq.Expressions;
using System;
using System.Linq;

class Test
{
    public interface ITest<T>
    {
        T Key { get; }
    }

    public static void Main()
    {
        Expression<Func<ITest<int>, int>> e = (var1) => var1.Key;
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "");
        }

        [WorkItem(544171, "DevDiv")]
        [Fact]
        public void GenericFieldAccess()
        {
            var text = @"
using System.Linq.Expressions;
using System;
using System.Linq;

class Test
{
    public class ITest<T>
    {
        public T Key;
    }

    public static void Main()
    {
        Expression<Func<ITest<int>, int>> e = (var1) => var1.Key;
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "");
        }

        [WorkItem(544185, "DevDiv")]
        [Fact]
        public void UnaryPlusOperandNullableInt()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Test
{
    public static void Main()
    {
        Expression<Func<int?, int?>> e = (x) => +x;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(e);

        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"                            
                           Lambda:
                                Type->System.Func`2[System.Nullable`1[System.Int32],System.Nullable`1[System.Int32]]
                                Parameters->
                                    Parameter:
                                        Type->System.Nullable`1[System.Int32]
                                        Name->x
                                Body->
                                    Parameter:
                                        Type->System.Nullable`1[System.Int32]
                                        Name->x

";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544213, "DevDiv")]
        [Fact]
        public void DelegateInvocation()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Test
{
    delegate int MultFunc(int a, int b);
    public static void Main()
    {
        Expression<Func<MultFunc, int>> testExpr = (mf) => mf(3, 4);
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);

        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
                            Lambda:
                                Type->System.Func`2[Test+MultFunc,System.Int32]
                                Parameters->
                                    Parameter:
                                        Type->Test+MultFunc
                                        Name->mf
                                Body->
                                    Invoke:
                                        Type->System.Int32
                                        Arguments->
                                            Constant:
                                                Type->System.Int32
                                                Value->3
                                            Constant:
                                                Type->System.Int32
                                                Value->4
                                        Lambda->
                                            Parameter:
                                                Type->Test+MultFunc
                                                Name->mf  

";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544220, "DevDiv")]
        [Fact]
        public void CoalesceWithLiftedImplicitUDC()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class SampClass1
{
    public static implicit operator SampClass1(decimal d)
    {
        return new SampClass1();
    }
}
class Test
{
    public static void Main()
    {
        Expression<Func<SampClass1>> testExpr = () => new decimal?(5) ?? new SampClass1();
    }
}";
            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: "");
        }

        [WorkItem(544222, "DevDiv")]
        [Fact]
        public void CoalesceWithImplicitUDC()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class SampClass1
{
}

public class SampClass2
{
    public static implicit operator SampClass1(SampClass2 sc1)
    {
        return new SampClass1();
    }
}

class A
{
    static void Main()
    {
        Expression<Func<SampClass1, SampClass2, SampClass1>> testExpr = (x, y) => x ?? y;
        Console.WriteLine(testExpr);
    }
}";
            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: "(x, y) => (x ?? Convert(y))");
        }

        [WorkItem(546156, "DevDiv"), WorkItem(546157, "DevDiv")]
        [Fact]
        public void CoalesceWithImplicitUDCFromNullable01()
        {
            var text =
@"using System;
using System.Linq.Expressions;
public struct CT0
{
}
public struct CT2
{
    public static implicit operator CT0(CT2? c)
    {
        throw new Exception(""this conversion is not needed during execution of this test"");
    }
}
public struct CT3
{
    public static implicit operator CT0?(CT3? c)
    {
        return null;
    }
}

public class Program
{
    static void Main()
    {
        Func<CT2?, CT0, CT0> lambda1 = (c1, c2) => c1 ?? c2;
        Expression<Func<CT2?, CT0, CT0>> e104 = (c1, c2) => c1 ?? c2;
        Console.WriteLine(e104.Dump());
        Func<CT2?, CT0, CT0> lambda2 = e104.Compile();
        Console.WriteLine(lambda1(null, new CT0()));
        Console.WriteLine(lambda2(null, new CT0()));

        Expression<Func<CT3?, CT0?, CT0?>> e105 = (c1, c2) => c1 ?? c2;
        Console.WriteLine(e105.Dump());
    }
}";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }, expectedOutput:
@"Coalesce(Parameter(c1 Type:System.Nullable`1[CT2]) Parameter(c2 Type:CT0) Conversion:Lambda((Parameter(p Type:CT2)) => Convert(Convert(Parameter(p Type:CT2) Lifted LiftedToNull Type:System.Nullable`1[CT2]) Method:[CT0 op_Implicit(System.Nullable`1[CT2])] Type:CT0) ReturnType:CT0 Type:System.Func`2[CT2,CT0]) Type:CT0)
CT0
CT0
Coalesce(Parameter(c1 Type:System.Nullable`1[CT3]) Parameter(c2 Type:System.Nullable`1[CT0]) Conversion:Lambda((Parameter(p Type:CT3)) => Convert(Convert(Parameter(p Type:CT3) Lifted LiftedToNull Type:System.Nullable`1[CT3]) Method:[System.Nullable`1[CT0] op_Implicit(System.Nullable`1[CT3])] Type:System.Nullable`1[CT0]) ReturnType:System.Nullable`1[CT0] Type:System.Func`2[CT3,System.Nullable`1[CT0]]) Type:System.Nullable`1[CT0])");
        }

        [WorkItem(544248, "DevDiv")]
        [Fact]
        public void CoalesceWithImplicitUDC2()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public struct SampStruct
{
    public static implicit operator int(SampStruct ss1)
    {
        return 1;
    }
}

public class Test
{
    static void Main()
    {
        Expression<Func<SampStruct?, decimal, decimal>> testExpr = (x, y) => x ?? y;
        Console.WriteLine(testExpr.Compile()(new SampStruct(), 5));
        Console.WriteLine(testExpr.Dump());
    }
}";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }, expectedOutput:
@"1
Coalesce(Parameter(x Type:System.Nullable`1[SampStruct]) Parameter(y Type:System.Decimal) Conversion:Lambda((Parameter(p Type:SampStruct)) => Convert(Convert(Parameter(p Type:SampStruct) Method:[Int32 op_Implicit(SampStruct)] Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal) ReturnType:System.Decimal Type:System.Func`2[SampStruct,System.Decimal]) Type:System.Decimal)"
            );
        }

        [Fact, WorkItem(544223, "DevDiv"), WorkItem(546146, "DevDiv")]
        public void CoalesceWithLiftedImplicitPDC()
        {
            var text =
@"using System;

class Test : TestBase
{
    public static void Main()
    {
        Console.WriteLine(ToString<short?, int, long?>((x, y) => x ?? y));
        Console.WriteLine(ToString<long?, int?>(x => (int?)x));
        Console.WriteLine(ToString<long, int?>(x => (int?)x));
        Console.WriteLine(ToString<long?, int>(x => (int)x));
        Console.WriteLine(ToString<int, long?>(x => x));
        checked
        {
            Console.WriteLine(ToString<short?, int, long?>((x, y) => x ?? y));
            Console.WriteLine(ToString<long?, int?>(x => (int?)x));
            Console.WriteLine(ToString<long, int?>(x => (int?)x));
            Console.WriteLine(ToString<long?, int>(x => (int)x));
            Console.WriteLine(ToString<int, long?>(x => x));
        }
    }
}";
            var expectedOutput =
@"Convert(Convert(Coalesce(Parameter(x Type:System.Nullable`1[System.Int16]) Parameter(y Type:System.Int32) Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])
Convert(Parameter(x Type:System.Nullable`1[System.Int64]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])
Convert(Parameter(x Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])
Convert(Parameter(x Type:System.Nullable`1[System.Int64]) Lifted Type:System.Int32)
Convert(Convert(Parameter(x Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])
ConvertChecked(ConvertChecked(Coalesce(Parameter(x Type:System.Nullable`1[System.Int16]) Parameter(y Type:System.Int32) Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])
ConvertChecked(Parameter(x Type:System.Nullable`1[System.Int64]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])
ConvertChecked(Parameter(x Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int32])
ConvertChecked(Parameter(x Type:System.Nullable`1[System.Int64]) Lifted Type:System.Int32)
ConvertChecked(ConvertChecked(Parameter(x Type:System.Int32) Type:System.Int64) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }, expectedOutput: expectedOutput);
        }

        [WorkItem(544228, "DevDiv")]
        [Fact]
        public void NewOfDecimal()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Test
{
    public static void Main()
    {
        Expression<Func<decimal>> testExpr = () => new decimal();
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`1[System.Decimal]
	Parameters->
	Body->
		Constant:
			Type->System.Decimal
			Value->0
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544241, "DevDiv")]
        [Fact]
        public void ArrayIndexTypeLong()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<string[], long, string>> testExpr = (str, i) => str[i];
        Console.WriteLine(testExpr);
    }
}";
            string expectedOutput = @"(str, i) => str[ConvertChecked(i)]";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544240, "DevDiv")]
        [Fact]
        public void EventAssignment()
        {
            var source =
@"using System.Linq.Expressions;

public delegate void A(D d);
public delegate void B();
public class C { public event B B1;}
public class D : C
{
    public event B B2;
    static void Main()
    {
        Expression<A> e = x => x.B2 += (B)null;
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(source)
                .VerifyDiagnostics(
                // (11,32): error CS0832: An expression tree may not contain an assignment operator
                //        Expression<A> e = x => x.B2 += (B)null;
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "x.B2 += (B)null"),
                // (5,33): warning CS0067: The event 'C.B1' is never used
                // public class C { public event B B1;}
                Diagnostic(ErrorCode.WRN_UnreferencedEvent, "B1").WithArguments("C.B1"),
                // (8,20): warning CS0067: The event 'D.B2' is never used
                //     public event B B2;
                Diagnostic(ErrorCode.WRN_UnreferencedEvent, "B2").WithArguments("D.B2")
            );
        }

        [WorkItem(544233, "DevDiv")]
        [Fact]
        public void UnsafePointerAddition()
        {
            var source =
@"using System.Linq.Expressions;

class Program
{
    unsafe delegate int* D1(int* i);
    static void Main(string[] args)
    {
        unsafe
        {
            Expression<D1> testExpr = (x) => x + 1;
        }
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(
                source,
                compOptions: Microsoft.CodeAnalysis.CSharp.Test.Utilities.TestOptions.UnsafeDll)
            .VerifyDiagnostics(
                // (10,46): error CS1944: An expression tree may not contain an unsafe pointer operation
                //             Expression<D1> testExpr = (x) => x + 1;
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsPointerOp, "x")
            );
        }


        [WorkItem(544276, "DevDiv")]
        [Fact]
        public void UnsafeParamTypeInDelegate()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

unsafe public class Test
{
    delegate int* UnsafeFunc(int* x);
    static int* G(int* x) { return x; }
    static void Main()
    {
        Expression<UnsafeFunc> testExpr = (x) => G(x);
        Console.WriteLine(testExpr);
    }
}";
            string expectedOutput = @"x => G(x)";

            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, options: Microsoft.CodeAnalysis.CSharp.Test.Utilities.TestOptions.UnsafeExe, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544246, "DevDiv")]
        [Fact]
        public void MethodCallWithParams()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    public static int ModAdd2(params int[] b) { return 0; }

    static void Main()
    {
        Expression<Func<int>> testExpr = () => ModAdd2();
        Console.WriteLine(testExpr);
    }
}";
            string expectedOutput = @"() => ModAdd2(new [] {})";

            CompileAndVerify(
                text,
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544270, "DevDiv")]
        [Fact]
        public void MethodCallWithParams2()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    public static int ModAdd2(params int[] b) { return 0; }

    static void Main()
    {
        Expression<Func<int>> testExpr = () => ModAdd2(0, 1);
        Console.WriteLine(testExpr);
    }
}";
            string expectedOutput = @"() => ModAdd2(new [] {0, 1})";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [Fact]
        public void MethodCallWithParams3()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    public static int ModAdd2(int x = 3, int y = 4, params int[] b) { return 0; }

    static void Main()
    {
        Expression<Func<int>> testExpr = () => ModAdd2();
        Console.WriteLine(testExpr);
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(text)
                .VerifyDiagnostics(
                // (10,48): error CS0854: An expression tree may not contain a call or invocation that uses optional arguments
                //         Expression<Func<int>> testExpr = () => ModAdd2();
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsOptionalArgument, "ModAdd2()")
                );
        }

        [WorkItem(544419, "DevDiv")]
        [Fact]
        public void ExplicitUDC2()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    public static explicit operator int(Test x)
    {
        return 1;
    }
    static void Main()
    {
        Expression<Func<Test, long?>> testExpr = x => (long?)x;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            var expectedOutput = @"
Lambda:
  Type->System.Func`2[Test,System.Nullable`1[System.Int64]]
  Parameters->
    Parameter:
      Type->Test
      Name->x
  Body->
    Convert:
      Type->System.Nullable`1[System.Int64]
      Method->
      IsLifted->True
      IsLiftedToNull->True
      Operand->
        Convert:
          Type->System.Int64
          Method->
          IsLifted->False
          IsLiftedToNull->False
          Operand->
            Convert:
            Type->System.Int32
            Method->Int32 op_Explicit(Test)
            IsLifted->False
            IsLiftedToNull->False
            Operand->
              Parameter:
                Type->Test
                Name->x
";
            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }


        [WorkItem(544027, "DevDiv")]
        [Fact]
        public void AnonTypes1()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<int>> testExpr = () => new { Name = ""Bill"", Salary = 6950.85m, Age = 45 }.Age;
        Console.WriteLine(testExpr.Compile()());
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "45");
        }

        [Fact]
        public void AnonTypes2()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<object>> testExpr = () => new { Name = ""Bill"", Salary = 6950.85m, Age = 45 };
        Console.WriteLine(testExpr.Dump());
    }
}";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: "New([Void .ctor(System.String, System.Decimal, Int32)](Constant(Bill Type:System.String), Constant(6950.85 Type:System.Decimal), Constant(45 Type:System.Int32)){System.String Name System.Decimal Salary Int32 Age} Type:<>f__AnonymousType0`3[System.String,System.Decimal,System.Int32])");
        }

        [WorkItem(544252, "DevDiv")]
        [Fact]
        public void EqualsWithOperandsNullableStructAndNull()
        {
            var text = @"
using System;
using System.Linq.Expressions;

public struct StructType { }

public class Test
{
    static void Main()
    {
        Expression<Func<StructType?, bool>> testExpr = (x) => x == null;
        Console.WriteLine(testExpr.Compile()(new StructType()));
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "False");
        }

        [WorkItem(544254, "DevDiv")]
        [Fact]
        public void GreaterThanUD1()
        {
            var text = @"
using System;
using System.Linq.Expressions;

struct JoinRec
{
    public static bool operator >(JoinRec a, JoinRec b)
    {
        return true;
    }

    public static bool operator <(JoinRec a, JoinRec b)
    {
        return false;
    }
}

public class Test
{
    static void Main()
    {
        Expression<Func<JoinRec, JoinRec, bool>> testExpr = (x, y) => x > y;
        Console.WriteLine(testExpr.Compile()(new JoinRec(), new JoinRec()));
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "True");
        }

        [WorkItem(544255, "DevDiv")]
        [Fact]
        public void ExpressionTreeAndOperatorOverloading()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

public class R
{
    public static int value = 0;

    static public R operator +(R r, Expression<Func<R, R>> e)
    {
        Func<R, R> fun = e.Compile();

        return r;
    }

    static public R operator +(R r1, R r2)
    {
        return r1;
    }

    public static int Test2()
    {
        R.value = 0;
        R r = new R();
        r = r + ((R c) => (c + ((R d) => (d + d))));

        return R.value;

    }
}
public class Test
{
    static void Main()
    {
        Console.WriteLine(R.Test2());
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "0");
        }

        [WorkItem(544269, "DevDiv")]
        [Fact]
        public void CheckedImplicitConversion()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Test
{
    public static void Main()
    {
        Expression<Func<int, long, long>> testExpr = (x, y) => checked(x + y);
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`3[System.Int32,System.Int64,System.Int64]
	Parameters->
		Parameter:
			Type->System.Int32
			Name->x
		Parameter:
			Type->System.Int64
			Name->y
		Body->
			AddChecked:
				Type->System.Int64
				Method->
				IsLifted->False
				IsLiftedToNull->False
				Left->
					ConvertChecked:
						Type->System.Int64
						Method->
						IsLifted->False
						IsLiftedToNull->False
						Operand->
							Parameter:
								Type->System.Int32
								Name->x
				Right->
					Parameter:
						Type->System.Int64
						Name->y
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544304, "DevDiv")]
        [Fact]
        public void CheckedEnumAddition()
        {
            var text =
@"using System;

class Test : TestBase
{
    public enum color { Red, Blue, Green };
    public static void Main()
    {
        Check<color, int, color>(
            (x, y) => checked(x + y),
            ""ConvertChecked(AddChecked(ConvertChecked(Parameter(x Type:Test+color) Type:System.Int32) Parameter(y Type:System.Int32) Type:System.Int32) Type:Test+color)"");
    }
}";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }, expectedOutput: "");
        }

        [WorkItem(544275, "DevDiv")]
        [Fact]
        public void SizeOf()
        {
            var text = @"
using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<int>> testExpr = () => sizeof(int);
        Console.WriteLine(testExpr);
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "() => 4");
        }

        [WorkItem(544285, "DevDiv")]
        [Fact]
        public void ImplicitReferenceConversion()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<string, object>> testExpr = x => x;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`2[System.String,System.Object]
	Parameters->
		Parameter:
			Type->System.String
			Name->x
	Body->
		Parameter:
			Type->System.String
			Name->x
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544287, "DevDiv")]
        [Fact]
        public void ExplicitIdentityConversion()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<int, int>> testExpr = (num1) => (int)num1;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`2[System.Int32,System.Int32]
	Parameters->
		Parameter:
			Type->System.Int32
			Name->num1
	Body->
		Convert:
			Type->System.Int32
			Method->
			IsLifted->False
			IsLiftedToNull->False
			Operand->
				Parameter:
					Type->System.Int32
					Name->num1
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544411, "DevDiv")]
        [Fact]
        public void ExplicitConvIntToNullableInt()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<int, int?>> testExpr = (num1) => (int?)num1;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`2[System.Int32,System.Nullable`1[System.Int32]]
	Parameters->
		Parameter:
			Type->System.Int32
			Name->num1
	Body->
		Convert:
			Type->System.Nullable`1[System.Int32]
			Method->
			IsLifted->True
			IsLiftedToNull->True
			Operand->
				Parameter:
					Type->System.Int32
					Name->num1
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544277, "DevDiv")]
        [Fact]
        public void ConvertExtensionMethodToDelegate()
        {
            var text =
@"using System;
using System.Linq;
using System.Linq.Expressions;
 
class A
{
    static void Main()
    {
        Expression<Func<Func<bool>>> x = () => ""ABC"".Any;
        Console.WriteLine(x.Compile()()());
    }
}";
            string expectedOutput = @"True";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: expectedOutput);
        }

        [WorkItem(544306, "DevDiv")]
        [Fact]
        public void ExplicitConversionNullToNullableType()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<short?>> testExpr = () => (short?)null;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`1[System.Nullable`1[System.Int16]]
	Parameters->
	Body->
		Convert:
			Type->System.Nullable`1[System.Int16]
			Method->
			IsLifted->True
			IsLiftedToNull->True
			Operand->
				Constant:
					Type->System.Object
					Value->
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544295, "DevDiv")]
        [Fact]
        public void LiftedEquality()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Program
{
    static void Main()
    {
        Expression<Action> e = () => Console.WriteLine(default(DateTime) == null);
        e.Compile()();
    }
}";
            string expectedOutput = @"False";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: expectedOutput);
        }

        [WorkItem(544396, "DevDiv")]
        [Fact]
        public void UserDefinedOperatorWithPointerType()
        {
            var text =
@"using System;
using System.Linq.Expressions;

unsafe class Test
{
    struct PtrRec
    {
        public static int operator +(PtrRec a, int* b)
        {
            return 10;
        }
    }


    public static void Main()
    {
        int* ptr = null;
        Expression<Func<PtrRec, int>> testExpr = (x) => x + ptr;
    }
}";

            CompileAndVerify(text,
                additionalRefs: new[] { SystemCoreRef },
                options: Microsoft.CodeAnalysis.CSharp.Test.Utilities.TestOptions.UnsafeExe,
                emitOptions: EmitOptions.RefEmitBug,
                verify: false)
            .VerifyDiagnostics();
        }

        [WorkItem(544398, "DevDiv")]
        [Fact]
        public void BitwiseComplementOnNullableShort()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<short?, int?>> testExpr = (x) => ~x;
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`2[System.Nullable`1[System.Int16],System.Nullable`1[System.Int32]]
	Parameters->
		Parameter:
			Type->System.Nullable`1[System.Int16]
			Name->x
	Body->
		Not:
			Type->System.Nullable`1[System.Int32]
			Method->
			IsLifted->True
			IsLiftedToNull->True
			Operand->
				Convert:
					Type->System.Nullable`1[System.Int32]
					Method->
					IsLifted->True
					IsLiftedToNull->True
					Operand->
						Parameter:
							Type->System.Nullable`1[System.Int16]
							Name->x
";
            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544400, "DevDiv")]
        [Fact]
        public void ExpressionTreeWithIterators()
        {
            var text =
@"using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            IEnumerable<int> enumerable = Generate<object>(5);
            IEnumerator<int> enumerator = enumerable.GetEnumerator();

            enumerator.MoveNext();
        }

        public static IEnumerable<int> Generate<T>(int count)
        {
            Expression<Func<int>> f = () => count;

            for (var i = 1; i <= count; i++)
                yield return i;
        }
    }
}";
            string expectedOutput = @"";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544401, "DevDiv")]
        [Fact]
        public void AnonMethodInsideExprTree()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public delegate void D();
public class A
{
    static void Main()
    {
        Expression<Func<D>> f = () => delegate() { };
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(text)
                .VerifyDiagnostics(
                // (9,39): error CS1945: An expression tree may not contain an anonymous method expressio
                //        Expression<Func<D>> f = () => delegate() { };
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAnonymousMethod, "delegate() { }")
                );
        }

        [WorkItem(544403, "DevDiv")]
        [Fact]
        public void ConditionalWithOperandTypesObjectArrAndStringArr()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<bool, object[]>> testExpr = (x) => x ? new object[] { ""Test"" } : new string[] { ""Test"" };
        Console.WriteLine(testExpr);
    }
}";
            string expectedOutput = @"x => IIF(x, new [] {""Test""}, Convert(new [] {""Test""}))";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544413, "DevDiv")]
        [Fact]
        public void ExplicitConversionLambdaToExprTree()
        {
            var text =
@"using System;
using System.Linq.Expressions;

public class Test
{
    static void Main()
    {
        Expression<Func<int, Expression<Func<int, int>>>> testExpr = (y => (Expression<Func<int, int>>)(x => 2 * x));
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`2[System.Int32,System.Linq.Expressions.Expression`1[System.Func`2[System.Int32,System.Int32]]]
	Parameters->
		Parameter:
			Type->System.Int32
			Name->y
	Body->
		Convert:
			Type->System.Linq.Expressions.Expression`1[System.Func`2[System.Int32,System.Int32]]
			Method->
			IsLifted->False
			IsLiftedToNull->False
			Operand->
				Quote:
					Type->System.Linq.Expressions.Expression`1[System.Func`2[System.Int32,System.Int32]]
					Method->
					IsLifted->False
					IsLiftedToNull->False
					Operand->
						Lambda:
							Type->System.Func`2[System.Int32,System.Int32]
							Parameters->
								Parameter:
									Type->System.Int32
									Name->x
							Body->
								Multiply:
									Type->System.Int32
									Method->
									IsLifted->False
									IsLiftedToNull->False
									Left->
										Constant:
											Type->System.Int32
											Value->2
									Right->
										Parameter:
											Type->System.Int32
											Name->x
";

            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544442, "DevDiv")]
        [Fact]
        public void ExprTreeFieldInitCoalesceWithNullOnLHS()
        {
            var text =
@"using System;
using System.Linq.Expressions;

class Program
{
    Expression<Func<object>> testExpr = () => null ?? new object();
}";
            CreateCompilationWithMscorlibAndSystemCore(text)
                .VerifyDiagnostics(
                // (6,47): error CS0845: An expression tree lambda may not contain a coalescing operator with a null literal left-hand side
                //     Expression<Func<object>> testExpr = () => null ?? new object();
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsBadCoalesce, "null")
                );
        }

        [WorkItem(544429, "DevDiv")]
        [Fact]
        public void ExtraConversionInDelegateCreation()
        {
            string source = @"using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;

public class TestClass1
{
    public int Func1(string a) { return 2; }
    public int Func1(int b) { return 9; }
}

public delegate int Del(string a);

class Program
{
    static void Main(string[] args)
    {
        Expression<Func<TestClass1, Del>> test2 = (tc1) => tc1.Func1;
        Console.WriteLine(test2.Dump());
    }
}";
            string expectedOutput = @"Convert(Call(null.[System.Delegate CreateDelegate(System.Type, System.Object, System.Reflection.MethodInfo)](Constant(Del Type:System.Type), Parameter(tc1 Type:TestClass1), Constant(Int32 Func1(System.String) Type:System.Reflection.MethodInfo)) Type:System.Delegate) Type:Del)";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(544430, "DevDiv")]
        [Fact]
        public void ExtraConversionInLiftedUserDefined()
        {
            string source =
@"using System;
using System.Linq.Expressions;

struct RomanNumeral
{
    static public implicit operator RomanNumeral(BinaryNumeral binary)
    {
        return new RomanNumeral();
    }
}

struct BinaryNumeral
{
}

class Program
{
    static void Main(string[] args)
    {
        Expression<Func<BinaryNumeral?, RomanNumeral?>> test4 = (expr1) => expr1;
        Console.WriteLine(test4.Dump());
    }
}";
            string expectedOutput = @"Convert(Parameter(expr1 Type:System.Nullable`1[BinaryNumeral]) Lifted LiftedToNull Method:[RomanNumeral op_Implicit(BinaryNumeral)] Type:System.Nullable`1[RomanNumeral])";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(529420, "DevDiv")]
        [Fact]
        public void HalfLiftedLeftShift()
        {
            string source =
@"using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

class Program
{
    public static void Main(string[] args)
    {
        Expression<Func<long?, short, long?>> e = (x, y) => x << y;
        Console.WriteLine(e.Dump());
    }
}";
            string expectedOutput = @"LeftShift(Parameter(x Type:System.Nullable`1[System.Int64]) Convert(Convert(Parameter(y Type:System.Int16) Type:System.Int32) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int64])";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(544451, "DevDiv")]
        [Fact]
        public void BinaryOnLiftedByteEnum()
        {
            string source =
@"using System;
using System.Linq.Expressions;

enum Color : byte { Red, Blue, Green }

class Program : TestBase
{
    public static void Main(string[] args)
    {
        // See comments in 12781 regarding these two cases
//        Check<Color?, byte, Color?>((a, b) => a + b,
//            ""Convert(Add(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Byte) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");
//        Check<Color, byte?, Color?>((a, b) => a + b,
//            ""Convert(Add(Convert(Parameter(a Type:Color) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[System.Byte]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");

        Check<Color, byte, Color>((a, b) => a + b,
            ""Convert(Add(Convert(Parameter(a Type:Color) Type:System.Int32) Convert(Parameter(b Type:System.Byte) Type:System.Int32) Type:System.Int32) Type:Color)"");
        Check<Color?, byte?, Color?>((a, b) => a + b,
            ""Convert(Add(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[System.Byte]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");

        Check<byte, Color, Color>((a, b) => a + b,
            ""Convert(Add(Convert(Parameter(a Type:System.Byte) Type:System.Int32) Convert(Parameter(b Type:Color) Type:System.Int32) Type:System.Int32) Type:Color)"");
        Check<byte?, Color?, Color?>((a, b) => a + b,
            ""Convert(Add(Convert(Parameter(a Type:System.Nullable`1[System.Byte]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");

        Check<Color, Color, byte>((a, b) => a - b,
            ""Convert(Subtract(Convert(Parameter(a Type:Color) Type:System.Int32) Convert(Parameter(b Type:Color) Type:System.Int32) Type:System.Int32) Type:System.Byte)"");
        Check<Color?, Color?, byte?>((a, b) => a - b,
            ""Convert(Subtract(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Byte])"");

        Check<Color, byte, Color>((a, b) => a - b,
            ""Convert(Subtract(Convert(Parameter(a Type:Color) Type:System.Int32) Convert(Parameter(b Type:System.Byte) Type:System.Int32) Type:System.Int32) Type:Color)"");
        Check<Color?, byte?, Color?>((a, b) => a - b,
            ""Convert(Subtract(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[System.Byte]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Color])"");

        Check<Color, Color, bool>((a, b) => a == b,
            ""Equal(Convert(Parameter(a Type:Color) Type:System.Int32) Convert(Parameter(b Type:Color) Type:System.Int32) Type:System.Boolean)"");
        Check<Color?, Color?, bool>((a, b) => a == b,
            ""Equal(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted Type:System.Boolean)"");

        Check<Color, Color, bool>((a, b) => a < b,
            ""LessThan(Convert(Parameter(a Type:Color) Type:System.Int32) Convert(Parameter(b Type:Color) Type:System.Int32) Type:System.Boolean)"");
        Check<Color?, Color?, bool>((a, b) => a < b,
            ""LessThan(Convert(Parameter(a Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(b Type:System.Nullable`1[Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted Type:System.Boolean)"");
}
}";
            string expectedOutput = "";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(544458, "DevDiv")]
        [Fact]
        public void EmptyCollectionInit()
        {
            var text =
@"using System;
using System.Collections.Generic;
using System.Linq.Expressions;

public class Parent
{
    static void Main()
    {
        Expression<Func<List<int>>> testExpr = () => new List<int> {  };
        Console.WriteLine(testExpr.Dump());
    }
}";
            string expectedOutput =
@"MemberInit(NewExpression: New([Void .ctor()]() Type:System.Collections.Generic.List`1[System.Int32]) Bindings:[] Type:System.Collections.Generic.List`1[System.Int32])";
            CompileAndVerify(
                new[] { text, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544485, "DevDiv")]
        [Fact]
        public void EmptyObjectInitForPredefinedType()
        {
            var text =
@"using System;
using System.Collections.Generic;
using System.Linq.Expressions;

public class Parent
{
    static void Main()
    {
        Expression<Func<int>> testExpr = () => new int { };
        ExpressionVisitor ev = new ExpressionVisitor();
        ev.Visit(testExpr);
        Console.Write(ev.toStr);
    }
}";
            string expectedOutput = @"
Lambda:
	Type->System.Func`1[System.Int32]
	Parameters->
	Body->
		MemberInit:
			Type->System.Int32
			NewExpression->
			New:
				Type->System.Int32
				Constructor->
				Arguments->
				Bindings->
";
            CompileAndVerify(
                new[] { text, TreeWalkerLib },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544546, "DevDiv")]
        [Fact]
        public void BadExprTreeLambdaInNSDecl()
        {
            string source = @"
namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (2,11): error CS7000: Unexpected use of an aliased name
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_UnexpectedAliasedName, "global::"),
                // (2,19): error CS1001: Identifier expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_IdentifierExpected, "("),
                // (2,70): error CS0116: A namespace does not directly contain members such as fields or methods
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, ">"),
                // (2,79): error CS0116: A namespace does not directly contain members such as fields or methods
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, "B"),
                // (2,19): error CS1514: { expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_LbraceExpected, "("),
                // (2,20): error CS1022: Type or namespace definition, or end-of-file expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_EOFExpected, "("),
                // (2,71): error CS1022: Type or namespace definition, or end-of-file expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_EOFExpected, ")"),
                // (2,81): error CS1022: Type or namespace definition, or end-of-file expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_EOFExpected, ")"),
                // (2,84): error CS1520: Method must have a return type
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "Compile"),
                // (2,93): error CS1002: ; expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "("),
                // (2,93): error CS1022: Type or namespace definition, or end-of-file expected
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_EOFExpected, "("),
                // (2,84): error CS0501: '.<invalid-global-code>.Compile()' must declare a body because it is not marked abstract, extern, or partial
                // namespace global::((System.Linq.Expressions.Expression<System.Func<B>>)(() => B )).Compile()(){}
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "Compile").WithArguments(".<invalid-global-code>.Compile()"));
        }

        [WorkItem(544548, "DevDiv")]
        [Fact]
        public void NSaliasSystemIsGlobal()
        {
            string source = @"
using System = global;

class Test
{
    static void Main()
    {
        ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine(""))).Compile()();
    }
}";
            CreateCompilationWithMscorlib(source).VerifyDiagnostics(
                // (8,58): error CS1547: Keyword 'void' cannot be used in this context
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_NoVoidHere, "void"),
                // (8,105): error CS1010: Newline in constant
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_NewlineInConst, ""),
                // (8,122): error CS1026: ) expected
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_CloseParenExpected, ""),
                // (8,122): error CS1026: ) expected
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_CloseParenExpected, ""),
                // (8,122): error CS1026: ) expected
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_CloseParenExpected, ""),
                // (8,122): error CS1002: ; expected
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_SemicolonExpected, ""),
                // (2,16): error CS0246: The type or namespace name 'global' could not be found (are you missing a using directive or an assembly reference?)
                // using System = global;
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "global").WithArguments("global"),
                // (8,11): error CS0576: Namespace '<global namespace>' contains a definition conflicting with alias 'System'
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_ConflictAliasAndMember, "System").WithArguments("System", "<global namespace>"),
                // (8,46): error CS0576: Namespace '<global namespace>' contains a definition conflicting with alias 'System'
                //         ((System.Linq.Expressions.Expression<System.Func<void>>)(() => global::System.Console.WriteLine("))).Compile()();
                Diagnostic(ErrorCode.ERR_ConflictAliasAndMember, "System").WithArguments("System", "<global namespace>"));
        }

        [WorkItem(544586, "DevDiv")]
        [Fact]
        public void ExprTreeInsideAnonymousMethod()
        {
            string source = @"
using System;
using System.Collections.Generic;

class Test
{
    delegate void D<T>(T t);

    static void M<T>(IEnumerable<T> items)
    {
        T val = default(T);
        IEnumerator<T> ie = items.GetEnumerator();
        ie.MoveNext();
        D<T> d = delegate(T tt) { val = ((System.Linq.Expressions.Expression<System.Func<T>>)(() => ie.Current)).Compile()(); Console.WriteLine(tt); };
        d(ie.Current);
    }

    static void Main()
    {
        List<int> items = new List<int>();
        items.Add(3);
        items.Add(6);
        M(items);
    }
}";
            string expectedOutput = @"3";
            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef }, expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544598, "DevDiv")]
        [Fact]
        public void ConstructorWithParamsParameter()
        {
            string source = @"
class MyClass
{
    int intTest;
    public MyClass(params int[] values)
    {
        intTest = values[0] + values[1] + values[2];
    }

    public static void Main()
    {
        MyClass mc = ((System.Linq.Expressions.Expression<System.Func<MyClass>>)(() => new MyClass(1, 2, 3))).Compile()();
        System.Console.WriteLine(mc.intTest);
    }
}";
            string expectedOutput = @"6";
            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(544599, "DevDiv")]
        [Fact]
        public void ExplicitEnumToDecimal()
        {
            string source = @"
using System;
using System.Linq.Expressions;

enum EnumType { ValOne = 1 }

public class Test
{
    public static void Main()
    {
        Expression<Func<EnumType, decimal>> e = x => (decimal)x;
        Console.WriteLine(e.Dump());
        Console.WriteLine(e.Compile()(EnumType.ValOne));
    }
}";
            string expectedOutput =
@"Convert(Convert(Parameter(x Type:EnumType) Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal)
1";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [Fact]
        public void ExplicitEnumToDecimal_Nullable1()
        {
            string source = @"
using System;
using System.Linq.Expressions;

enum E
{
    A
}

class C
{
    static void Main()
    {
        Expression<Func<E, decimal>> ed = (x) => (decimal)x;
        Expression<Func<E?, decimal>> nd = (x) => (decimal)x;
        Expression<Func<E, decimal?>> end = (x) => (decimal)x;
        Expression<Func<E?, decimal?>> nend = (x) => (decimal)x;
        Console.WriteLine(ed.Dump());
        Console.WriteLine(nd.Dump());
        Console.WriteLine(end.Dump());
        Console.WriteLine(nend.Dump());
    }
}";
            string expectedOutput = @"
Convert(Convert(Parameter(x Type:E) Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal)
Convert(Convert(Parameter(x Type:System.Nullable`1[E]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal)
Convert(Convert(Convert(Parameter(x Type:E) Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal) Lifted LiftedToNull Type:System.Nullable`1[System.Decimal])
Convert(Convert(Convert(Parameter(x Type:System.Nullable`1[E]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal) Lifted LiftedToNull Type:System.Nullable`1[System.Decimal])
".Trim();

            var verifier = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);

            verifier.VerifyIL("C.Main", @"
{
  // Code size      405 (0x195)
  .maxstack  5
  .locals init (System.Linq.Expressions.Expression<System.Func<E, decimal>> V_0, //ed
  System.Linq.Expressions.Expression<System.Func<E?, decimal>> V_1, //nd
  System.Linq.Expressions.Expression<System.Func<E, decimal?>> V_2, //end
  System.Linq.Expressions.ParameterExpression V_3)
  IL_0000:  ldtoken    ""E""
  IL_0005:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_000a:  ldstr      ""x""
  IL_000f:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_0014:  stloc.3
  IL_0015:  ldloc.3
  IL_0016:  ldtoken    ""int""
  IL_001b:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0020:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_0025:  ldtoken    ""decimal""
  IL_002a:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_002f:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_0034:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_0039:  castclass  ""System.Reflection.MethodInfo""
  IL_003e:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_0043:  ldc.i4.1
  IL_0044:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_0049:  dup
  IL_004a:  ldc.i4.0
  IL_004b:  ldloc.3
  IL_004c:  stelem.ref
  IL_004d:  call       ""System.Linq.Expressions.Expression<System.Func<E, decimal>> System.Linq.Expressions.Expression.Lambda<System.Func<E, decimal>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_0052:  stloc.0
  IL_0053:  ldtoken    ""E?""
  IL_0058:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_005d:  ldstr      ""x""
  IL_0062:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_0067:  stloc.3
  IL_0068:  ldloc.3
  IL_0069:  ldtoken    ""int?""
  IL_006e:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0073:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_0078:  ldtoken    ""decimal""
  IL_007d:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0082:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_0087:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_008c:  castclass  ""System.Reflection.MethodInfo""
  IL_0091:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_0096:  ldc.i4.1
  IL_0097:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_009c:  dup
  IL_009d:  ldc.i4.0
  IL_009e:  ldloc.3
  IL_009f:  stelem.ref
  IL_00a0:  call       ""System.Linq.Expressions.Expression<System.Func<E?, decimal>> System.Linq.Expressions.Expression.Lambda<System.Func<E?, decimal>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_00a5:  stloc.1
  IL_00a6:  ldtoken    ""E""
  IL_00ab:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_00b0:  ldstr      ""x""
  IL_00b5:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_00ba:  stloc.3
  IL_00bb:  ldloc.3
  IL_00bc:  ldtoken    ""int""
  IL_00c1:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_00c6:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_00cb:  ldtoken    ""decimal""
  IL_00d0:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_00d5:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_00da:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_00df:  castclass  ""System.Reflection.MethodInfo""
  IL_00e4:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_00e9:  ldtoken    ""decimal?""
  IL_00ee:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_00f3:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_00f8:  ldc.i4.1
  IL_00f9:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_00fe:  dup
  IL_00ff:  ldc.i4.0
  IL_0100:  ldloc.3
  IL_0101:  stelem.ref
  IL_0102:  call       ""System.Linq.Expressions.Expression<System.Func<E, decimal?>> System.Linq.Expressions.Expression.Lambda<System.Func<E, decimal?>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_0107:  stloc.2
  IL_0108:  ldtoken    ""E?""
  IL_010d:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0112:  ldstr      ""x""
  IL_0117:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_011c:  stloc.3
  IL_011d:  ldloc.3
  IL_011e:  ldtoken    ""int?""
  IL_0123:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0128:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_012d:  ldtoken    ""decimal""
  IL_0132:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0137:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_013c:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_0141:  castclass  ""System.Reflection.MethodInfo""
  IL_0146:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_014b:  ldtoken    ""decimal?""
  IL_0150:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0155:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_015a:  ldc.i4.1
  IL_015b:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_0160:  dup
  IL_0161:  ldc.i4.0
  IL_0162:  ldloc.3
  IL_0163:  stelem.ref
  IL_0164:  call       ""System.Linq.Expressions.Expression<System.Func<E?, decimal?>> System.Linq.Expressions.Expression.Lambda<System.Func<E?, decimal?>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_0169:  ldloc.0
  IL_016a:  call       ""string ExpressionExtensions.Dump<System.Func<E, decimal>>(System.Linq.Expressions.Expression<System.Func<E, decimal>>)""
  IL_016f:  call       ""void System.Console.WriteLine(string)""
  IL_0174:  ldloc.1
  IL_0175:  call       ""string ExpressionExtensions.Dump<System.Func<E?, decimal>>(System.Linq.Expressions.Expression<System.Func<E?, decimal>>)""
  IL_017a:  call       ""void System.Console.WriteLine(string)""
  IL_017f:  ldloc.2
  IL_0180:  call       ""string ExpressionExtensions.Dump<System.Func<E, decimal?>>(System.Linq.Expressions.Expression<System.Func<E, decimal?>>)""
  IL_0185:  call       ""void System.Console.WriteLine(string)""
  IL_018a:  call       ""string ExpressionExtensions.Dump<System.Func<E?, decimal?>>(System.Linq.Expressions.Expression<System.Func<E?, decimal?>>)""
  IL_018f:  call       ""void System.Console.WriteLine(string)""
  IL_0194:  ret
}");
        }

        [Fact]
        public void ExplicitEnumToDecimal_Nullable2()
        {
            string source = @"
using System;
using System.Linq.Expressions;

enum E
{
    A
}

class C
{
    static void Main()
    {
        Expression<Func<E, decimal?>> end = (x) => (decimal?)x;
        Expression<Func<E?, decimal?>> nend = (x) => (decimal?)x;
        Console.WriteLine(end.Dump());
        Console.WriteLine(nend.Dump());
    }
}";
            string expectedOutput = @"
Convert(Convert(Convert(Parameter(x Type:E) Type:System.Int32) Method:[System.Decimal op_Implicit(Int32)] Type:System.Decimal) Lifted LiftedToNull Type:System.Nullable`1[System.Decimal])
Convert(Convert(Parameter(x Type:System.Nullable`1[E]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Method:[System.Decimal op_Implicit(Int32)] Type:System.Nullable`1[System.Decimal])
".Trim();

            var verifier = CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);

            verifier.VerifyIL("C.Main", @"
{
  // Code size      202 (0xca)
  .maxstack  5
  .locals init (System.Linq.Expressions.Expression<System.Func<E, decimal?>> V_0, //end
  System.Linq.Expressions.ParameterExpression V_1)
  IL_0000:  ldtoken    ""E""
  IL_0005:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_000a:  ldstr      ""x""
  IL_000f:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_0014:  stloc.1
  IL_0015:  ldloc.1
  IL_0016:  ldtoken    ""int""
  IL_001b:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0020:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_0025:  ldtoken    ""decimal""
  IL_002a:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_002f:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_0034:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_0039:  castclass  ""System.Reflection.MethodInfo""
  IL_003e:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_0043:  ldtoken    ""decimal?""
  IL_0048:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_004d:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_0052:  ldc.i4.1
  IL_0053:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_0058:  dup
  IL_0059:  ldc.i4.0
  IL_005a:  ldloc.1
  IL_005b:  stelem.ref
  IL_005c:  call       ""System.Linq.Expressions.Expression<System.Func<E, decimal?>> System.Linq.Expressions.Expression.Lambda<System.Func<E, decimal?>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_0061:  stloc.0
  IL_0062:  ldtoken    ""E?""
  IL_0067:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_006c:  ldstr      ""x""
  IL_0071:  call       ""System.Linq.Expressions.ParameterExpression System.Linq.Expressions.Expression.Parameter(System.Type, string)""
  IL_0076:  stloc.1
  IL_0077:  ldloc.1
  IL_0078:  ldtoken    ""int?""
  IL_007d:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0082:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type)""
  IL_0087:  ldtoken    ""decimal?""
  IL_008c:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_0091:  ldtoken    ""decimal decimal.op_Implicit(int)""
  IL_0096:  call       ""System.Reflection.MethodBase System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)""
  IL_009b:  castclass  ""System.Reflection.MethodInfo""
  IL_00a0:  call       ""System.Linq.Expressions.UnaryExpression System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression, System.Type, System.Reflection.MethodInfo)""
  IL_00a5:  ldc.i4.1
  IL_00a6:  newarr     ""System.Linq.Expressions.ParameterExpression""
  IL_00ab:  dup
  IL_00ac:  ldc.i4.0
  IL_00ad:  ldloc.1
  IL_00ae:  stelem.ref
  IL_00af:  call       ""System.Linq.Expressions.Expression<System.Func<E?, decimal?>> System.Linq.Expressions.Expression.Lambda<System.Func<E?, decimal?>>(System.Linq.Expressions.Expression, params System.Linq.Expressions.ParameterExpression[])""
  IL_00b4:  ldloc.0
  IL_00b5:  call       ""string ExpressionExtensions.Dump<System.Func<E, decimal?>>(System.Linq.Expressions.Expression<System.Func<E, decimal?>>)""
  IL_00ba:  call       ""void System.Console.WriteLine(string)""
  IL_00bf:  call       ""string ExpressionExtensions.Dump<System.Func<E?, decimal?>>(System.Linq.Expressions.Expression<System.Func<E?, decimal?>>)""
  IL_00c4:  call       ""void System.Console.WriteLine(string)""
  IL_00c9:  ret
}");
        }

        [WorkItem(544955, "DevDiv")]
        [Fact]
        public void FirstOperandOfConditionalOperatorImplementsOperatorTrue()
        {
            string source = @"using System;
using System.Linq.Expressions;

class MyTest
{
    public static bool operator true(MyTest t)
    {
        return true;
    }
    public static bool operator false(MyTest t)
    {
        return false;
    }
}

class MyClass
{

    public static void Main()
    {
        Expression<Func<MyTest, int>> e = t => t ? 2 : 3;
        Console.WriteLine(e.Dump());
        int intI = e.Compile()(new MyTest());
        Console.WriteLine(intI);
    }
}";
            string expectedOutput = @"Conditional(Call(null.[Boolean op_True(MyTest)](Parameter(t Type:MyTest)) Type:System.Boolean) ? Constant(2 Type:System.Int32) : Constant(3 Type:System.Int32) Type:System.Int32)
2";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(545042, "DevDiv")]
        [Fact]
        public void AnonMethodInExprTree()
        {
            var source =
@"using System;
using System.Linq.Expressions;

public class Program
{
    static void  Main()
    {
        EventHandler eventHandler = delegate { };
        Expression<Func<EventHandler>> testExpr = () => new EventHandler(delegate { });
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(source)
            .VerifyDiagnostics(
                // (9,74): error CS1945: An expression tree may not contain an anonymous method expression
                //        Expression<Func<EventHandler>> testExpr = () => new EventHandler(delegate { });
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAnonymousMethod, "delegate { }")
            );
        }

        [WorkItem(545122, "DevDiv")]
        [Fact]
        public void CollInitAddMethodWithParams()
        {
            string source = @"
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq.Expressions;

class C
{
    public static void Main()
    {
        Expression<Func<B>> e1 = () => new B { { 5, 8, 10, 15L } };
        Console.WriteLine(e1);
    }
}

public class B : IEnumerable
{
    List<object> list = new List<object>();

    public int Add(params long[] l1)
    {
        for (int i = 0; i < l1.Length; i++)
        {
            list.Add(l1[i]);
        }
        return 10;
    }
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < list.Count; i++)
            yield return list[i];
    }
}";
            string expectedOutput = @"() => new B() {Int32 Add(Int64[])(new [] {5, 8, 10, 15})}";
            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: TrimExpectedOutput(expectedOutput));
        }

        [WorkItem(545189, "DevDiv")]
        [Fact]
        public void ExprTreeInTypeArgument()
        {
            string source = @"
public class MemberInitializerTest
{
    delegate void D<T>();
    public static void GenericMethod<T>() { }
    public static void Run()
    {
        Foo f = new Foo {
            genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
        };
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.Experimental)).VerifyDiagnostics(
                // (9,108): error CS1001: Identifier expected
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_IdentifierExpected, ")"),
                // (9,123): error CS1525: Invalid expression term '}'
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "").WithArguments("}"),
                // (8,9): error CS0246: The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)
                //         Foo f = new Foo {
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Foo").WithArguments("Foo"),
                // (8,21): error CS0246: The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)
                //         Foo f = new Foo {
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Foo").WithArguments("Foo"),
                // (9,20): error CS0030: Cannot convert type 'method' to 'MemberInitializerTest.D<int>'
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(D<int>) GenericMethod").WithArguments("method", "MemberInitializerTest.D<int>"),
                // (9,105): error CS0165: Use of unassigned local variable ''
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_UseDefViolation, "int").WithArguments("").WithLocation(9, 105));
        }

        [WorkItem(545189, "DevDiv")]
        [Fact]
        public void ExprTreeInTypeArgument_NoDeclExpr()
        {
            string source = @"
public class MemberInitializerTest
{
    delegate void D<T>();
    public static void GenericMethod<T>() { }
    public static void Run()
    {
        Foo f = new Foo {
            genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
        };
    }
}";
            CreateCompilationWithMscorlibAndSystemCore(source).VerifyDiagnostics(
                // (9,105): error CS1525: Invalid expression term 'int'
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "int").WithArguments("int"),
                // (9,123): error CS1525: Invalid expression term '}'
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "").WithArguments("}"),
                // (8,9): error CS0246: The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)
                //         Foo f = new Foo {
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Foo").WithArguments("Foo"),
                // (8,21): error CS0246: The type or namespace name 'Foo' could not be found (are you missing a using directive or an assembly reference?)
                //         Foo f = new Foo {
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Foo").WithArguments("Foo"),
                // (9,20): error CS0030: Cannot convert type 'method' to 'MemberInitializerTest.D<int>'
                //             genD = (D<int>) GenericMethod<((System.Linq.Expressions.Expression<System.Func<int>>)(() => int)).Compile()()> 
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(D<int>) GenericMethod").WithArguments("method", "MemberInitializerTest.D<int>"));
        }

        [WorkItem(545191, "DevDiv")]
        [Fact]
        public void ObjectInitializersValueType()
        {
            string source = @"
using System;
using System.Linq;
using System.Linq.Expressions;

interface I
{
    int X { get; set; }
}

struct S : I
{
    public int X { get; set; }
}

class Program
{
    static void Main()
    {
        int result = Foo<S>();
        Console.WriteLine(result);
    }

    static int Foo<T>() where T : I, new()
    {
        Expression<Func<T>> f1 = () => new T { X = 1 };
        var b = f1.Compile()();
        return b.X;
    }
}";
            string expectedOutput = @"1";

            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(545396, "DevDiv")]
        [Fact]
        public void LongLengthArrayProperty()
        {
            string source = @"
using System;
using System.Linq.Expressions;

public class Test
{
    public static void Main()
    {        
        Expression<Func<long>> e1 = () => new int[100].LongLength;
        Func<long> f1 = e1.Compile();

        Console.WriteLine(f1());
    }
}";
            string expectedOutput = @"100";

            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(545457, "DevDiv")]
        [Fact]
        public void NullableDecimalToNullableEnumExplicitConv()
        {
            string source = @"
using System;
using System.Linq.Expressions;

public class Derived
{
    public enum Enum1 { zero, one, two, three }

    public static void Main()
    {
        Expression<Func<decimal?, Enum1?>> f1e = decimalq => (Enum1?)decimalq;
        Func<decimal?, Enum1?> f1 = f1e.Compile();
        Console.WriteLine(f1(1));

        Expression<Func<decimal, Enum1?>> f2e = decimalq => (Enum1?)decimalq;
        Func<decimal, Enum1?> f2 = f2e.Compile();
        Console.WriteLine(f2(2));

        Expression<Func<decimal?, Enum1>> f3e = decimalq => (Enum1)decimalq;
        Func<decimal?, Enum1> f3 = f3e.Compile();
        Console.WriteLine(f3(3));
    }
}
";
            string expectedOutput =
@"one
two
three";

            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(545461, "DevDiv")]
        [Fact]
        public void LiftedUserDefinedConversionWithNullArg()
        {
            string source = @"
using System;
using System.Linq.Expressions;

public struct C
{
    public static implicit operator C(int num)
    {
        return new C();
    }

    public static void Main()
    {
        Expression<Func<int?, C?>> e1 = (x) => (C?)x;
        Console.WriteLine(e1.Dump());
        e1.Compile()(null);
    }
}
";
            string expectedOutput = @"Convert(Parameter(x Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Method:[C op_Implicit(Int32)] Type:System.Nullable`1[C])";

            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(546731, "DevDiv")]
        [Fact]
        public void CallLeastDerivedOverride()
        {
            string source = @"
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
public class TestClass1
{
    public virtual int VirtMeth1() { return 5; }
}
public class TestClass2 : TestClass1
{
    public override int VirtMeth1() { return 10; }
}
class Test
{
    // Invoking a instance virtual method, ""override""
    public static void Main()
    {
        Expression<Func<TestClass2, int>> testExpr = (tc2) => tc2.VirtMeth1();
        Console.WriteLine(testExpr.Dump());
        Console.WriteLine(((MethodCallExpression)testExpr.Body).Method.DeclaringType);
        Console.WriteLine(testExpr.Compile()(new TestClass2()));
    }
}
";

            string expectedOutput = @"Call(Parameter(tc2 Type:TestClass2).[Int32 VirtMeth1()]() Type:System.Int32)
TestClass1
10";

            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(530529, "DevDiv")]
        [Fact]
        public void BoxTypeParameter()
        {
            string source =
@"using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
interface I
{
    int P { get; set; }
    int M();
}
struct S : I
{
    int _p;
    public int P { get { return _p++; } set { _p = value; } }
    public int M() { P = 7; return 1; }
}
class Test
{
    public static void Test1<T>() where T : I
    {
        Func<T, int> f = x => x.M() + x.P + x.P;
        var r1 = f(default(T));
        Expression<Func<T, int>> e = x => x.M() + x.P + x.P;
        var r2 = e.Compile()(default(T));
        Console.WriteLine(r1==r2 ? ""pass"" : ""fail"");
    }
    static void Main()
    {
        Test1<S>();
    }
}";

            string expectedOutput = @"pass";

            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(546601, "DevDiv")]
        [Fact]
        public void NewArrayInitInAsAndIs()
        {
            string source =
@"using System;
using System.Linq.Expressions;
class Test
{
    public static void Main()
    {
        // Func<bool> func1 = () => new[] { DayOfWeek.Friday } is int[];
        Expression<Func<bool>> expr1 = () => new[] { DayOfWeek.Friday } is int[];
        Console.WriteLine(expr1.Dump());

        Expression<Func<Test>> expr2 = () => (object)null as Test;
        Console.WriteLine(expr2.Dump());

        Expression<Func<Test, object>> e = t => t as object;
        Console.WriteLine(e.Dump());
    }
}";

            string expectedOutput =
@"TypeIs(NewArrayInit([Constant(Friday Type:System.DayOfWeek)] Type:System.DayOfWeek[]) TypeOperand:System.Int32[] Type:System.Boolean)
TypeAs(Constant(null Type:System.Object) Type:Test)
TypeAs(Parameter(t Type:Test) Type:System.Object)";

            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(531047, "DevDiv")]
        [Fact, WorkItem(531047, "DevDiv")]
        public void NullIsRegression()
        {
            string source =
@"using System;
using System.Linq.Expressions;
class Test
{
    public static void Main()
    {
        Expression<Func<bool>> expr = () => null is Test;
        Console.WriteLine(expr.Dump());
    }
}";
            string expectedOutput = "TypeIs(Constant(null Type:System.Object) TypeOperand:Test Type:System.Boolean)";
            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(546618, "DevDiv")]
        [Fact]
        public void TildeNullableEnum()
        {
            string source =
@"using System;
using System.Linq;
using System.Linq.Expressions;
class Test
{
    public enum Color { Red, Green, Blue };
    static void Main()
    {
        Expression<Func<Color?, Color?>> e1 = x => x ^ x;
        Console.WriteLine(e1.Dump());
        Expression<Func<Color?, Color?>> e2 = x => ~x;
        Console.WriteLine(e2.Dump());
        Expression<Func<Color, Color>> e3 = x => ~x;
        Console.WriteLine(e3.Dump());
    }
}";

            string expectedOutput =
@"Convert(ExclusiveOr(Convert(Parameter(x Type:System.Nullable`1[Test+Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Convert(Parameter(x Type:System.Nullable`1[Test+Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Test+Color])
Convert(Not(Convert(Parameter(x Type:System.Nullable`1[Test+Color]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[System.Int32]) Lifted LiftedToNull Type:System.Nullable`1[Test+Color])
Convert(Not(Convert(Parameter(x Type:Test+Color) Type:System.Int32) Type:System.Int32) Type:Test+Color)";

            CompileAndVerify(
                new[] { source, ExpressionTestLibrary },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        [WorkItem(531382, "DevDiv")]
        [Fact]
        public void IndexerIsIndexedPropoperty()
        {
            var source1 =
@"<System.Runtime.InteropServices.ComImport>
Public Class Cells
    Default Public ReadOnly Property Cell(index As Integer) As Integer
        Get
            Return 0
        End Get
    End Property
End Class";
            var reference1 = BasicCompilationUtils.CompileToMetadata(source1, verify: false);

            var source2 =
@"class A
{
    public Cells Cells
    {
        get { return null; }
    }
}

class Program
{
    static void Main(string[] args)
    {
        System.Linq.Expressions.Expression<System.Func<A, int>> z2 = a => a.Cells[2];
        System.Console.WriteLine(z2.ToString());
    }
}";
            var expectedOutput = @"a => a.Cells.get_Cell(2)";
            CompileAndVerify(
                new[] { source2 },
                new[] { ExpressionAssemblyRef, reference1 },
                expectedOutput: expectedOutput);
        }

        [WorkItem(579711, "DevDiv")]
        [Fact]
        public void CheckedEnumConversion()
        {
            var text =
@"using System;
using System.Linq;
using System.Linq.Expressions;

public enum color { Red, Blue, Green };
public enum cars { Toyota, Honda, Scion, Ford };
class C
{
    static void Main()
    {
        Expression<Func<color, int>> expr1 = (x) => checked((int)x);
        Console.WriteLine(expr1);
        Expression<Func<int, color>> expr2 = (x) => checked((color)x);
        Console.WriteLine(expr2);
        Expression<Func<cars, color>> expr3 = (x) => checked((color)x);
        Console.WriteLine(expr3);
    }
}";
            var expected =
@"x => ConvertChecked(x)
x => ConvertChecked(x)
x => ConvertChecked(x)";
            CompileAndVerify(
                new[] { text },
                new[] { ExpressionAssemblyRef }, expectedOutput: expected);
        }

        [WorkItem(717364, "DevDiv")]
        [Fact]
        public void NullAs()
        {
            var text = @"
using System;
using System.Linq;
using System.Linq.Expressions;

public delegate object Del();
class Test
{
    public static void Main()
    {
        Expression<Del> testExpr = () => null as string;
        Console.WriteLine(testExpr);
    }
}";
            CompileAndVerify(text, new[] { ExpressionAssemblyRef }, expectedOutput: "() => (null As String)");
        }

        [WorkItem(797996, "DevDiv")]
        [Fact]
        public void MissingMember_System_Type__GetTypeFromHandle()
        {
            var text =
@"using System.Linq.Expressions;
namespace System
{
    public class Object { }
    public struct Void { }
    public class ValueType { }
    public class MulticastDelegate { }
    public struct IntPtr { }
    public struct Int32 { }
    public struct Nullable<T> { }
    public class Type { }
}
namespace System.Collections.Generic
{
    public interface IEnumerable<T> { }
}
namespace System.Linq.Expressions
{
    public class Expression
    {
        public static Expression New(Type t) { return null; }
        public static Expression<T> Lambda<T>(Expression e, Expression[] args) { return null; }
    }
    public class Expression<T> { }
    public class ParameterExpression : Expression { }
}
delegate C D();
class C
{
    static Expression<D> E = () => new C();
}";
            var compilation = CreateCompilation(text);
            compilation.VerifyDiagnostics();
            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                result.Diagnostics.Verify(
                    // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                    Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion),
                    // (30,36): error CS0656: Missing compiler required member 'System.Type.GetTypeFromHandle'
                    //     static Expression<D> E = () => new C();
                    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "new C()").WithArguments("System.Type", "GetTypeFromHandle").WithLocation(30, 36));
            }
        }

        [WorkItem(797996, "DevDiv")]
        [Fact]
        public void MissingMember_System_Reflection_FieldInfo__GetFieldFromHandle()
        {
            var text =
@"using System.Linq.Expressions;
using System.Reflection;
namespace System
{
    public class Object { }
    public struct Void { }
    public class ValueType { }
    public class MulticastDelegate { }
    public struct IntPtr { }
    public struct Int32 { }
    public struct Nullable<T> { }
    public class Type { }
    public class Array { }
}
namespace System.Collections.Generic
{
    public interface IEnumerable<T> { }
}
namespace System.Linq.Expressions
{
    public class Expression
    {
        public static Expression Field(Expression e, FieldInfo f) { return null; }
        public static Expression<T> Lambda<T>(Expression e, ParameterExpression[] args) { return null; }
    }
    public class Expression<T> { }
    public class ParameterExpression : Expression { }
}
namespace System.Reflection
{
    public class FieldInfo { }
}
delegate object D();
class A
{
    static object F = null;
    static Expression<D> G = () => F;
}
class B<T>
{
    static object F = null;
    static Expression<D> G = () => F;
}";
            var compilation = CreateCompilation(text);
            compilation.VerifyDiagnostics();
            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                result.Diagnostics.Verify(
    // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
    Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
    // (37,36): error CS0656: Missing compiler required member 'System.Reflection.FieldInfo.GetFieldFromHandle'
    //     static Expression<D> G = () => F;
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "F").WithArguments("System.Reflection.FieldInfo", "GetFieldFromHandle").WithLocation(37, 36),
    // (42,36): error CS0656: Missing compiler required member 'System.Reflection.FieldInfo.GetFieldFromHandle'
    //     static Expression<D> G = () => F;
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "F").WithArguments("System.Reflection.FieldInfo", "GetFieldFromHandle").WithLocation(42, 36)
                    );
            }
        }

        [WorkItem(797996, "DevDiv")]
        [Fact]
        public void MissingMember_System_Reflection_MethodBase__GetMethodFromHandle()
        {
            var text =
@"using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
namespace System
{
    public class Object { }
    public struct Void { }
    public class ValueType { }
    public class MulticastDelegate { }
    public struct IntPtr { }
    public struct Int32 { }
    public struct Nullable<T> { }
    public class Type { }
}
namespace System.Collections.Generic
{
    public interface IEnumerable<T> { }
}
namespace System.Linq.Expressions
{
    public class Expression
    {
        public static Expression Constant(object o, Type t) { return null; }
        public static Expression Call(Expression e, MethodInfo m, Expression[] args) { return null; }
        public static Expression<T> Lambda<T>(Expression e, Expression[] args) { return null; }
        public static Expression New(ConstructorInfo c, IEnumerable<Expression> args) { return null; }
    }
    public class Expression<T> { }
    public class ParameterExpression : Expression { }
}
namespace System.Reflection
{
    public class ConstructorInfo { }
    public class MethodInfo { }
}
delegate void D();
class A
{
    static Expression<D> F = () => new A(null);
    static Expression<D> G = () => M();
    static void M() { }
    A(object o) { }
}
class B<T>
{
    static Expression<D> F = () => new B<object>(null);
    static Expression<D> G = () => M();
    static void M() { }
    B(object o) { }
}";
            var compilation = CreateCompilation(text);
            compilation.VerifyDiagnostics();
            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                result.Diagnostics.Verify(
    // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
    Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
    // (39,36): error CS0656: Missing compiler required member 'System.Reflection.MethodBase.GetMethodFromHandle'
    //     static Expression<D> F = () => new A(null);
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "new A(null)").WithArguments("System.Reflection.MethodBase", "GetMethodFromHandle").WithLocation(39, 36),
    // (40,36): error CS0656: Missing compiler required member 'System.Reflection.MethodBase.GetMethodFromHandle'
    //     static Expression<D> G = () => M();
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "M()").WithArguments("System.Reflection.MethodBase", "GetMethodFromHandle").WithLocation(40, 36),
    // (46,36): error CS0656: Missing compiler required member 'System.Reflection.MethodBase.GetMethodFromHandle'
    //     static Expression<D> F = () => new B<object>(null);
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "new B<object>(null)").WithArguments("System.Reflection.MethodBase", "GetMethodFromHandle").WithLocation(46, 36),
    // (47,36): error CS0656: Missing compiler required member 'System.Reflection.MethodBase.GetMethodFromHandle'
    //     static Expression<D> G = () => M();
    Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "M()").WithArguments("System.Reflection.MethodBase", "GetMethodFromHandle").WithLocation(47, 36)
                    );
            }
        }

        [WorkItem(957927, "DevDiv")]
        [Fact]
        public void Bug957927()
        {
            string source =
@"
using System;
using System.Linq.Expressions;

class Test
{
    static void Main()
    {
        System.Console.WriteLine(GetFunc<int>()().ToString());
    }

	static Func<Expression<Func<T,T>>> GetFunc<T>()
	{
		int x = 10;
		return ()=> { int y = x; return (T m)=>  m;};
	}	
}";

            string expectedOutput = @"m => m";

            CompileAndVerify(
                new[] { source },
                new[] { ExpressionAssemblyRef },
                expectedOutput: expectedOutput);
        }

        #endregion Regression Tests

        #region helpers

        public string TrimExpectedOutput(string expectedOutput)
        {
            char[] delimit = { '\n' };
            string trimmedOutput = null;
            string[] expected_strs = expectedOutput.Trim().Split(delimit);

            foreach (string expected_string in expected_strs)
            {
                trimmedOutput = trimmedOutput + expected_string.Trim() + '\n';
            }

            return trimmedOutput;
        }

        const string TreeWalkerLib = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

public class ExpressionVisitor
{

    public string toStr;

    public ExpressionVisitor()
    {
        toStr = null;
    }


    internal virtual Expression Visit(Expression exp)
    {
        if (exp == null)
            return exp;

        toStr = toStr + exp.NodeType + "":\n"";
        toStr = toStr + ""Type->"" + exp.Type + ""\n"";

        switch (exp.NodeType)
        {
            case ExpressionType.Negate:
            case ExpressionType.UnaryPlus:
            case ExpressionType.NegateChecked:
            case ExpressionType.Not:
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
            case ExpressionType.ArrayLength:
            case ExpressionType.Quote:
            case ExpressionType.TypeAs:
                return this.VisitUnary((UnaryExpression)exp);
            case ExpressionType.Add:
            case ExpressionType.AddChecked:
            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
            case ExpressionType.Divide:
            case ExpressionType.Modulo:
            case ExpressionType.And:
            case ExpressionType.AndAlso:
            case ExpressionType.Or:
            case ExpressionType.OrElse:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.Coalesce:
            case ExpressionType.ArrayIndex:
            case ExpressionType.RightShift:
            case ExpressionType.LeftShift:
            case ExpressionType.ExclusiveOr:
                return this.VisitBinary((BinaryExpression)exp);
            case ExpressionType.TypeIs:
                return this.VisitTypeIs((TypeBinaryExpression)exp);
            case ExpressionType.Conditional:
                return this.VisitConditional((ConditionalExpression)exp);
            case ExpressionType.Constant:
                return this.VisitConstant((ConstantExpression)exp);
            case ExpressionType.Parameter:
                return this.VisitParameter((ParameterExpression)exp);
            case ExpressionType.MemberAccess:
                return this.VisitMemberAccess((MemberExpression)exp);
            case ExpressionType.Call:
                return this.VisitMethodCall((MethodCallExpression)exp);
            case ExpressionType.Lambda:
                return this.VisitLambda((LambdaExpression)exp);
            case ExpressionType.New:
                return this.VisitNew((NewExpression)exp);
            case ExpressionType.NewArrayInit:
            case ExpressionType.NewArrayBounds:
                return this.VisitNewArray((NewArrayExpression)exp);
            case ExpressionType.Invoke:
                return this.VisitInvocation((InvocationExpression)exp);
            case ExpressionType.MemberInit:
                return this.VisitMemberInit((MemberInitExpression)exp);
            case ExpressionType.ListInit:
                return this.VisitListInit((ListInitExpression)exp);
            default:
                return null;
        }
    }

    internal virtual MemberBinding VisitBinding(MemberBinding binding)
    {

        toStr = toStr + ""MemberBindingType->"" + binding.BindingType + ""\n"";
        toStr = toStr + ""Member->"" + binding.Member + ""\n"";

        switch (binding.BindingType)
        {
            case MemberBindingType.Assignment:
                return this.VisitMemberAssignment((MemberAssignment)binding);
            case MemberBindingType.MemberBinding:
                return this.VisitMemberMemberBinding((MemberMemberBinding)binding);
            case MemberBindingType.ListBinding:
                return this.VisitMemberListBinding((MemberListBinding)binding);
            default:
                return null;
        }
    }

    internal virtual ElementInit VisitElementInitializer(ElementInit initializer)
    {
        toStr = toStr + ""AddMethod->"" + initializer.AddMethod + ""\n"";
        toStr = toStr + ""Arguments->"" + ""\n"";
        ReadOnlyCollection<Expression> arguments = this.VisitExpressionList(initializer.Arguments);
        if (arguments != initializer.Arguments)
        {
            return Expression.ElementInit(initializer.AddMethod, arguments);
        }
        return initializer;
    }

    internal virtual Expression VisitUnary(UnaryExpression u)
    {
        toStr = toStr + ""Method->"" + u.Method + ""\n"";
        toStr = toStr + ""IsLifted->"" + u.IsLifted + ""\n"";
        toStr = toStr + ""IsLiftedToNull->"" + u.IsLiftedToNull + ""\n"";
        toStr = toStr + ""Operand->"" + ""\n"";
        Expression operand = this.Visit(u.Operand);
        if (operand != u.Operand)
        {
            return Expression.MakeUnary(u.NodeType, operand, u.Type);
        }
        return u;
    }

    internal virtual Expression VisitBinary(BinaryExpression b)
    {
        toStr = toStr + ""Method->"" + b.Method + ""\n"";
        toStr = toStr + ""IsLifted->"" + b.IsLifted + ""\n"";
        toStr = toStr + ""IsLiftedToNull->"" + b.IsLiftedToNull + ""\n"";

        toStr = toStr + ""Left->"" + ""\n"";
        Expression left = this.Visit(b.Left);

        toStr = toStr + ""Right->"" + ""\n"";
        Expression right = this.Visit(b.Right);

        if (b.NodeType == ExpressionType.Coalesce)
        {
            toStr = toStr + ""Conversion->"" + ""\n"";
            Expression conversion = this.Visit(b.Conversion);
        }

        if (left != b.Left || right != b.Right)
        {
            return Expression.MakeBinary(b.NodeType, left, right);
        }
        return b;
    }

    internal virtual Expression VisitTypeIs(TypeBinaryExpression b)
    {

        toStr = toStr + ""Expression->"" + ""\n"";
        Expression expr = this.Visit(b.Expression);

        toStr = toStr + ""TypeOperand->"" + b.TypeOperand + ""\n"";

        if (expr != b.Expression)
        {
            return Expression.TypeIs(expr, b.TypeOperand);
        }
        return b;
    }

    internal virtual Expression VisitConstant(ConstantExpression c)
    {
        toStr = toStr + ""Value->"" + c.Value + ""\n"";
        return c;
    }

    internal virtual Expression VisitConditional(ConditionalExpression c)
    {

        toStr = toStr + ""Test->"" + ""\n"";
        Expression test = this.Visit(c.Test);

        toStr = toStr + ""IfTrue->"" + ""\n"";
        Expression ifTrue = this.Visit(c.IfTrue);

        toStr = toStr + ""IfFalse->"" + ""\n"";
        Expression ifFalse = this.Visit(c.IfFalse);

        if (test != c.Test || ifTrue != c.IfTrue || ifFalse != c.IfFalse)
        {
            return Expression.Condition(test, ifTrue, ifFalse);
        }
        return c;
    }

    internal virtual Expression VisitParameter(ParameterExpression p)
    {
        toStr = toStr + ""Name->"" + p.Name + ""\n"";
        return p;
    }

    internal virtual Expression VisitMemberAccess(MemberExpression m)
    {

        toStr = toStr + ""Expression->"" + ""\n"";
        Expression exp = this.Visit(m.Expression);

        toStr = toStr + ""Member->"" + m.Member + ""\n"";

        if (exp != m.Expression)
        {
            return Expression.MakeMemberAccess(exp, m.Member);
        }
        return m;
    }

    internal virtual Expression VisitMethodCall(MethodCallExpression m)
    {

        toStr = toStr + ""MethodInfo->"" + m.Method + ""\n"";

        toStr = toStr + ""Object->"" + ""\n"";
        Expression obj = this.Visit(m.Object);

        toStr = toStr + ""Arguments->"" + ""\n"";
        IEnumerable<Expression> args = this.VisitExpressionList(m.Arguments);
        if (obj != m.Object || args != m.Arguments)
        {
            return Expression.Call(obj, m.Method, args);
        }
        return m;
    }

    internal virtual ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
    {
        List<Expression> list = null;
        for (int i = 0, n = original.Count; i < n; i++)
        {
            Expression p = this.Visit(original[i]);
            if (list != null)
            {
                list.Add(p);
            }
            else if (p != original[i])
            {
                list = new List<Expression>(n);
                for (int j = 0; j < i; j++)
                {
                    list.Add(original[j]);
                }
                list.Add(p);
            }
        }
        if (list != null)
            return list.ToReadOnlyCollection();
        return original;
    }

    internal virtual ReadOnlyCollection<ParameterExpression> VisitParamExpressionList(ReadOnlyCollection<ParameterExpression> original)
    {
        List<ParameterExpression> list = null;
        for (int i = 0, n = original.Count; i < n; i++)
        {
            ParameterExpression p = (ParameterExpression)this.Visit(original[i]);
            if (list != null)
            {
                list.Add(p);
            }
            else if (p != original[i])
            {
                list = new List<ParameterExpression>(n);
                for (int j = 0; j < i; j++)
                {
                    list.Add(original[j]);
                }
                list.Add(p);
            }
        }
        if (list != null)
            return list.ToReadOnlyCollection();
        return original;
    }

    internal virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
    {
        toStr = toStr + ""Expression->"" + ""\n"";
        Expression e = this.Visit(assignment.Expression);

        if (e != assignment.Expression)
        {
            return Expression.Bind(assignment.Member, e);
        }
        return assignment;
    }

    internal virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
    {
        toStr = toStr + ""Bindings->"" + ""\n"";
        IEnumerable<MemberBinding> bindings = this.VisitBindingList(binding.Bindings);

        if (bindings != binding.Bindings)
        {
            return Expression.MemberBind(binding.Member, bindings);
        }
        return binding;
    }

    internal virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
    {
        toStr = toStr + ""Initiailizers->"" + ""\n"";
        IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(binding.Initializers);

        if (initializers != binding.Initializers)
        {
            return Expression.ListBind(binding.Member, initializers);
        }
        return binding;
    }

    internal virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
    {
        List<MemberBinding> list = null;
        for (int i = 0, n = original.Count; i < n; i++)
        {
            MemberBinding b = this.VisitBinding(original[i]);
            if (list != null)
            {
                list.Add(b);
            }
            else if (b != original[i])
            {
                list = new List<MemberBinding>(n);
                for (int j = 0; j < i; j++)
                {
                    list.Add(original[j]);
                }
                list.Add(b);
            }
        }
        if (list != null)
            return list;
        return original;
    }

    internal virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
    {
        List<ElementInit> list = null;
        for (int i = 0, n = original.Count; i < n; i++)
        {
            ElementInit init = this.VisitElementInitializer(original[i]);
            if (list != null)
            {
                list.Add(init);
            }
            else if (init != original[i])
            {
                list = new List<ElementInit>(n);
                for (int j = 0; j < i; j++)
                {
                    list.Add(original[j]);
                }
                list.Add(init);
            }
        }
        if (list != null)
            return list;
        return original;
    }

    internal virtual Expression VisitLambda(LambdaExpression lambda)
    {
        toStr = toStr + ""Parameters->"" + ""\n"";
        IEnumerable<ParameterExpression> parms = this.VisitParamExpressionList(lambda.Parameters);
        toStr = toStr + ""Body->"" + ""\n"";
        Expression body = this.Visit(lambda.Body);
        if (body != lambda.Body)
        {
            return Expression.Lambda(lambda.Type, body, lambda.Parameters);
        }
        return lambda;
    }

    internal virtual NewExpression VisitNew(NewExpression nex)
    {
        toStr = toStr + ""Constructor->"" + nex.Constructor + ""\n"";

        toStr = toStr + ""Arguments->"" + ""\n"";
        IEnumerable<Expression> args = this.VisitExpressionList(nex.Arguments);
        if (args != nex.Arguments)
        {
            return Expression.New(nex.Constructor, args);
        }
        return nex;
    }

    internal virtual Expression VisitMemberInit(MemberInitExpression init)
    {
        toStr = toStr + ""NewExpression->"" + ""\n"";
        NewExpression n = (NewExpression)this.Visit(init.NewExpression);

        toStr = toStr + ""Bindings->"" + ""\n"";
        IEnumerable<MemberBinding> bindings = this.VisitBindingList(init.Bindings);

        if (n != init.NewExpression || bindings != init.Bindings)
        {
            return Expression.MemberInit(n, bindings);
        }
        return init;
    }

    internal virtual Expression VisitListInit(ListInitExpression init)
    {
        toStr = toStr + ""NewExpression->"" + ""\n"";
        NewExpression n = (NewExpression)this.Visit(init.NewExpression);

        toStr = toStr + ""Initializers->"" + ""\n"";
        IEnumerable<ElementInit> initializers = this.VisitElementInitializerList(init.Initializers);

        if (n != init.NewExpression || initializers != init.Initializers)
        {
            return Expression.ListInit(n, initializers);
        }
        return init;
    }

    internal virtual Expression VisitNewArray(NewArrayExpression na)
    {
        toStr = toStr + ""Expressions->"" + ""\n"";
        IEnumerable<Expression> exprs = this.VisitExpressionList(na.Expressions);

        if (exprs != na.Expressions)
        {
            if (na.NodeType == ExpressionType.NewArrayInit)
            {
                return Expression.NewArrayInit(na.Type.GetElementType(), exprs);
            }
            else
            {
                return Expression.NewArrayBounds(na.Type.GetElementType(), exprs);
            }
        }
        return na;
    }

    internal virtual Expression VisitInvocation(InvocationExpression iv)
    {
        toStr = toStr + ""Arguments->"" + ""\n"";
        IEnumerable<Expression> args = this.VisitExpressionList(iv.Arguments);

        toStr = toStr + ""Lambda->"" + ""\n"";
        Expression expr = this.Visit(iv.Expression);
        if (args != iv.Arguments || expr != iv.Expression)
        {
            return Expression.Invoke(expr, args);
        }
        return iv;
    }
}

internal static class ReadOnlyCollectionExtensions
{
    internal static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> sequence)
    {
        if (sequence == null)
            return DefaultReadOnlyCollection<T>.Empty;
        ReadOnlyCollection<T> col = sequence as ReadOnlyCollection<T>;
        if (col != null)
            return col;
        IList<T> list = sequence as IList<T>;
        if (list != null)
            return new ReadOnlyCollection<T>(list);
        return new ReadOnlyCollection<T>(new List<T>(sequence));
    }
    private static class DefaultReadOnlyCollection<T>
    {
        private static ReadOnlyCollection<T> _defaultCollection;
        internal static ReadOnlyCollection<T> Empty
        {
            get
            {
                if (_defaultCollection == null)
                    _defaultCollection = new ReadOnlyCollection<T>(new T[] { });
                return _defaultCollection;
            }
        }
    }
}";
        #endregion helpers
    }
}
