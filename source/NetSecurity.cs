using System;
using System.Net;
using System.Runtime.Versioning;

// csc does not emit this. Without it the 4.8 runtime treats the exe as a
// .NET Framework 4.0 app and leaves ServicePointManager on SSL 3.0 / TLS 1.0.
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace Chest
{
    internal static class NetSecurity
    {
        internal static void EnableModernTls()
        {
            try { Apply(); }
            catch (Exception) { }
        }

        static void Apply()
        {
            try
            {
                Type appContext = Type.GetType("System.AppContext");
                if (appContext != null)
                {
                    System.Reflection.MethodInfo set = appContext.GetMethod("SetSwitch", new Type[] { typeof(string), typeof(bool) });
                    if (set != null) set.Invoke(null, new object[] { "Switch.System.Net.DontEnableSystemDefaultTlsVersions", false });
                }
            }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (System.Reflection.TargetInvocationException) { }
            try
            {
                SecurityProtocolType protocols = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
                protocols &= ~SecurityProtocolType.Ssl3;
                ServicePointManager.SecurityProtocol = protocols;
            }
            catch (NotSupportedException)
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }
                catch (NotSupportedException) { }
                catch (TypeInitializationException) { }
            }
            catch (TypeInitializationException) { }
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; }
            catch (NotSupportedException) { }
            catch (TypeInitializationException) { }
        }
    }
}
