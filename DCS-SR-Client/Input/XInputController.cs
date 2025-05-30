using System;
using System.Runtime.InteropServices;
using SharpDX.XInput;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Input;

internal static unsafe class Native
{
    [DllImport("XINPUT1_4.DLL")]
    public static extern uint XInputGetState(uint dwUserIndex, State* pState);
}

/// <summary>
///     Controller class that masquerades as a SharpDX.DirectInput.Device.
/// </summary>
public class XInputController : IDisposable
{
    private State _state;

    public static Guid DeviceGuid => InformationData.ProductGuid;

    // Dispose interface, not much to do.
    public bool IsDisposed { get; private set; }

    public InformationData Information { get; }

    public void Dispose()
    {
        // noop.
        IsDisposed = true;
    }

    // SharpDX.DirectInput.Device masquerading.
    public bool Poll()
    {
        unsafe
        {
            fixed (State* pstate = &_state)
            {
                return Native.XInputGetState(0, pstate) == 0;
            }
        }
    }

    public GamepadButtonFlags GetCurrentState()
    {
        return _state.Gamepad.Buttons;
    }

    public void Unacquire()
    {
        // noop.
    }

    public struct InformationData
    {
        // Randomly generated Guid.

        public string ProductName => "XInputController";

        public Guid InstanceGuid => ProductGuid;

        public static Guid ProductGuid { get; } = new("bb78c26f-9bfd-41c1-81f0-2f1258d25075");
    }
}