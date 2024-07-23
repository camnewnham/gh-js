using System.Linq;
using System.Text.RegularExpressions;

namespace JavascriptForGrasshopper.Templating
{
    internal static class TypescriptSupport
    {
        private static readonly Regex variableNameRegex = new Regex("^[a-zA-Z][a-zA-Z0-9_]*$");

        public static bool IsValidVariableName(string var)
        {
            if (string.IsNullOrEmpty(var))
            {
                return false;
            }

            if (!variableNameRegex.IsMatch(var))
            {
                return false;
            }

            if (ReservedKeywords.Contains(var))
            {
                return false;
            }

            return true;
        }

        public static string[] ReservedKeywords = new string[] {
            "out",
            "break",
            "case",
            "catch",
            "class",
            "const",
            "continue",
            "debugger",
            "default",
            "delete",
            "do",
            "else",
            "enum",
            "export",
            "extends",
            "false",
            "finally",
            "for",
            "function",
            "if",
            "import",
            "in",
            "instanceof",
            "new",
            "null",
            "return",
            "super",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "var",
            "void",
            "while",
            "with",
            "as",
            "implements",
            "interface",
            "let",
            "package",
            "private",
            "protected",
            "public",
            "static",
            "yield",
            "any",
            "boolean",
            "constructor",
            "declare",
            "get",
            "module",
            "require",
            "number",
            "set",
            "string",
            "symbol",
            "type",
            "from",
            "of",
            "namespace",
            "async",
            "await"
        };
    }
}
