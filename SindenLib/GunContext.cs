using SindenLib.Models;
using SindenLib.Static;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using System.Threading;

namespace SindenLib
{
    public class GunContext
    {
        public bool IsConnected { get; private set; }
        public byte[] PublicKey { get; private set; }
        public byte[] SessionKey { get; private set; }
        public DeviceInfo DeviceInfo { get; private set; }
        public ButtonMap ButtonMap { get; }
        public VideoSettings VideoSettings { get; }

        private readonly GunPort ComPort;
        private readonly GunCamera Camera;

        public GunContext(string port)
        {
            ComPort = new GunPort(port);
            Camera = new GunCamera(this);
            DeviceInfo = new DeviceInfo();
            ButtonMap = new ButtonMap(this);
        }

        /// <summary>
        /// Initiates a new connection to the device
        /// </summary>
        /// <returns></returns>
        public ConnectionState Connect()
        {
            if (IsConnected)
                return ConnectionState.AlreadyConnected;

            using var sha256 = SHA256.Create();

            if (!ComPort.Open())
                return ConnectionState.DeviceNotResponding;

            // send connection request
            ComPort.Write(CreateMessage(Opcodes.Connect));
            ComPort.Flush(100);

            // send our public key
            PublicKey = sha256.ComputeHash(Guid.NewGuid().ToByteArray());
            ComPort.Write(PublicKey);
            ComPort.Poll(32);

            // generate our local sessionkey and hash it
            SessionKey = new byte[73];
            Array.Copy(PublicKey, SessionKey, PublicKey.Length);
            Array.Copy(Consts.PrivateKey, 0, SessionKey, 32, Consts.PrivateKey.Length);
            SessionKey = sha256.ComputeHash(SessionKey);

            // read the session key from the device
            var sessionKey = new byte[32];
            ComPort.Read(sessionKey);
            ComPort.Flush();

            // verify matching public keys
            if (!SessionKey.IsEqual(sessionKey))
                return ConnectionState.InvalidAuthentication;

            // send handshake request
            ComPort.Write(CreateMessage(Opcodes.Handshake));
            Thread.Sleep(5);
            ComPort.Poll(32);

            // read handshake response and suffix apply private key
            var handshake = new byte[64];
            ComPort.Read(handshake, count: 32);
            Array.Copy(Consts.HandshakeKey, 0, handshake, 32, Consts.HandshakeKey.Length);

            // send our hashed handshake + private key
            ComPort.Write(sha256.ComputeHash(handshake));
            ComPort.Poll(5);

            if (ComPort.ReadLine() != "true")
                return ConnectionState.InvalidAuthentication;

            ComPort.Write(CreateMessage(Opcodes.Authenticated));
            Thread.Sleep(100);
            ComPort.Write(CreateMessage(Opcodes.Authenticated));

            IsConnected = true;
            return ConnectionState.Success;
        }

        public bool Disconnect()
        {
            if (!IsConnected)
                return false;

            ComPort.Close();
            IsConnected = false;
            return true;
        }

        public void Start()
        {
            // TODO

            EnableSleepMode(true);
            // EnableEdgeReload(true);
            EnableEdgeClickReload(true);
            EnableCalibration(true);
            ButtonMap.Sync();
            EnableRecoil(true);

            Thread.Sleep(100);
            while (ComPort.CanRead(1))
                ComPort.Flush();
        }

        #region Capture

        public void CalculateYSightOffset(double tvSize, bool inches)
        {
            VideoSettings.YSightOffset = Math.Round((inches ? 1.2 : 3.05) / tvSize * 100.0, 2);
        }

        public void OnNewFrame(Bitmap frame)
        {
            var now = DateTime.Now;

            if(DeviceInfo.RequireCalibration)
            {
                DeviceInfo.RequireCalibration = false;
                SetCalibration(DeviceInfo.CalibrationX, DeviceInfo.CalibrationY);
            }

            Camera.ProcessFrame(frame);

            _ = now - DateTime.Now;
        }

        #endregion

        #region Cursor

        public void SetCursorOffset(short x, short y)
        {
            var buffer = CreateMessage(Opcodes.CursorOffset);
            buffer[2] = (byte)(x >> 8);
            buffer[3] = (byte)(x & 0xFF);
            buffer[4] = (byte)(y >> 8);
            buffer[5] = (byte)(y & 0xFF);
            ComPort.Write(buffer);

            // only for old versions
            if (DeviceInfo.Version <= Versions.v1_5)
            {
                buffer[1] = (byte)Opcodes.EnableSleepMode;
                ComPort.Write(buffer);
            }

            // handle immediate response
            if (!ComPort.CanRead(1))
                return;

            // temp buffer for 254 resp
            var temp = new byte[10];

            switch (ComPort.Read())
            {
                case 200: // unassign buttons
                    ButtonMap.Assign(Buttons.ButtonTrigger, Keys.None);
                    ButtonMap.Assign(Buttons.ButtonPumpAction, Keys.None);
                    break;

                case 201:
                    DeviceInfo.RequireCalibration = true;
                    goto case 202;
                case 202: // resync buttons
                    ButtonMap.Sync(Buttons.ButtonTrigger);
                    ButtonMap.Sync(Buttons.ButtonPumpAction);
                    break;

                case 254 when DeviceInfo.Version > Versions.v1_5 && ComPort.CanRead(3): // sync pushed buttons?
                    ComPort.Read(temp, count: 3);
                    DeviceInfo.LastButtonPushed = DateTime.Now;
                    break;

                case 254 when DeviceInfo.Version < Versions.v1_6 && ComPort.CanRead(11): // sync pushed buttons?
                    ComPort.Read(temp);
                    ComPort.Read();
                    if (!Array.TrueForAll(temp, t => t == 0))
                        DeviceInfo.LastButtonPushed = DateTime.Now;
                    break;
            }
        }

        #endregion Cursor

        #region Buttons

        /// <summary>
        /// Updates a button mapping on the device
        /// </summary>
        /// <param name="button">Device button</param>
        /// <param name="key">ASCII key</param>
        public void AssignButton(Buttons button, Keys key)
        {
            var buffer = CreateMessage(Opcodes.AssignButton);
            buffer[3] = (byte)button;
            buffer[5] = (byte)key;
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates multiple button mappings on the device
        /// </summary>
        /// <param name="buttons"></param>
        public void AssignButtons(IEnumerable<KeyValuePair<Buttons, Keys>> buttons)
        {
            var buffer = CreateMessage(Opcodes.AssignButton);

            foreach (var combo in buttons)
            {
                buffer[3] = (byte)combo.Key;
                buffer[5] = (byte)combo.Value;
                ComPort.Write(buffer);
            }
        }

        #endregion Buttons

        #region DeviceInfo

        /// <summary>
        /// Returns the current firmware version of the device
        /// </summary>
        /// <returns></returns>
        public Versions RequestFirmwareVersion()
        {
            ComPort.Flush();

            ComPort.Write(CreateMessage(Opcodes.RequestFirmware));
            Thread.Sleep(50);

            DeviceInfo.Version = (Versions)((ComPort.Read() << 8) + ComPort.Read());

            Thread.Sleep(100);
            while (ComPort.CanRead(1))
                ComPort.Flush(100);

            return DeviceInfo.Version;
        }

        /// <summary>
        /// Returns the device's unique id
        /// </summary>
        /// <returns></returns>
        public string RequestUniqueId()
        {
            ComPort.Flush();

            ComPort.Write(CreateMessage(Opcodes.RequestColour));
            Thread.Sleep(100);

            var uniqueId = "";
            while (ComPort.CanRead(1))
                uniqueId += ComPort.Read();

            if (uniqueId.Length > 0)
                DeviceInfo.UniqueId = uniqueId;

            return DeviceInfo.UniqueId;
        }

        /// <summary>
        /// Returns the device's colour aka variation
        /// </summary>
        /// <returns></returns>
        public string RequestColour()
        {
            ComPort.Flush();

            ComPort.Write(CreateMessage(Opcodes.RequestColour));
            Thread.Sleep(100);

            if (ComPort.CanRead(1))
                DeviceInfo.Colour = ComPort.ReadExisting();

            Thread.Sleep(50);

            return DeviceInfo.Colour;
        }

        /// <summary>
        /// Returns the device's unique id
        /// </summary>
        /// <returns></returns>
        public string RequestManufactureDate()
        {
            ComPort.Flush();

            ComPort.Write(CreateMessage(Opcodes.RequestManufactureDate));
            Thread.Sleep(100);

            var manufactureDate = "";
            while (ComPort.CanRead(1))
                manufactureDate += ComPort.Read().ToString("D2");

            if (manufactureDate.Length > 0)
                DeviceInfo.ManufactureDate = manufactureDate;

            return DeviceInfo.UniqueId;
        }

        /// <summary>
        /// Returns the camera device name stored on the device
        /// </summary>
        /// <returns></returns>
        public string RequestCamera()
        {
            ComPort.Flush();

            ComPort.Write(CreateMessage(Opcodes.RequestCamera));
            Thread.Sleep(200);

            if (ComPort.CanRead(15))
                DeviceInfo.LinkedCamera = ComPort.ReadString(15);

            return DeviceInfo.LinkedCamera;
        }

        /// <summary>
        /// Updates the camera device name stored on the device
        /// </summary>
        /// <param name="targetCamera"></param>
        public void UpdateCamera(string targetCamera)
        {
            var buffer = CreateMessage(Opcodes.UpdateCamera);
            var text = targetCamera.PadRight(15, ' ');

            for (byte i = 0; i < 15; i++)
            {
                buffer[3] = i;
                buffer[5] = Convert.ToByte(text[i]);
                ComPort.Write(buffer);
                Thread.Sleep(50);
            }
        }

        #endregion DeviceInfo

        #region Settings

        /// <summary>
        /// Enables/disables sleep mode
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableSleepMode(bool enabled)
        {
            var opcode = Opcodes.EnableSleepMode + Convert.ToByte(!enabled);
            var buffer = CreateMessage(opcode);
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Enables/disables reloading by pointing away from the screen
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableEdgeReload(bool enabled)
        {
            var opcode = Opcodes.EnableEdgeReload + Convert.ToByte(!enabled);
            var buffer = CreateMessage(opcode);
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Enables/disables reloading by firing away from the screen
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableEdgeClickReload(bool enabled)
        {
            var opcode = Opcodes.EnableEdgeClickReload + Convert.ToByte(!enabled);
            var buffer = CreateMessage(opcode);
            ComPort.Write(buffer);
        }

        #endregion Settings

        #region Calibration

        /// <summary>
        /// Enables/disables the "hold left for 3 seconds to calibrate" option
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableCalibration(bool enabled)
        {
            var buffer = CreateMessage(Opcodes.EnableCalibration);
            buffer[2] = Convert.ToByte(enabled);
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Returns both axis calibration amounts stored on the device
        /// </summary>
        /// <returns></returns>
        public (double X, double Y) RequestCalibration()
        {
            return (RequestCalibration(Axis.X), RequestCalibration(Axis.Y));
        }

        /// <summary>
        /// Returns the axis calibration amount stored on the device
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public double RequestCalibration(Axis axis)
        {
            var opcode = Opcodes.RequestCalibrationX + (byte)axis;
            var buffer = CreateMessage(opcode);

            ComPort.Flush();
            ComPort.Write(buffer);

            Thread.Sleep(150);
            var value = ComPort.CanRead(1) ? ComPort.Read() << 8 : 0;
            Thread.Sleep(50);
            value += ComPort.CanRead(1) ? ComPort.Read() : 0;

            return axis switch
            {
                Axis.X => DeviceInfo.CalibrationX = (value - 10000.0) / 100.0,
                Axis.Y => DeviceInfo.CalibrationY = (value - 10000.0) / 100.0,
                _ => 0
            };
        }

        /// <summary>
        /// Updates the axis calibration amounts stored on the device
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="value"></param>
        public void SetCalibration(double xAmount, double yAmount)
        {
            SetCalibration(Axis.X, xAmount);
            SetCalibration(Axis.Y, yAmount);
        }

        /// <summary>
        /// Updates the axis calibration amount stored on the device
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="value"></param>
        public void SetCalibration(Axis axis, double value)
        {
            var opcode = Opcodes.UpdateCalibrationX + (byte)axis;
            var buffer = CreateMessage(opcode);
            var amount = (short)Math.Floor(value * 100.0 + 10000.0);

            buffer[2] = (byte)(amount >> 8);
            buffer[3] = (byte)(amount & 0xFF);
            ComPort.Write(buffer);

            if (axis == Axis.X)
                DeviceInfo.CalibrationX = value;
            else
                DeviceInfo.CalibrationY = value;

            Thread.Sleep(100);
            ComPort.Flush();
        }

        #endregion Calibration

        #region Recoil

        /// <summary>
        /// Enables/disables recoil on the device
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableRecoil(bool enabled)
        {
            var buffer = CreateMessage(Opcodes.EnableRecoil);
            buffer[2] = Convert.ToByte(enabled);
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the pulse strength (duration), delay between pulses and
        /// the initial delay after the first pulse
        /// </summary>
        /// <param name="strength">Length of pulse</param>
        /// <param name="delay">Delay between pulses</param>
        /// <param name="startDelay">Initial delay after first pulse</param>
        public void SetRecoilPulseValues(byte strength, byte delay, byte startDelay = 0)
        {
            var buffer = CreateMessage(Opcodes.RecoilPulseValues);
            buffer[2] = strength;
            buffer[2] = startDelay;
            buffer[2] = strength;
            buffer[2] = delay;
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the recoil style
        /// </summary>
        /// <param name="style"></param>
        public void SetRecoilStyle(RecoilStyle style)
        {
            var buffer = CreateMessage(Opcodes.RecoilStyle);
            buffer[2] = Convert.ToByte(style != RecoilStyle.Normal);
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the events that recoil is activated for
        /// </summary>
        /// <param name="events">Flag</param>
        public void SetRecoilTriggerEvents(RecoilEventFlags events)
        {
            var buffer = CreateMessage(Opcodes.RecoilEvents);
            buffer[2] = Convert.ToByte(events.HasFlag(RecoilEventFlags.TriggerRecoil));
            buffer[3] = Convert.ToByte(events.HasFlag(RecoilEventFlags.TriggerOffscreenRecoil));
            buffer[4] = Convert.ToByte(events.HasFlag(RecoilEventFlags.PumpActionRecoilOnEvent));
            buffer[5] = Convert.ToByte(events.HasFlag(RecoilEventFlags.PumpActionRecoilOffEvent));
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the position of the recoil
        /// </summary>
        /// <param name="frontLeft"></param>
        /// <param name="backLeft"></param>
        /// <param name="frontRight"></param>
        /// <param name="backRight"></param>
        public void SetRecoilPositions(byte frontLeft, byte backLeft, byte frontRight, byte backRight)
        {
            var buffer = CreateMessage(Opcodes.RecoilPositions);
            buffer[2] = frontLeft;
            buffer[3] = backLeft;
            buffer[4] = frontRight;
            buffer[5] = backRight;
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the strength/voltage of the recoil
        /// </summary>
        /// <param name="voltage"></param>
        public void SetRecoilStrength(byte voltage)
        {
            var buffer = CreateMessage(Opcodes.RecoilStrength);
            buffer[2] = voltage;
            ComPort.Write(buffer);
        }

        /// <summary>
        /// Updates the strength of each pulse
        /// </summary>
        /// <param name="strength"></param>
        /// <param name="customAmount"></param>
        public void SetPulseStrength(PulseStrength strength, byte customAmount = 0)
        {
            var buffer = CreateMessage(Opcodes.PulseStrength);
            buffer[2] = buffer[3] = buffer[4] = (byte)strength;
            ComPort.Write(buffer);

            Thread.Sleep(100);

            if (strength == PulseStrength.Custom)
            {
                buffer[1] = (byte)Opcodes.CustomPulseStrength;
                buffer[2] = customAmount;
                ComPort.Write(buffer);
            }
        }

        #endregion Recoil

        #region Tests

        /// <summary>
        /// Forces the device to perform a single recoil pulse
        /// </summary>
        public void TestRecoil()
        {
            ComPort.Write(CreateMessage(Opcodes.RecoilTest));
            Thread.Sleep(50);
        }

        /// <summary>
        ///  Forces the device to start/stop performing recoil pulses
        /// </summary>
        /// <param name="enabled"></param>
        public void TestRepeatRecoil(bool enabled)
        {
            var opcode = Opcodes.RecoilTestRepeatStart + Convert.ToByte(!enabled);
            ComPort.Write(CreateMessage(opcode));
            Thread.Sleep(50);
        }

        /// <summary>
        /// Sends a custom message to the device and returns the response
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="data">Limited to 5 bytes</param>
        /// <returns></returns>
        public string Debug(byte opcode, params byte[] data)
        {
            ComPort.Flush();

            var buffer = CreateMessage((Opcodes)opcode);
            Array.Copy(data, 0, buffer, 1, Math.Min(data.Length, 5));
            ComPort.Write(buffer);
            Thread.Sleep(100);

            var result = "";
            while (ComPort.CanRead(1))
            {
                result += "-" + ComPort.Read();
                Thread.Sleep(5);
            }

            return result[1..];
        }

        #endregion Tests

        private static byte[] CreateMessage(Opcodes opcode)
        {
            var buffer = new byte[7];
            buffer[0] = 170;
            buffer[1] = (byte)opcode;
            buffer[^1] = 187;
            return buffer;
        }
    }
}