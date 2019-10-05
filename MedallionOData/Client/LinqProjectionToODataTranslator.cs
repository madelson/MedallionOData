using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Medallion.OData;
using Medallion.OData.Trees;
using MemberPath = System.Collections.Generic.IReadOnlyList<System.Reflection.MemberInfo>;

namespace Medallion.OData.Client
{
    internal partial class LinqToODataTranslator
    {
        /// <summary>
        /// Helper class for dealing with translating members across projections. This is difficult because OData has no notion of projection except
        /// for a final column selection
        /// </summary>
        private class MemberAndParameterTranslator
        {
            private static readonly IEqualityComparer<MemberPath> MemberPathComparer = EqualityComparers.Create<MemberPath>(
                (l1, l2) => l1.SequenceEqual(l2, Helpers.MemberComparer),
                l => l.Aggregate(0, (s, m) => s ^ Helpers.MemberComparer.GetHashCode(m))
            );

            /// <summary>
            /// Tracks all projections which have been applied via SELECTs. This is needed to allow for a client-side projection delegate to be generated
            /// (OData doesn't support projection except for column selection and inclusion)
            /// </summary>
            private readonly List<LambdaExpression> _projectionList = new List<LambdaExpression>();
            /// <summary>
            /// This keeps track of the current set of mappings between member paths (e. g. a.b.c) and translations. 
            /// The reason we need this is that we can't actually translate every projection directly. For example, if we have 
            /// <code>q.Select(a => new { x = new { a.Foo } ... }).Select(t => t.x)</code>, we can't translate t.x directly into OData.
            /// We can, however, remember all translations that could derive from t.x (in this case x.Foo), so that any time a property
            /// of x is referenced we can provide the correct translation.
            /// 
            /// Note that this could just be a variable to hold the most current value, but keeping the entire stack aids in debugging
            /// </summary>
            private readonly Stack<Dictionary<MemberPath, ODataExpression>> _pathStack = new Stack<Dictionary<MemberPath, ODataExpression>>();
            private readonly LinqToODataTranslator _translator;

            public MemberAndParameterTranslator(LinqToODataTranslator translator)
            {
                this._translator = translator;
            }

            #region ---- Public API ----
            public ODataExpression TranslateParameter(ParameterExpression parameterExpression)
            {
                if (this._pathStack.Count == 0)
                {
                    // we represent the root parameter, as in q.Where(a => a.Flag) as null
                    return null;
                }

                // otherwise, we can only evaluate a parameter if we have a (empty) path for it in the mapping
                // this will happen in cases like q.Select(a => a.Text).Where(t => t.Length > 0)
                if (!this._pathStack.Peek().TryGetValue(Empty<MemberInfo>.Array, out var result))
                {
                    throw new ODataCompileException("Could not translate parameter of type " + parameterExpression.Type + " to OData in this context!");
                }
                return result;
            }

            public ODataExpression TranslateMemberAccess(MemberExpression memberAccess)
            {
                // translate special properties
                if (this.TryTranslateMemberAccessAsSpecialMember(memberAccess, out var result))
                {
                    return result;
                }

                // otherwise, try to translate as a member access chain going back to a parameter. Since the only special members
                // we support return types with no members (primitive types), this is a safe assertion
                var memberAccessChain = Traverse.Along(memberAccess, me => me.Expression as MemberExpression)
                    .Reverse()
                    .ToArray();
                if (memberAccessChain[0].Expression.NodeType != ExpressionType.Parameter)
                {
                    throw new ODataCompileException("Cannot compile member access path '" + memberAccessChain.Select(me => me.Member.Name).ToDelimitedString(".") + "' to OData: must start with a lambda parameter");
                }

                // attempt to translate the full path by referencing the mapping in the path stack
                if (this._pathStack.Count > 0 && this._pathStack.Peek().TryGetValue(memberAccessChain.Select(me => me.Member).ToArray(), out result))
                {
                    return result;
                }

                // translate the sub path, and then attempt to apply the current member
                // for example, if the path was param.A.B, then we'd translate param.A and then try to translate B as a property of A
                var instance = this._translator.TranslateInternal(memberAccess.Expression);
                var property = memberAccess.Member as PropertyInfo;
                if (property == null)
                {
                    throw new ODataCompileException("Only properties are supported. Found: " + memberAccess.Member);
                }
                if ((property.GetMethod ?? property.SetMethod).IsStatic)
                {
                    // we don't really expect to hit this case in practice, because static properties should be evaluated in memory 
                    // rather than translated
                    throw new ODataCompileException("Static properties are not supported. Found: " + property);
                }
                if (instance == null || instance.Kind == ODataExpressionKind.MemberAccess)
                {
                    return ODataExpression.MemberAccess((ODataMemberAccessExpression)instance, property);
                }
                
                throw new ODataCompileException("Property " + property + " is not supported in OData");
            }

            /// <summary>
            /// Registers a lambda projection so that it can be used to inform further translations
            /// </summary>
            public void RegisterProjection(LambdaExpression projection)
            {
                this._pathStack.Push(this.ParseProjection(projection));
                this._projectionList.Add(projection);
            }

            /// <summary>
            /// Returns a <see cref="LambdaExpression"/> representing the final client-side projection to be applied to the returned result
            /// </summary>
            public LambdaExpression GetFinalProjection()
            {
                var result = this._projectionList.Count > 0
                    ? ClientSideProjectionBuilder.CreateProjection(this._projectionList)
                    : null;
                return result;
            }

            /// <summary>
            /// Determines which properties of the root parameter were actually referenced in the final projection so that
            /// we can create a $select filter
            /// </summary>
            public ISet<MemberPath> GetReferencedMemberPathsInFinalProjection()
            {
                if (this._pathStack.Count == 0)
                {
                    return null; // select everything
                }

                var visitor = new MemberPathFinder();
                var referencedPaths = this._pathStack.Peek()
                    .Values
                    .SelectMany(visitor.FindPaths)
                    .ToSet(MemberPathComparer);
                return referencedPaths;
            }   
            #endregion

            /// <summary>
            /// Attempts to translate the given <see cref="MemberExpression"/> as a special OData member, like <see cref="Nullable{T}.Value"/> or
            /// <see cref="string.Length"/>.
            /// </summary>
            private bool TryTranslateMemberAccessAsSpecialMember(MemberExpression memberAccess, out ODataExpression result)
            {
                // TODO FUTURE null handling?
                Func<IEnumerable<ODataExpression>> translateInstance = () => this._translator.TranslateInternal(memberAccess.Expression).Enumerate();
                if (memberAccess.Member.DeclaringType == typeof(string) && memberAccess.Member.Name == "Length")
                {
                    result = ODataExpression.Call(ODataFunction.Length, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Year")
                {
                    result = ODataExpression.Call(ODataFunction.Year, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Month")
                {
                    result = ODataExpression.Call(ODataFunction.Month, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Day")
                {
                    result = ODataExpression.Call(ODataFunction.Day, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Hour")
                {
                    result = ODataExpression.Call(ODataFunction.Hour, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Minute")
                {
                    result = ODataExpression.Call(ODataFunction.Minute, translateInstance());
                }
                else if (memberAccess.Member.DeclaringType == typeof(DateTime) && memberAccess.Member.Name == "Second")
                {
                    result = ODataExpression.Call(ODataFunction.Second, translateInstance());
                }
                // nullable properties
                else if (memberAccess.Member.Name == "HasValue" && memberAccess.Member.DeclaringType.IsGenericOfType(typeof(Nullable<>)))
                {
                    // for HasValue we just re-translate expr != null
                    result = this._translator.TranslateInternal(Expression.NotEqual(memberAccess.Expression, Expression.Constant(null, memberAccess.Expression.Type)));
                }
                else if (memberAccess.Member.Name == "Value" && memberAccess.Member.DeclaringType.IsGenericOfType(typeof(Nullable<>)))
                {
                    // .Value calls can just be ignored, since OData doesn't have the notion of nullable types
                    result = this._translator.TranslateInternal(memberAccess.Expression);
                }
                else
                {
                    result = null;
                    return false;
                }

                return true;
            }

            private Dictionary<IReadOnlyList<MemberInfo>, ODataExpression> ParseProjection(LambdaExpression projection)
            {
                Throw.If(projection.Parameters.Count != 1, "projection: must have 1 parameter");

                var result = this.ParseProjectionBody(projection.Body);
                return result;
            }

            private Dictionary<MemberPath, ODataExpression> ParseProjectionBody(Expression body)
            {
                var isAnonymousTypeProjection = body.NodeType == ExpressionType.New && body.Type.IsAnonymous();
                var isObjectInitializerProjection = body.NodeType == ExpressionType.MemberInit;             
                if (isAnonymousTypeProjection || isObjectInitializerProjection)
                {
                    Dictionary<MemberPath, Expression> pathToLinqMapping;
                    if (isAnonymousTypeProjection) // anonymous, like a => new { x = a.B }
                    {
                        // anonymous type creation is actually a new using a constructor whose arguments match the anonymous properties
                        var @new = (NewExpression)body;
                        pathToLinqMapping = @new.Constructor.GetParameters()
                            .Select((v, i) => new { Value = v, Index = i })
                            .ToDictionary(
                                t => @new.Type.GetMember(t.Value.Name).As<MemberPath>(), 
                                t => @new.Arguments[t.Index]
                            );
                    }
                    else
                    {
                        // initializer, like a => new X { Foo = a.B }
                        var memberInit = (MemberInitExpression)body;
                        if (memberInit.NewExpression.Arguments.Count > 0)
                        {
                            throw new ODataCompileException("Only parameterless constructors are supported with object initializers in OData. Found: " + memberInit);
                        }
                        if (memberInit.Bindings.Any(mb => mb.BindingType != MemberBindingType.Assignment))
                        {
                            throw new ODataCompileException("Only member assignment initializers are supported in OData. Found: " + memberInit);
                        }

                        pathToLinqMapping = memberInit.Bindings.Cast<MemberAssignment>()
                            .ToDictionary(
                                mb => new[] { mb.Member }.As<MemberPath>(), 
                                mb => mb.Expression
                        );
                    }

                    // for anonymous and initializer projections, we support nested projections such as
                    // a => new { b = new { c = a.x } } }
                    // To do this, for each property mapping (a.b in the example above), we simply recurse
                    // on the value expression and then add the property prefix to the resulting paths
                    var result = new Dictionary<MemberPath, ODataExpression>(MemberPathComparer);
                    foreach (var kvp in pathToLinqMapping)
                    {
                        var parsed = this.ParseProjectionBody(kvp.Value);
                        result.AddRange(parsed.Select(p => KeyValuePair.Create(kvp.Key.Concat(p.Key).ToArray().As<MemberPath>(), p.Value)));
                    }

                    return result;
                }

                // if we have a path stack and we find a parameter, then we can just copy over all paths from
                // that parameters mapping to the new mapping
                if (this._pathStack.Count > 0 && body.NodeType == ExpressionType.Parameter)
                {
                    // add all paths for the last parameter
                    var result = new Dictionary<MemberPath, ODataExpression>(this._pathStack.Peek(), MemberPathComparer);
                    return result;
                }

                // a member path, where the path stack is non-empty and thus the path won't translate directly
                // for example: a => a.b.x, where a is not the root query parameter
                if (this._pathStack.Count > 0 && body.NodeType == ExpressionType.MemberAccess)
                {
                    // pull out the member path as going back to a parameter (similar to what we do when translating a member)
                    var reverseMemberPath = Traverse.Along((MemberExpression)body, me => me.Expression as MemberExpression)
                        .ToArray();
                    if (reverseMemberPath[reverseMemberPath.Length - 1].Expression.NodeType != ExpressionType.Parameter)
                    {
                        throw new ODataCompileException("Expression '" + reverseMemberPath.Last().Expression + "' could not be compiled to OData as part of a projection");
                    }

                    // find all paths for the current parameter which are prefixes of this path
                    var memberPath = reverseMemberPath.Reverse().Select(me => me.Member).ToArray();
                    var result = this._pathStack.Peek().Where(kvp => StartsWith(kvp.Key, prefix: memberPath))
                        .ToDictionary(
                            // the new mapping has the same path, but without the prefix of the current parameter
                            kvp => kvp.Key.Skip(memberPath.Length).ToArray(),
                            kvp => kvp.Value,
                            MemberPathComparer
                        );
                    // if we didn't find any such paths, then this should be directly translatable. For example:
                    // q.Select(a => a.B).Select(b => b.Id), then no path starts with b.Id. Thus, we fall through to
                    // just translating b.Id
                    if (result.Count > 0)
                    {
                        return result;  
                    }
                }
                
                // finally, if the projection doesn't match any special patterns, then we simply try
                // to translate the projected value directly (e. g. a => a.Id + 2)
                var simpleResult = new Dictionary<MemberPath, ODataExpression>(MemberPathComparer)
                {
                    { Empty<MemberInfo>.Array, this._translator.TranslateInternal(body) },
                };
                return simpleResult;
            }

            private static bool StartsWith(MemberPath path, MemberPath prefix)
            {
                return prefix.Count <= path.Count
                        && path.Zip(prefix, (me1, me2) => Helpers.MemberComparer.Equals(me1, me2))
                                .All(b => b);
            }

            private class MemberPathFinder : ODataExpressionVisitor
            {
                private ISet<MemberPath> _paths;

                public ISet<MemberPath> FindPaths(ODataExpression node)
                {
                    this._paths = new HashSet<MemberPath>();
                    this.Visit(node);
                    return this._paths;
                }

                protected override void VisitMemberAccess(ODataMemberAccessExpression node)
                {
                    var path = Traverse.Along(node, e => e.Expression)
                        .Reverse()
                        .Select(e => e.Member)
                        .ToArray();
                    this._paths.Add(path);
                }
            }
        }
    }
}
