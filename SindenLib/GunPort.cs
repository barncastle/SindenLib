using System.IO.Ports;
using System.Threading;

namespace SindenLib
{
    public sealed class GunPort
    {
        public bool IsConnected { get; private set; }
        public bool IsAvailable => ComPort.BytesToRead > 0;

        private readonly SerialPort ComPort;

        private readonly byte[] ByteBuff = new byte[1];

        public GunPort(string port, int baudRate = 115200)
        {
            ComPort = new SerialPort(port, baudRate)
            {
                RtsEnable = true,
                DtrEnable = true
            };
        }

        public bool Open()
        {
            if (!ComPort.IsOpen)
            {
                try
                {
                    ComPort.Open();
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        public void Close()
        {
            if (ComPort.IsOpen)
                ComPort.Close();
        }

        #region Reading

        public int Read() => ComPort.ReadByte();

        public string ReadLine() => ComPort.ReadLine();

        public string ReadExisting() => ComPort.ReadExisting();

        public string ReadString(int length)
        {
            if (length >= 0)
            {
                var buffer = new char[length];
                ComPort.Read(buffer, 0, buffer.Length);
                return new string(buffer);
            }
            else
            {
                return ComPort.ReadExisting();
            }
        }

        public void Read(byte[] buffer, int start = 0, int count = -1)
        {
            ComPort.Read(buffer, start, count < 0 ? buffer.Length : count);
        }

        public bool CanRead(int count) => count > 0 && ComPort.BytesToRead >= count;

        #endregion Reading

        #region Writing

        public void Write(byte value)
        {
            ByteBuff[0] = value;
            ComPort.Write(ByteBuff, 0, 1);
        }

        public void Write(byte[] buffer, int start = 0, int count = -1)
        {
            ComPort.Write(buffer, start, count < 0 ? buffer.Length : count);
        }

        #endregion Writing

        /// <summary>
        /// Clears the read buffer with optional wait
        /// </summary>
        /// <param name="sleep"></param>
        public void Flush(int sleep = 0)
        {
            if (ComPort.BytesToRead > 0)
            {
                Thread.Sleep(sleep);
                ComPort.ReadExisting();
            }
        }

        /// <summary>
        /// Waits until a certain amount of bytes are available to read
        /// </summary>
        /// <param name="count"></param>
        public void Poll(int count)
        {
            while (ComPort.BytesToRead < count)
                Thread.Sleep(10);
        }
    }
}