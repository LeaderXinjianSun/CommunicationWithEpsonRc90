using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace BingLibrary.hjb
{
    public class TaiDaPLC
    {
        //private ManualResetEvent Pause_Event = new ManualResetEvent(false);
        public SerialPort curSerialPort;
        private object SerialLock = new object();

        public bool Close { set; get; }

        public TaiDaPLC(string PortName, int BaudRate, Parity Parity, int DataBits, StopBits StopBits)
        {
            curSerialPort = new SerialPort();
            curSerialPort.PortName = PortName;
            curSerialPort.BaudRate = BaudRate;
            curSerialPort.Parity = Parity;
            curSerialPort.DataBits = DataBits;
            curSerialPort.StopBits = StopBits;
            curSerialPort.ReadTimeout = 1000;
            curSerialPort.WriteTimeout = 1000;
        }

        public bool ConnectPLC()
        {
            try
            {
                if (curSerialPort.PortName != "" && curSerialPort.IsOpen == false)
                {
                    curSerialPort.Open();
                    return true;
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }

        public bool ReConnectPLC()
        {
            while (curSerialPort?.PortName != "" && curSerialPort?.IsOpen == false && Close == false)
            {
                try
                {
                    curSerialPort.Open();
                }
                catch
                {
                    Thread.Sleep(mTime);
                    mTime = mTime < 1200 ? mTime + 50 : 1200;
                    if (mTime == 1000)
                        return false;
                }
            }
            return true;
        }

        private int mTime = 100;

        public void DisConnectPLC()
        {
            try
            {
                Close = true;
                curSerialPort?.Close();
            }
            catch { }
        }


        public bool SetM(string mDevAdd, bool mDevData)
        {
            return mDevData ? PLCWrite("01", mDevAdd, "FF00") : PLCWrite("01", mDevAdd, "0000");
        }

        public bool ReadM(string mDevAdd)
        {
            string temps = PLCRead("01", mDevAdd);
            int tempi = Convert.ToInt32(temps, 16);
            tempi = tempi & 1;
            return tempi == 1 ? true : false;
        }

        public double ReadD(string addr)
        {
            string strD = PLCRead("01",addr);
            double dD = Convert.ToInt32(strD,16);
            return dD;
        }

        public bool WriteD(string addr, string data)
        {try
            {
                PLCWrite("01", addr, Convert.ToInt32(data).ToString("X4"));
                return true;
            }
            catch { return false; }
        }

        //高低反
        public string PLCInversion(int mData)
        {
            Int32 data = Convert.ToInt32(((mData & 0xFFFF) << 16) + ((mData & 0xFFFF0000) >> 16));
            return data.ToString("X8");
        }

        public bool PLCWriteBit(string mDevIndex, string mDevAdd, string mByteIndex, string mDevData)
        {
            if (!curSerialPort.IsOpen) ReConnectPLC();
            if (mDevAdd == "" || mDevData == "") return false;
            string mStr;
            string mModBus, mDevType, mAddress;
            int m;

            mDevType = mDevAdd.Substring(0, 1);
            mAddress = mDevAdd.Replace(mDevType, "");

            switch (mDevType)
            {
                case "M":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress);
                    m += m > 1535 ? 45056 : 2048;
                    mAddress = m.ToString("X4");
                    break;

                case "X":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1024;
                    mAddress = m.ToString("X4");
                    break;

                case "Y":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1280;
                    mAddress = m.ToString("X4");
                    break;

                case "S":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress);
                    mAddress = m.ToString("X4");
                    break;

                default:
                    return false;
            }

            mStr = mDevIndex + mModBus + mAddress + mByteIndex + (mDevData.Length / 2).ToString("X2") + mDevData;
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.Write(mStr);
                try
                {
                    mRecStr = curSerialPort.ReadLine();
                }
                catch
                {
                    return false;
                }
            }
            return mStr.Contains(mRecStr.Remove(13)) ? true : false;
            
        }

        //写入一串寄存器，需要自己高低位置反
        public bool PLCWriteStr(string mDevIndex, string mDevAdd, string mDevData)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            if (mDevAdd == "")
                return false;
            if (mDevData == "")
                return false;

            string mStr;
            string mModBus, mDevType, mAddress, mLength;
            int m;
            mDevType = mDevAdd.Substring(0, 1);
            mAddress = mDevAdd.Replace(mDevType, "");
            mModBus = "10";
            m = Convert.ToInt32(mAddress);
            if (m > 4095)
                m += 32768;
            else
                m += 4096;
            mAddress = m.ToString("X4");
            m = mDevData.Length;
            mLength = (m / 4).ToString("X4") + (m / 2).ToString("X2");
            mStr = mDevIndex + mModBus + mAddress + mLength + mDevData;
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.Write(mStr);

                try
                {
                    mRecStr = curSerialPort.ReadLine();
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message);
                    return false;
                }
            }

            if (mStr.Contains(mRecStr.Remove(13)))
                return true;
            else
            {
                return false;
            }
        }



        // 写入PLC装置值
        public bool PLCWrite(string mDevIndex, string mDevAdd, string mDevData)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            if (mDevAdd == "")
                return false;
            if (mDevData == "")
                return false;

            string mStr;
            string mModBus, mDevType, mAddress, mData;
            int m;

            mDevType = mDevAdd.Substring(0, 1);
            mAddress = mDevAdd.Replace(mDevType, "");

            switch (mDevType)
            {
                case "M":
                    mModBus = "05";
                    m = Convert.ToInt32(mAddress);
                    if (m > 1535)
                        m = m + 45056;
                    else
                        m = m + 2048;
                    mAddress = m.ToString("X4");
                    break;

                case "X":
                    mModBus = "05";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1024;
                    mAddress = m.ToString("X4");
                    break;

                case "Y":
                    mModBus = "05";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1280;
                    mAddress = m.ToString("X4");
                    break;

                case "S":
                    mModBus = "05";
                    m = Convert.ToInt32(mAddress);
                    mAddress = m.ToString("X4");
                    break;

                case "D"://32位
                    mModBus = "10";
                    m = Convert.ToInt32(mAddress);
                    if (m > 4095)
                        m = m + 32768;
                    else
                        m = m + 4096;
                    mAddress = m.ToString("X4");
                    break;

                case "W"://16位
                    mModBus = "06";
                    m = Convert.ToInt32(mAddress);
                    if (m > 4095)
                        m = m + 32768;
                    else
                        m = m + 4096;
                    mAddress = m.ToString("X4");
                    break;

                default:
                    return false;
            }
            m = Convert.ToInt32(mDevData, 16);
            if (mDevType == "D")
            {
                mData = m.ToString("X8");
                string SubStr;
                SubStr = mData.Substring(0, 4);
                mData = mData.Insert(8, SubStr);
                mData = mData.Remove(0, 4);
                mStr = mDevIndex + mModBus + mAddress + "000204" + mData;
            }
            else
            {
                mData = m.ToString("X4");
                mStr = mDevIndex + mModBus + mAddress + mData;
            }

            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.ReadExisting();
                curSerialPort.Write(mStr);
                mRecStr = curSerialPort.ReadLine();
            }

            if (mRecStr == "")
            {
                Debug.Print("写入失败！");
                return false;
            }
            if (mStr.Contains(mRecStr.Remove(13)))
                return true;
            else
            {
                Debug.Print("写入失败！");
                return false;
            }
        }

        public string PLCRead(string mDevIndex, string mDevAdd, string mByteToRead = "0001", bool mHex = false)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            if (mDevAdd == null)
            {
                return "";
            }
            if (mDevAdd.Length < 2)
            {
                return "";
            }
            string mModBus, mDevType, mAddress, mdata;
            string mStr;
            int m;
            mDevType = mDevAdd.Substring(0, 1);
            mAddress = mDevAdd.Replace(mDevType, "");
            switch (mDevType)
            {
                case "M":
                    mModBus = "01";
                    m = Convert.ToInt32(mAddress);
                    if (m > 1535)
                        m += 45056;
                    else
                        m += 2048;
                    mAddress = m.ToString("X4");
                    break;

                case "X":
                    mModBus = "02";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1024;
                    mAddress = m.ToString("X4");
                    break;

                case "Y":
                    mModBus = "01";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 1280;
                    mAddress = m.ToString("X4");
                    break;

                case "S":
                    mModBus = "01";
                    m = Convert.ToInt32(mAddress);
                    mAddress = m.ToString("X4");
                    break;

                case "D"://32位
                    mModBus = "03";
                    m = Convert.ToInt32(mAddress);
                    if (m > 4095)
                        m += 32768;
                    else
                        m += 4096;
                    mAddress = m.ToString("X4");
                    mByteToRead = (Convert.ToInt32(mByteToRead, 16) * 2).ToString("X4");
                    break;

                case "W"://16位
                    mModBus = "03";
                    m = Convert.ToInt32(mAddress);
                    if (m > 4095)
                        m += 32768;
                    else
                        m += 4096;
                    mAddress = m.ToString("X4");
                    break;

                default:
                    return "";
            }

            mStr = mDevIndex + mModBus + mAddress + mByteToRead;
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.ReadExisting();
                curSerialPort.Write(mStr);
                mRecStr = curSerialPort.ReadLine();
            }

            mRecStr = mRecStr.Substring(1);
            if (mRecStr.Contains(mDevIndex + mModBus))
            {
                m = 2 * Convert.ToInt32(mRecStr.Substring(4, 2), 16);
                mdata = mRecStr.Substring(6, m);
                if (mDevType == "D")
                {
                    for (int n = 1; n < m / 8 + 1; n++)
                    {
                        string SubStr;
                        SubStr = mdata.Substring((n - 1) * 8, 4);
                        mdata = mdata.Insert(8 * n, SubStr);
                        mdata = mdata.Remove((n - 1) * 8, 4);
                    }
                }
                if (mHex)
                {
                    mdata = "0x" + mdata;
                }

                return mdata;
            }
            Debug.Print("ReadBad");
            return "";
        }

        public string ReadTemperature(string mDevIndex)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            string mStr = mDevIndex + "03" + "1000" + "0001";
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.ReadExisting();
                curSerialPort.Write(mStr);
                mRecStr = curSerialPort.ReadLine();
            }

            mRecStr = mRecStr.Substring(1);
            int m;
            string mdata;
            if (mRecStr.Contains(mDevIndex + "03"))
            {
                m = 2 * Convert.ToInt32(mRecStr.Substring(4, 2), 16);
                mdata = mRecStr.Substring(6, m);

                mdata = (Convert.ToInt32(mdata, 16) / 10.0).ToString("F1");
                return mdata;
            }
            Debug.Print("ReadBad");
            return "";
        }

        public string ReadSV(string mDevIndex)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            string mStr = mDevIndex + "03" + "1001" + "0001";
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.ReadExisting();
                curSerialPort.Write(mStr);
                mRecStr = curSerialPort.ReadLine();
            }

            mRecStr = mRecStr.Substring(1);
            int m;
            string mdata;
            if (mRecStr.Contains(mDevIndex + "03"))
            {
                m = 2 * Convert.ToInt32(mRecStr.Substring(4, 2), 16);
                mdata = mRecStr.Substring(6, m);

                mdata = (Convert.ToInt32(mdata, 16) / 10.0).ToString("F1");
                return mdata;
            }
            Debug.Print("ReadBad");
            return "";
        }

        public bool WriteSV(string mDevIndex, string mDevData)
        {
            if (!curSerialPort.IsOpen)
            {
                ReConnectPLC();
            }
            if (mDevData == "")
                return false;

            string mStr;
            string mData;
            int m;
            m = (int)(Convert.ToDouble(mDevData) * 10);
            if (m > 600)
                m = 600;
            mData = m.ToString("X4");
            mStr = mDevIndex + "06" + "1001" + mData;
            mStr = ":" + LRC(mStr) + Environment.NewLine;

            string mRecStr = "";
            lock (SerialLock)
            {
                curSerialPort.ReadExisting();
                curSerialPort.Write(mStr);
                try
                {
                    mRecStr = curSerialPort.ReadLine();
                }
                catch
                {
                    return false;
                }
            }

            return mStr.Contains(mRecStr.Remove(13)) ? true : false;
              
        }

        public float ToFloat(string str)
        {
            try
            {
                byte[] buf = new byte[4];
                buf[3] = Convert.ToByte(str.Substring(0, 2), 16);
                buf[2] = Convert.ToByte(str.Substring(2, 2), 16);
                buf[1] = Convert.ToByte(str.Substring(4, 2), 16);
                buf[0] = Convert.ToByte(str.Substring(6, 2), 16);
                float f = BitConverter.ToSingle(buf, 0);
                return f;
            }
            catch { }
            return 0;
        }

        private string LRC(string str)
        {
            int mlength = str.Length;
            string c_data, h_lrc;
            long d_lrc = 0;
            for (int cTick = 0; cTick < mlength; cTick++)
            {
                c_data = str.Substring(cTick, 2);
                d_lrc += Convert.ToInt32(c_data, 16);
                cTick++;
            }
            d_lrc = d_lrc & 0xFF;
            if (d_lrc == 0)
                return str + "00";
            d_lrc = 0xFF - d_lrc + 1;
            h_lrc = d_lrc.ToString("X2");
            return str + h_lrc;
        }
    }
}