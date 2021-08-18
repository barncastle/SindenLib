using SindenLib.Static;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SindenLib.Models
{
    /// <summary>
    /// A button to keyboard key map for the device
    /// <para>Note: Buttons can only be assigned/unassigned not added/removed</para>
    /// </summary>
    public sealed class ButtonMap : IReadOnlyDictionary<Buttons, Keys>
    {
        public ICollection<Buttons> Keys => Map.Keys;
        public ICollection<Keys> Values => Map.Values;
        public int Count => Map.Count;
        IEnumerable<Buttons> IReadOnlyDictionary<Buttons, Keys>.Keys => Map.Keys;
        IEnumerable<Keys> IReadOnlyDictionary<Buttons, Keys>.Values => Map.Values;

        private readonly Dictionary<Buttons, Keys> Map;
        private readonly GunContext Context;

        public ButtonMap(GunContext context)
        {
            Context = context;
            Map = Instantiate();
        }

        public Keys this[Buttons key]
        {
            get => Map[key];
            set => Assign(key, value);
        }

        /// <summary>
        /// Updates a button's key mapping and syncs the device
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Assign(Buttons key, Keys value)
        {
            if (Enum.IsDefined(typeof(Buttons), key) && Enum.IsDefined(typeof(Keys), value))
            {
                Map[key] = value;
                Context.AssignButton(key, value);
            }
        }

        /// <summary>
        /// Syncs all buttons to the device
        /// </summary>
        public void Sync() => Context.AssignButtons(this);

        /// <summary>
        /// Syncs a specific button to the device
        /// </summary>
        /// <param name="key"></param>
        public void Sync(Buttons key) => Context.AssignButton(key, Map[key]);

        public bool ContainsKey(Buttons key) => Map.ContainsKey(key);

        public bool TryGetValue(Buttons key, out Keys value) => Map.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<Buttons, Keys>> GetEnumerator() => Map.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Populates the map and assigns the default button configuration
        /// </summary>
        /// <returns></returns>
        private static Dictionary<Buttons, Keys> Instantiate()
        {
            var map = new Dictionary<Buttons, Keys>(0x40);

            foreach (Buttons button in Enum.GetValues(typeof(Buttons)))
            {
                map[button] = button switch
                {
                    // normal
                    Buttons.ButtonTrigger => Static.Keys.MouseLeft,
                    Buttons.ButtonPumpAction => Static.Keys.MouseRight,
                    Buttons.ButtonFrontLeft => Static.Keys.MouseRight,
                    Buttons.ButtonRearLeft => Static.Keys.MouseMiddle,
                    Buttons.ButtonFrontRight => Static.Keys.Num1,
                    Buttons.ButtonRearRight => Static.Keys.BorderOnOffAltB,
                    Buttons.ButtonLeft => Static.Keys.Left,
                    Buttons.ButtonRight => Static.Keys.Right,
                    Buttons.ButtonUp => Static.Keys.Up,
                    Buttons.ButtonDown => Static.Keys.Down,

                    // offscreen
                    Buttons.ButtonTriggerOffscreen => Static.Keys.MouseRight,
                    Buttons.ButtonPumpActionOffscreen => Static.Keys.MouseRight,
                    Buttons.ButtonFrontLeftOffscreen => Static.Keys.MouseRight,
                    Buttons.ButtonRearLeftOffscreen => Static.Keys.MouseMiddle,
                    Buttons.ButtonFrontRightOffscreen => Static.Keys.Num5,
                    Buttons.ButtonRearRightOffscreen => Static.Keys.BorderOnOffAltB,
                    Buttons.ButtonLeftOffscreen => Static.Keys.Left,
                    Buttons.ButtonRightOffscreen => Static.Keys.Right,
                    Buttons.ButtonUpOffscreen => Static.Keys.Up,
                    Buttons.ButtonDownOffscreen => Static.Keys.Down,

                    _ => Static.Keys.None
                };
            }

            return map;
        }
    }
}