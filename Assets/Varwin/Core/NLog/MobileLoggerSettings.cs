using UnityEngine;

namespace NLogger
{
    public static class MobileLoggerSettings
    {
        public static bool TraceEnabled;
        public static bool DebugEnabled;
        
        public static void Init()
        {
            // TraceEnabled = Application.isEditor;
            // DebugEnabled = true;
            //
            // var unityDebugLog = new MethodCallTarget(nameof(LogEventAction), LogEventAction);
            //
            // var config = new LoggingConfiguration();
            //
            // if (TraceEnabled)
            // {
            //     config.AddRule(LogLevel.Trace, LogLevel.Trace, unityDebugLog);
            // }
            //
            // if (DebugEnabled)
            // {
            //     config.AddRule(LogLevel.Debug, LogLevel.Debug, unityDebugLog);
            // }
            //
            // config.AddRule(LogLevel.Info, LogLevel.Info, unityDebugLog);
            // config.AddRule(LogLevel.Warn, LogLevel.Warn, unityDebugLog);
            // config.AddRule(LogLevel.Error, LogLevel.Error, unityDebugLog);
            // config.AddRule(LogLevel.Fatal, LogLevel.Fatal, unityDebugLog);
            //
            // LogManager.Configuration = config;
            // LogManager.EnableLogging();
        }

        // private static void LogEventAction(LogEventInfo logEventInfo, object[] objects)
        // {
        //     // if (TraceEnabled && logEventInfo.Level == LogLevel.Trace)
        //     // {
        //     //     Debug.Log(logEventInfo.Message);
        //     // }
        //     // else if (DebugEnabled && logEventInfo.Level == LogLevel.Debug)
        //     // {
        //     //     Debug.Log(logEventInfo.Message);
        //     // }
        //     // else if (logEventInfo.Level == LogLevel.Info)
        //     // {
        //     //     Debug.Log(logEventInfo.Message);
        //     // }
        //     // else if (logEventInfo.Level == LogLevel.Warn)
        //     // {
        //     //     Debug.LogWarning(logEventInfo.Message);
        //     // }
        //     // else if (logEventInfo.Level == LogLevel.Error)
        //     // {
        //     //     Debug.LogError(logEventInfo.Message);
        //     // }
        //     // else if (logEventInfo.Level == LogLevel.Fatal)
        //     // {
        //     //     Debug.LogError(logEventInfo.Message);
        //     // }
        // }
    }
}