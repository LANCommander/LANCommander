using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace LANCommander.Helpers
{
    public static class DisplayName
    {
        public static string For<T>(Expression<Func<T>> accessor)
        {
            var expression = (MemberExpression)accessor.Body;
            var value = expression.Member.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;

            return value?.Name ?? expression.Member.Name;
        }
    }
}
