using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;

namespace Bifrost.Public.Sdk
{
    public class Log
    {
        public static ILog Logger;

        private static readonly string _logRootDirKey = "logrootdir";

        public static void Configure(FileInfo configFile, DirectoryInfo directory)
        {
            if (Logger == null)
            {
                if (!directory.Exists)
                    directory.Create();

                GlobalContext.Properties[_logRootDirKey] = directory.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                XmlConfigurator.ConfigureAndWatch(configFile);
                Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().Name);
            }
        }

        public static ILog GetLogger(Type loggerType)
        {
            return LogManager.GetLogger(loggerType);
        }
    }
}