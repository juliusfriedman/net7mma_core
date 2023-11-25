using System;

namespace Media.Common.Loggers
{
    /// <summary>
    /// A <see cref="ILogging"/> implementation which does nothing.
    /// </summary>
    public class NullLogger : BaseLogger
    {
        public static readonly NullLogger Default = new(false);

        public NullLogger(bool shouldDispose) : base(shouldDispose) { }

        public override void Log(string message) { }

        public override void LogException(Exception ex) { }
    }
}
