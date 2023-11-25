namespace Media.Common.Extensions.MethodBase
{
    public static class MethodBaseExtensions
    {
        internal static System.Type TypeOfValueType = typeof(System.ValueType);

        public static bool IsForValueType(this System.Reflection.MethodBase methodBase) => methodBase.DeclaringType.IsSubclassOf(TypeOfValueType);

        public static bool IsOverride(this System.Reflection.MethodBase methodBase)
        {
            return methodBase is System.Reflection.MethodInfo methodInfo
&& methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
        }
    }
}
