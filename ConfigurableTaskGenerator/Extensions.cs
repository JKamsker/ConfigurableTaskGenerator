using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConfigurableTaskGenerator;
internal static class Extensions
{
    //firstchartolower
    public static string FirstCharToLower(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        var charArray = input.ToCharArray();
        charArray[0] = char.ToLower(charArray[0]);
        return new string(charArray);
    }

    // syntaxnode? enumerate parents
    public static IEnumerable<SyntaxNode> EnumerateParents(this SyntaxNode? node)
    {
        if (node == null)
        {
            yield break;
        }

        var current = node;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    // getfirstparentoftype
    public static T? GetFirstParentOfType<T>(this SyntaxNode? node) where T : SyntaxNode
    {
        return node?.EnumerateParents().OfType<T>().FirstOrDefault();
    }
}
