namespace Media.Common.Loggers
{
    public class DebugLogger : BaseLogger
    {
        internal static void CoreWrite(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            System.Diagnostics.Debug.WriteLine(message);
        }

        public override void LogException(System.Exception ex)
        {
            CoreWrite(ex.Message);
        }

        public override void Log(string data)
        {
            CoreWrite(data);
        }
    }
}
