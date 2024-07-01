public static partial class Func
{
   public static void Eval(this Action toEval)
   {
      toEval();
   }
   public static T Eval<T>(this Func<T> toEval)
   {
      return toEval();
   }
}