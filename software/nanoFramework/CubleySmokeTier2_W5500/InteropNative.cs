using System.Runtime.CompilerServices;

namespace Cubley.Interop
{
    public static class BringupStatus
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeSet(uint statusWord);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGet();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetLastNativeError();
    }

    public static class W5500Socket
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint NativeGetVersion();
    }
}
