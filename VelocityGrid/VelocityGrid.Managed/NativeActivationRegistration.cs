using System;
using System.IO;
using System.Linq;
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
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ActivateInstance(nint factory, out nint instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetIids(nint instance, out uint count, out nint iids);

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
                // The generated projection now follows its normal activation and
                // COM-registration path, with only factory discovery redirected
                // to the DLL shipped beside the application.
                try
                {
                    return new VelocityGrid_Native.VelocityGrid();
                }
                catch (Exception error)
                {
                    throw new InvalidOperationException(
                        $"Native instance interface report: {GetNativeInterfaceReport(getFactory, runtimeClassName)}", error);
                }
            }
            finally
            {
                ActivationFactory.ActivationHandler = previousHandler;
            }
        }
    }

    private static string GetNativeInterfaceReport(GetActivationFactory getFactory, string runtimeClassName)
    {
        nint className = 0;
        nint factory = 0;
        nint instance = 0;
        nint iidArray = 0;
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(
                runtimeClassName, (uint)runtimeClassName.Length, out className));
            Marshal.ThrowExceptionForHR(getFactory(className, out factory));
            nint factoryVtable = Marshal.ReadIntPtr(factory);
            var activate = Marshal.GetDelegateForFunctionPointer<ActivateInstance>(
                Marshal.ReadIntPtr(factoryVtable, 6 * IntPtr.Size));
            Marshal.ThrowExceptionForHR(activate(factory, out instance));

            nint instanceVtable = Marshal.ReadIntPtr(instance);
            var getIids = Marshal.GetDelegateForFunctionPointer<GetIids>(
                Marshal.ReadIntPtr(instanceVtable, 3 * IntPtr.Size));
            Marshal.ThrowExceptionForHR(getIids(instance, out uint count, out iidArray));
            var iids = Enumerable.Range(0, checked((int)count))
                .Select(index => Marshal.PtrToStructure<Guid>(iidArray + index * Marshal.SizeOf<Guid>()))
                .Select(iid => iid.ToString("D"));
            return $"expected f3110a51-b42c-5a95-9770-a8bf7c33a1b9; exposed [{string.Join(", ", iids)}]";
        }
        catch (Exception error)
        {
            return $"inspection failed with 0x{error.HResult:X8}: {error.Message}";
        }
        finally
        {
            if (iidArray != 0) Marshal.FreeCoTaskMem(iidArray);
            if (instance != 0) Marshal.Release(instance);
            if (factory != 0) Marshal.Release(factory);
            if (className != 0) WindowsDeleteString(className);
        }
    }

}
