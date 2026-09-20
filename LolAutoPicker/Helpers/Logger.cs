using System;

namespace LolAutoPicker.Helpers
{
    public static class Logger
    {
        public static Action<string>? Log;

        public static void Info(string msg)
        {
            Log?.Invoke(msg);
        }
    }
}