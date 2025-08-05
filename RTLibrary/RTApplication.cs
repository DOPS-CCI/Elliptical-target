using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Deployment.Application;
namespace RTLibrary
{
    public class RTApplication : Application
    {
        /// <summary>
        /// Directory in local data files are located; ends in directory separator character
        /// </summary>
        public static string DataDirectory = "./";

        /// <summary>
        /// Deployed version number
        /// </summary>
        public static string Version = null;

        /// <summary>
        /// Constructior for RTApplication; sets ShutdownMode and UnhandledException
        /// </summary>
        public RTApplication()
        {
            //This allows us to open secondary dialogs before the main windows are opened
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Application.Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                Version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                DataDirectory = ApplicationDeployment.CurrentDeployment.DataDirectory + Path.DirectorySeparatorChar;
            }
            else
            {
                DataDirectory = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
            }
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception exc = e.Exception;
            while (exc.InnerException != null) exc = exc.InnerException;
#if RTTrace || RTTraceUAId
            RTClock.ExternalTrace($"In {exc.TargetSite}: {exc.Message}");
            RTClock.trace.Display();
#else
            MessageBox.Show($"In {exc.TargetSite}: {exc.Message}", "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
#endif
            Environment.Exit(-1);
        }

    }
}
