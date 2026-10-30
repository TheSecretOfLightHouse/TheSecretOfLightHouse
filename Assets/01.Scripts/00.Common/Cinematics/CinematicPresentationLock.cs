using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lighthouse.Common.Cinematics
{
    public static class CinematicPresentationLock
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();
        private static readonly List<InputDevice> DisabledDevices = new List<InputDevice>();

        /// <summary>Local presentation input lock; physics and network updates continue.</summary>
        public static bool IsLocked => Owners.Count > 0;

        /// <summary>Blocks action maps and direct keyboard/mouse polling without closing UI.</summary>
        public static void Acquire(object owner)
        {
            if (owner == null || !Owners.Add(owner) || Owners.Count != 1)
            {
                return;
            }
            InputSystem.onDeviceChange += HandleDeviceChanged;
            foreach (InputDevice device in InputSystem.devices)
            {
                Disable(device);
            }
        }

        /// <summary>Restores only devices disabled here, after the last presentation leaves.</summary>
        public static void Release(object owner)
        {
            if (owner == null || !Owners.Remove(owner) || Owners.Count > 0)
            {
                return;
            }
            InputSystem.onDeviceChange -= HandleDeviceChanged;
            for (int i = 0; i < DisabledDevices.Count; i++)
            {
                InputDevice device = DisabledDevices[i];
                if (device.added && !device.enabled)
                {
                    InputSystem.EnableDevice(device);
                }
            }
            DisabledDevices.Clear();
        }

        private static void HandleDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
            {
                Disable(device);
            }
        }

        private static void Disable(InputDevice device)
        {
            if (device.enabled && (device is Keyboard || device is Pointer || device is Gamepad || device is Joystick))
            {
                if (!DisabledDevices.Contains(device))
                {
                    DisabledDevices.Add(device);
                }
                InputSystem.DisableDevice(device);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Also handles entering Play with domain reload disabled.
            InputSystem.onDeviceChange -= HandleDeviceChanged;
            foreach (InputDevice device in DisabledDevices)
            {
                if (device.added && !device.enabled)
                {
                    InputSystem.EnableDevice(device);
                }
            }
            DisabledDevices.Clear();
            Owners.Clear();
        }
    }
}
