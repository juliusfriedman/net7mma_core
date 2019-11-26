namespace Media.Common.Extensions.MethodBase
{
    public static class MethodBaseExtensions
    {
        internal static System.Type TypeOfValueType = typeof(System.ValueType);

        public static bool IsForValueType(this System.Reflection.MethodBase methodBase) => methodBase.DeclaringType.IsSubclassOf(TypeOfValueType);

        public static bool IsOverride(this System.Reflection.MethodBase methodBase)
        {
            if (!(methodBase is System.Reflection.MethodInfo methodInfo))
                return false;

            return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
        }
    }
}
