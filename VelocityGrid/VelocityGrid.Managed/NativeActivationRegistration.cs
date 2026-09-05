using System;
using System.IO;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;

namespace VelocityGrid.Managed;

/// <summary>
/// Loads the packaged native WinRT factory directly. This avoids requiring an
/// application manifest or machine-wide registration in unpackaged hosts.
/// </summary>
internal static class NativeActivationRegistration
{
    private static readonly object Sync = new();
    private static nint s_module;
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetActivationFactory(nint activatableClassId, out nint factory);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString, uint length, out nint hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint hstring);

    internal static VelocityGrid_Native.VelocityGrid CreateGrid()
    {
        lock (Sync)
        {
            string nativePath = Path.Combine(AppContext.BaseDirectory, "VelocityGrid.Native.dll");
            if (s_module == 0) s_module = NativeLibrary.Load(nativePath);
            nint callback = NativeLibrary.GetExport(s_module, "DllGetActivationFactory");
            var getFactory = Marshal.GetDelegateForFunctionPointer<GetActivationFactory>(callback);
            const string runtimeClassName = "VelocityGrid_Native.VelocityGrid";
            var previousHandler = ActivationFactory.ActivationHandler;
            ActivationFactory.ActivationHandler = (typeName, iid) =>
            {
                if (!string.Equals(typeName, runtimeClassName, StringComparison.Ordinal))
                    return previousHandler?.Invoke(typeName, iid) ?? 0;

                Marshal.ThrowExceptionForHR(WindowsCreateString(
                    runtimeClassName, (uint)runtimeClassName.Length, out nint className));
                nint factory = 0;
                try
                {
                    Marshal.ThrowExceptionForHR(getFactory(className, out factory));
                    if (iid == ABI.WinRT.Interop.IActivationFactoryMethods.IID)
                        return factory;

                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(factory, in iid, out nint requested));
                    Marshal.Release(factory);
                    factory = 0;
                    return requested;
                }
                finally
                {
                    if (factory != 0 && iid != ABI.WinRT.Interop.IActivationFactoryMethods.IID)
                        Marshal.Release(factory);
                    WindowsDeleteString(className);
                }
            };
            try
            {
                // The generated projection follows its normal activation path;
                // only factory discovery is redirected to the app-local DLL.
                return new VelocityGrid_Native.VelocityGrid();
            }
            finally
            {
                ActivationFactory.ActivationHandler = previousHandler;
            }
        }
    }
}
