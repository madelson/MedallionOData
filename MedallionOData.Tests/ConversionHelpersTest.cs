using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests
{
    using Microsoft.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class ConversionHelpersTest
    {
        [TestMethod]
        public void ImplicitlyCastable()
        {
            this.RunTests((from, to) => from.IsImplicitlyCastableTo(to), @implicit: true);
        }

        [TestMethod]
        public void ExplicitlyCastable()
        {
            this.RunTests((from, to) => from.IsCastableTo(to), @implicit: false);
        }

        /// <summary>
        /// Validates the given implementation function for either implicit or explicit conversion
        /// </summary>
        private void RunTests(Func<Type, Type, bool> func, bool @implicit)
        {
            // gather types
            var primitives = typeof(object).Assembly.GetTypes().Where(t => t.IsPrimitive).ToArray();
            var simpleTypes = new[] { typeof(string), typeof(DateTime), typeof(decimal), typeof(object), typeof(DateTimeOffset), typeof(TimeSpan), typeof(StringSplitOptions), typeof(DateTimeKind) };
            var variantTypes = new[] { typeof(string[]), typeof(object[]), typeof(IEnumerable<string>), typeof(IEnumerable<object>), typeof(Func<string>), typeof(Func<object>), typeof(Action<string>), typeof(Action<object>) };
            var conversionOperators = new[] { typeof(Operators), typeof(Operators2), typeof(DerivedOperators), typeof(OperatorsStruct) };
            var typesToConsider = primitives.Concat(simpleTypes).Concat(variantTypes).Concat(conversionOperators).ToArray();
            var allTypesToConsider = typesToConsider.Concat(typesToConsider.Where(t => t.IsValueType).Select(t => typeof(Nullable<>).MakeGenericType(t)));

            // generate test cases
            var cases = this.GenerateTestCases(allTypesToConsider, @implicit);

            // collect errors
            var mistakes = new List<string>();
            foreach (var @case in cases)
            {
                var result = func(@case.Item1, @case.Item2);
                if (result != (@case.Item3 == null))
                {
                    // func(@case.Item1, @case.Item2); // break here for easy debugging
                    mistakes.Add(string.Format("{0} => {1}: got {2} for {3} cast", @case.Item1, @case.Item2, result, @implicit ? "implicit" : "explicit"));
                }
            }
            Assert.IsTrue(mistakes.Count == 0, string.Join(Environment.NewLine, new[] { mistakes.Count + " errors" }.Concat(mistakes)));
        }

        private List<Tuple<Type, Type, CompilerError>> GenerateTestCases(IEnumerable<Type> types, bool @implicit)
        {
            // gather all pairs
            var typeCrossProduct = types.SelectMany(t => types, (from, to) => new { from, to })
                .Select((t, index) => new { t.from, t.to, index })
                .ToArray();

            // create the code to pass to the compiler
            var code = string.Join(
                Environment.NewLine,
                new[] { "namespace A { public class B { static T Get<T>() { return default(T); } public void C() {" }
                .Concat(typeCrossProduct.Select(t => string.Format("{0} var{1} = {2}default({3});", GetName(t.to), t.index, @implicit ? string.Empty : "(" + GetName(t.to) + ")", GetName(t.from))))
                    .Concat(new[] { "}}}" })
            );

            // compile the code
            var provider = new CSharpCodeProvider();
            var compilerParams = new CompilerParameters();
            compilerParams.ReferencedAssemblies.Add(this.GetType().Assembly.Location); // reference the current assembly!
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            var compilationResult = provider.CompileAssemblyFromSource(compilerParams, code);

            // determine the outcome of each conversion by matching compiler errors with conversions by line #
            var cases = typeCrossProduct.GroupJoin(
                    compilationResult.Errors.Cast<CompilerError>(),
                    t => t.index,
                    e => e.Line - 2,
                    (t, e) => Tuple.Create(t.from, t.to, e.FirstOrDefault())
                )
                .ToList();

            // add a special case
            // this can't be verified by the normal means, since it's a private class
            cases.Add(Tuple.Create(typeof(PrivateOperators), typeof(int), default(CompilerError)));

            return cases;
        }

        /// <summary>
        /// Gets a C# name for the given type
        /// </summary>
        private static string GetName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.ToString();
            }

            return string.Format("{0}.{1}<{2}>", type.Namespace, type.Name.Substring(0, type.Name.IndexOf('`')), string.Join(", ", type.GetGenericArguments().Select(GetName)));
        }

        private class PrivateOperators
        {
            public static implicit operator int(PrivateOperators o)
            {
                return 1;
            }
        }
    }

    public class Operators
    {
        public static implicit operator string(Operators o)
        {
            throw new NotImplementedException();
        }

        public static implicit operator int(Operators o)
        {
            return 1;
        }

        public static explicit operator decimal?(Operators o)
        {
            throw new NotImplementedException();
        }

        public static explicit operator StringSplitOptions(Operators o)
        {
            return StringSplitOptions.RemoveEmptyEntries;
        }
    }

    public class DerivedOperators : Operators
    {
        public static explicit operator DateTime(DerivedOperators o)
        {
            return DateTime.Now;
        }
    }

    public struct OperatorsStruct
    {
        public static implicit operator string(OperatorsStruct o)
        {
            throw new NotImplementedException();
        }

        public static implicit operator int(OperatorsStruct o)
        {
            return 1;
        }

        public static explicit operator decimal?(OperatorsStruct o)
        {
            throw new NotImplementedException();
        }

        public static explicit operator StringSplitOptions(OperatorsStruct o)
        {
            return StringSplitOptions.RemoveEmptyEntries;
        }
    }

    public class Operators2
    {
        public static explicit operator bool(Operators2 o)
        {
            return false;
        }

        public static implicit operator Operators2(DerivedOperators o)
        {
            return null;
        }

        public static explicit operator Operators2(int i)
        {
            throw new NotImplementedException();
        }
    }
}
