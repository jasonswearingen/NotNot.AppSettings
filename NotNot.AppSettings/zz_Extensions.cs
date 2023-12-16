using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace NotNot.AppSettings;

internal static class zz_Extensions
{
   private static string _GenId = Assembly.GetExecutingAssembly().GetName().Name.Replace('.','_');
   public static void _Warn(this SourceProductionContext context, string message, string title = "_Warn")
   {

      _ReportDiagHelper(context, message, title, DiagnosticSeverity.Warning);
   }
   public static void _Error(this SourceProductionContext context, string message, string title = "_Error")
   {
      _ReportDiagHelper(context, message, title, DiagnosticSeverity.Error);
   }
   public static void _Info(this SourceProductionContext context, string message, string title = "_Info")
   {
      _ReportDiagHelper(context, message, title, DiagnosticSeverity.Info);
   }

   private static void _ReportDiagHelper(SourceProductionContext context, string message, string title, DiagnosticSeverity severity)
   {
      context.ReportDiagnostic(Diagnostic.Create(
         new DiagnosticDescriptor(_GenId,
            title,
            "{0}",
            "Debug",
            severity,
            isEnabledByDefault: true),
         Location.None,
         message));
   }
   public static void _Warn(this List<Diagnostic> diagReport, string message, string title = "_Warn")
   {
      _ReportDiagHelper(diagReport, message, title, DiagnosticSeverity.Warning);
   }
   public static void _Error(this List<Diagnostic> diagReport, string message, string title = "_Error")
   {
      _ReportDiagHelper(diagReport, message, title, DiagnosticSeverity.Error);
   }
   public static void _Info(this List<Diagnostic> diagReport, string message, string title = "_Info")
   {
      _ReportDiagHelper(diagReport, message, title, DiagnosticSeverity.Info);
   }

   private static void _ReportDiagHelper(List<Diagnostic> diagReport, string message, string title, DiagnosticSeverity severity)
   {
      diagReport.Add(Diagnostic.Create(
         new DiagnosticDescriptor(_GenId,
            title,
            "{0}",
            "Debug",
            severity,
            isEnabledByDefault: true),
         Location.None,
         message));
   }


   /// <summary>
   ///    converts a string to a stripped down version, only allowing alphaNumeric plus a single whiteSpace character
   ///    (customizable with default being '_' )
   ///    <para>
   ///       note: leading and trailing whiteSpace is trimmed, and internal whiteSpace is truncated down to single
   ///       characters
   ///    </para>
   ///    <para>This can be used to "safe encode" strings before use with xml</para>
   ///    <para>example:  "Hello, World!" ==> "Hello_World"</para>
   /// </summary>
   /// <param name="toConvert"></param>
   /// <param name="whiteSpace">
   ///    char to use as whiteSpace.  set to null to not write any whiteSpace (alphaNumeric chars only)
   ///    default is underscore '_'
   /// </param>
   /// <returns></returns>
   public static string _ConvertToAlphanumericCaps(this string toConvert, char? whiteSpace = '_')
   {
      var sb = new StringBuilder(toConvert.Length);

      bool includeWhitespace = whiteSpace.HasValue;
      bool isWhitespace = false;
      foreach (var c in toConvert)
      {
         if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
         {
            sb.Append(c);
            isWhitespace = false;
         }
         else
         {
            if (!isWhitespace && includeWhitespace)
            {
               sb.Append(whiteSpace.Value);
            }

            isWhitespace = true;
         }
      }

      

      string toReturn = sb.ToString();

      if (includeWhitespace)
      {
         toReturn = toReturn.Trim(whiteSpace.Value);
      }
      if (toReturn.Length > 0)
      {
         toReturn = Char.ToUpper(toReturn[0]) + toReturn.Substring(1);
      }
      return toReturn;
   }
}