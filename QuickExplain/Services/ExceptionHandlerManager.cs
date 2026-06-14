namespace QuickExplain.Services
{
    public static class ExceptionHandlerManager
    {
        private static bool _registered;

        public static void RegisterHandlers()
        {
            if (_registered)
                return;

            _registered = true;

            System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                ErrorLogger.Log("Dispatcher unhandled exception", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                ErrorLogger.Log($"AppDomain unhandled exception (IsTerminating: {e.IsTerminating})", e.ExceptionObject);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                ErrorLogger.Log("Unobserved task exception", e.Exception);
            };
        }
    }
}
