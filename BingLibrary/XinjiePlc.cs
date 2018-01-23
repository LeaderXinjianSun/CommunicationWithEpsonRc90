using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace BingLibrary.hjb
{
    public class XinjiePlc
    {
        private string mRecStr;
        private ManualResetEvent Pause_Event = new ManualResetEvent(false);
        public SerialPort curSerialPort;
        private object SerialLock = new object();

        public bool Close { set; get; }

        public string[] PortNames { set; get; }

        public string[] GetPortsName()
        {
            return SerialPort.GetPortNames();
        }

        public XinjiePlc(String PortName, int BaudRate, Parity Parity, int DataBits, StopBits StopBits)
        {
            curSerialPort = new SerialPort();
            curSerialPort.PortName = PortName;
            curSerialPort.BaudRate = BaudRate;
            curSerialPort.Parity = Parity;
            curSerialPort.DataBits = DataBits;
            curSerialPort.StopBits = StopBits;
        }

        public bool ConnectPLC()
        {
            try
            {
                if (curSerialPort?.PortName != "" && curSerialPort?.IsOpen == false)
                {
                    curSerialPort.Open();
                    return true;
                }
                else
                    return false;
            }
            catch { return false; }
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

        private bool PLCWriteBit(string mDevIndex, string mDevAdd, string mByteIndex, string mDevData)
        {
            if (!curSerialPort.IsOpen)
            {
                bool rst = ReConnectPLC();
                if (!rst) return rst;
            }
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
                    mAddress = m.ToString("X4");
                    break;

                case "X":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 16384;
                    mAddress = m.ToString("X4");
                    break;

                case "Y":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress, 8);
                    m += 18432;
                    mAddress = m.ToString("X4");
                    break;

                case "S":
                    mModBus = "0F";
                    m = Convert.ToInt32(mAddress);
                    m += 20480;
                    mAddress = m.ToString("X4");
                    break;

                default:
                    return false;
            }

            mStr = mDevIndex + mModBus + mAddress + mByteIndex + (mDevData.Length / 2).ToString("X2") + mDevData;
            byte[] mByteToWrite = StrToByte(mStr);

            lock (SerialLock)
            {
                mRecStr = "";
                curSerialPort.Write(mByteToWrite, 0, mByteToWrite.Length);

                //Thread.Sleep(20);
                int len = 8;
                int mCount = curSerialPort.BytesToRead;
                int mtiemout = 0;
                while (mCount < len)
                {
                    mCount = curSerialPort.BytesToRead;
                    Thread.Sleep(10);
                    mtiemout++;
                    if (mtiemout > 100)
                    {
                        return false;
                    }
                }

                byte[] mRecByte = new byte[len];
                curSerialPort.Read(mRecByte, 0, len);
                mRecStr = "";
                for (int ctick = 0; ctick < len; ctick++)
                {
                    mRecStr = mRecStr + mRecByte[ctick].ToString("X2");
                }

                if (mStr.Contains(mRecStr.Remove(12)))
                    return true;
                else
                {
                    Debug.Print("写入失败！");
                    return false;
                }
            }
        }

        //写入一串寄存器，需要自己高低位置反
        private bool PLCWriteStr(string mDevIndex, string mDevAdd, string mDevData)
        {
            if (!curSerialPort.IsOpen)
            {
                bool rst = ReConnectPLC();
                if (!rst)
                    return rst;
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
            mAddress = m.ToString("X4");
            m = mDevData.Length;
            mLength = (m / 4).ToString("X4") + (m / 2).ToString("X2");
            mStr = mDevIndex + mModBus + mAddress + mLength + mDevData;

            byte[] mByteToWrite = StrToByte(mStr);

            lock (SerialLock)
            {
                mRecStr = "";
                curSerialPort.Write(mByteToWrite, 0, mByteToWrite.Length);

                //Thread.Sleep(20);
                int len = 8;
                int mCount = curSerialPort.BytesToRead;
                int mtiemout = 0;
                while (mCount < len)
                {
                    mCount = curSerialPort.BytesToRead;
                    Thread.Sleep(10);
                    mtiemout++;
                    if (mtiemout > 100)
                    {
                        curSerialPort.ReadExisting();
                        return false;
                    }
                }

                byte[] mRecByte = new byte[len];
                curSerialPort.Read(mRecByte, 0, len);
                mRecStr = "";
                for (int ctick = 0; ctick < len; ctick++)
                {
                    mRecStr = mRecStr + mRecByte[ctick].ToString("X2");
                }

                if (mStr.Contains(mRecStr.Remove(12)))
                    return true;
                else
                {
                    Debug.Print("写入失败！");
                    return false;
                }
            }
        }

        // 写入PLC装置值
        private bool PLCWrite(string mDevAdd, int mPlcAddress, string mDevData, string mDevIndex = "01")
        {
            string mStr;
            string mModBus, mDevType, mAddress, mData;
            int m;

            mDevType = mDevAdd;

            switch (mDevType)
            {
                case "M":
                    mModBus = "05";
                    mAddress = mPlcAddress.ToString("X4");
                    break;

                case "X":
                    mModBus = "05";
                    mAddress = (16384 + mPlcAddress).ToString("X4");
                    break;

                case "Y":
                    mModBus = "05";
                    mAddress = (18432 + mPlcAddress).ToString("X4");
                    break;

                case "S":
                    mModBus = "05";
                    mAddress = (20480 + mPlcAddress).ToString("X4");
                    break;

                case "D"://32位
                    mModBus = "10";
                    mAddress = mPlcAddress.ToString("X4");
                    break;

                case "W"://16位
                    mModBus = "06";
                    mAddress = mPlcAddress.ToString("X4");
                    break;

                default:
                    return false;
            }

            if (mDevType == "D")
            {
                m = Convert.ToInt32(mDevData);
                mData = m.ToString("X8");
                string SubStr;
                SubStr = mData.Substring(0, 4);
                mData = mData.Insert(8, SubStr);
                mData = mData.Remove(0, 4);
                mStr = mDevIndex + mModBus + mAddress + "000204" + mData;
            }
            else
            {
                m = Convert.ToInt32(mDevData, 16);
                mData = m.ToString("X4");
                mStr = mDevIndex + mModBus + mAddress + mData;
            }
            byte[] mByteToWrite = StrToByte(mStr);

            lock (SerialLock)
            {
                mRecStr = "";
                curSerialPort.ReadExisting();
                curSerialPort.Write(mByteToWrite, 0, mByteToWrite.Length);

                //Thread.Sleep(20);
                int len = 8;
                int mCount = curSerialPort.BytesToRead;
                int mtiemout = 0;
                while (mCount < len)
                {
                    mCount = curSerialPort.BytesToRead;
                    Thread.Sleep(10);
                    mtiemout++;
                    if (mtiemout > 100)
                    {
                        curSerialPort.ReadExisting();
                        return false;
                    }
                }

                byte[] mRecByte = new byte[len];
                curSerialPort.Read(mRecByte, 0, len);
                mRecStr = "";
                for (int ctick = 0; ctick < len; ctick++)
                {
                    mRecStr = mRecStr + mRecByte[ctick].ToString("X2");
                }

                if (mStr.Contains(mRecStr.Remove(12)))
                    return true;
                else
                {
                    Debug.Print("写入失败！");
                    return false;
                }
            }
        }

        private string PLCRead(string mDevAdd, int mPlcAddress, string mDevIndex = "01", string mByteToRead = "0001", bool mHex = false)
        {
            string mModBus, mDevType, mAddress, mdata;
            string mStr;
            int m;
            mDevType = mDevAdd;
            switch (mDevType)
            {
                case "M":
                    mModBus = "01";
                    mAddress = mPlcAddress.ToString("X4");
                    break;

                case "X":
                    mModBus = "01";
                    mAddress = (16384 + mPlcAddress).ToString("X4");
                    break;

                case "Y":
                    mModBus = "01";
                    mAddress = (18432 + mPlcAddress).ToString("X4");
                    break;

                case "S":
                    mModBus = "01";
                    mAddress = (20480 + mPlcAddress).ToString("X4");
                    break;

                case "D"://32位
                    mModBus = "03";
                    mAddress = mPlcAddress.ToString("X4");
                    mByteToRead = (Convert.ToInt32(mByteToRead, 16) * 2).ToString("X4");
                    break;

                case "W"://16位
                    mModBus = "03";
                    mAddress = mPlcAddress.ToString("X4");
                    break;

                default:
                    return "";
            }

            mStr = mDevIndex + mModBus + mAddress + mByteToRead;

            byte[] mByteToWrite = StrToByte(mStr);

            int len = Convert.ToInt32(mByteToRead, 16);
            if (mModBus == "01")
            {
                if (len % 8 != 0)
                {
                    len = len / 8 + 1;
                }
                else
                {
                    len = len / 8;
                }
                len += 5;
            }
            else
            {
                len = len * 2 + 5;
            }
            lock (SerialLock)
            {
                mRecStr = "";
                curSerialPort.ReadExisting();
                curSerialPort.Write(mByteToWrite, 0, mByteToWrite.Length);

                //Thread.Sleep(20);
                int mCount = curSerialPort.BytesToRead;
                int mtiemout = 0;
                while (mCount < len)
                {
                    mCount = curSerialPort.BytesToRead;
                    Thread.Sleep(1);
                    mtiemout++;
                    if (mtiemout > 100)
                    {
                        curSerialPort.ReadExisting();
                        return "";
                    }
                }

                byte[] mRecByte = new byte[len];
                curSerialPort.Read(mRecByte, 0, len);
                mRecStr = "";
                for (int ctick = 0; ctick < len; ctick++)
                {
                    mRecStr = mRecStr + mRecByte[ctick].ToString("X2");
                }

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
        }

        //一次读160个m
        public ObservableCollection<bool> readMultiM(string t, int addr)
        {
            string temp0;
            temp0 = PLCRead(t, addr, "01", "00a0");
            string temp1 = "";
            try
            {
                for (int i = 0; i < 40; i = i + 2)
                {
                    temp1 += temp0[i + 1].ToString() + temp0[i].ToString();
                }
            }
            catch
            {
            }

            string[] temp2 = new string[40];

            ObservableCollection<bool> result = new ObservableCollection<bool>();

            string temp160 = "";
            try
            {
                for (int i = 0; i < 40; i++)
                {
                    temp2[i] = temp1[i].ToString();
                    int t0 = Convert.ToInt32(temp2[i], 16);
                    string t1 = Convert.ToString(t0, 2);
                    int t2 = Convert.ToInt32(t1);
                    string t3 = string.Format("{0:D4}", t2);
                    temp160 += BinaryReverse(t3);
                }
            }
            catch { }
            try
            {
                for (int i = 0; i < 160; i++)
                {
                    if (temp160[i] == '1')
                    {
                        result.Add(true);
                    }
                    else
                    {
                        result.Add(false);
                    }
                }
            }
            catch { }
            return result;
        }

        public ObservableCollection<double> readMultiD(int addr)
        {
            string temp0;
            temp0 = PLCRead("D", addr, "01", "001e");
            ObservableCollection<double> result = new ObservableCollection<double>();
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    result.Add(Convert.ToInt32(temp0.Substring(8 * i, 8), 16));
                }
            }
            catch { }

            return result;
        } 

        public double ReadD(int x)
        {
            try
            {
                string r = PLCRead("D", x);
                string t1 = r.Substring(0, 4);
                string t2 = r.Remove(0, 4);
                string t3 = t2 + t1;
                double d = Convert.ToInt32(t3, 16);
                return d != 0 ? d : 0;
            }
            catch { return 0; }
        }

        public bool WriteD(int x, string s)
        {
            try
            {
                return  PLCWrite("D", x, s);
            }
            catch { return false; }
        }

        public double ReadW(int x)
        {
            try
            {
                string r = PLCRead("W", x);
                double d = Convert.ToInt32(r, 16);
                return d != 0 ? d : 0;
            }
            catch { return 0; }
        }

        public bool WriteW(int x, string s)
        {
            try
            {
                string s1 = Convert.ToString(Convert.ToInt32(s, 10), 16);   
                return PLCWrite("W", x, s1);
            }
            catch { return false; }
        }

        public bool ReadX(int x)
        {
            string r;
            try
            {
                r = PLCRead("X", x);
                r = r == "" ? "0" : r;
            }
            catch { return false; }
            return Convert.ToInt32(r, 16) != 0 ? true : false;
        }

        public bool SetX(int x, bool act)
        {
            try
            {
                return act ? PLCWrite("X", x, "FF00") : PLCWrite("X", x, "0000");
            }
            catch { return false; }
        }

        public bool ReadY(int y)
        {
            string r;
            try
            {
                r = PLCRead("Y", y);
                r = r == "" ? "0" : r;
            }
            catch { return false; }
            return Convert.ToInt32(r, 16) != 0 ? true : false;
        }

        public bool SetY(int y, bool act)
        {
            try
            {
                return act ? PLCWrite("Y", y, "FF00") : PLCWrite("Y", y, "0000");
            }
            catch { return false; }
        }

        public bool ReadM(int m)
        {
            string r;
            try
            {
                r = PLCRead("M", m);
                r = r == "" ? "0" : r;
            }
            catch { return false; }
            return Convert.ToInt32(r, 16) != 0 ? true : false;
        }

        public bool SetM(int m, bool act)
        {
            try
            {
                return act ? PLCWrite("M", m, "FF00") : PLCWrite("M", m, "0000");
            }
            catch { return false; }
        }

        private void SPRead()
        {
            while (curSerialPort.IsOpen)
            {
                try
                {
                    int mCount = curSerialPort.BytesToRead;
                    if (mCount < 7)
                        continue;
                    Thread.Sleep(20);//不停顿下可能会读出0、、
                    byte[] mRecByte = null;
                    int a = curSerialPort.ReadByte();
                    int b = curSerialPort.ReadByte();
                    if (b <= 3)
                    {
                        int c = curSerialPort.ReadByte();
                        mCount = 3 + 2 + c;
                        mRecByte = new byte[mCount];
                        mRecByte[0] = (byte)a;
                        mRecByte[1] = (byte)b;
                        mRecByte[2] = (byte)c;
                        curSerialPort.Read(mRecByte, 3, mCount - 3);
                    }
                    else
                    {
                        mRecByte = new byte[mCount];
                        mRecByte[0] = (byte)a;
                        mRecByte[1] = (byte)b;
                        curSerialPort.Read(mRecByte, 2, mCount - 2);
                    }
                    //SerialPort1.ReadByte();
                    mRecStr = "";
                    for (int ctick = 0; ctick < mCount; ctick++)
                    {
                        mRecStr = mRecStr + mRecByte[ctick].ToString("X2");
                    }
                    //mRecStr = SerialPort1.ReadLine();
                    if (mRecStr != "")
                    {
                        Debug.Print(mCount + ":" + mRecStr);
                        Pause_Event.Set();
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("SPRead:" + ex.Message);
                }
            }
        }

        //高低反
        private string PLCInversion(int mData)
        {
            return (((mData & 0xFFFF) << 16) + ((mData & 0xFFFF0000) >> 16)).ToString("X8");
        }

        private string BinaryReverse(string text)
        {
            char[] charArray = text.ToCharArray();
            int len = text.Length - 1;

            for (int i = 0; i < len; i++, len--)
            {
                char tmp = charArray[i];
                charArray[i] = charArray[len];
                charArray[len] = tmp;
            }

            return new string(charArray);
        }

        //计算PLC的校验位、、高低位没处理
        private int CRC_Verify(byte[] cBuffer, int iBufLen)
        {
            int i, j;                 //#define wPolynom 0xA001
            int wCrc = 0xffff;
            int wPolynom = 0xA001;  /*---------------------------------------------------------------------------------*/
            for (i = 0; i < iBufLen; i++)
            {
                wCrc ^= cBuffer[i];
                for (j = 0; j < 8; j++)
                {
                    if ((wCrc & 0x0001) != 0)
                    { wCrc = (wCrc >> 1) ^ wPolynom; }
                    else { wCrc = wCrc >> 1; }
                }
            }
            return wCrc;
        }

        private byte[] StrToByte(string mStr)
        {
            int wCrc = 0xFFFF;
            int wPolynom = 0xA001;
            mStr = mStr.Trim();
            int count = mStr.Length / 2;
            byte[] b = new byte[count + 2];
            for (int ctick = 0; ctick < count; ctick++)
            {
                string temp = mStr.Substring(ctick * 2, 2);
                b[ctick] = Convert.ToByte(temp, 16);
                wCrc ^= b[ctick];
                for (int j = 0; j < 8; j++)
                {
                    if ((wCrc & 0x0001) != 0)
                    { wCrc = (wCrc >> 1) ^ wPolynom; }
                    else { wCrc = wCrc >> 1; }
                }
            }
            string strCrc = wCrc.ToString("X4");
            b[count] = Convert.ToByte(strCrc.Substring(2, 2), 16);
            b[count + 1] = Convert.ToByte(strCrc.Substring(0, 2), 16);

            return b;
        }

        [Obsolete("Connect has been deprecated.  Use ConnectPLC.")]
        public bool Connect()
        {
            try
            {
                if (curSerialPort.PortName != "" && curSerialPort != null && curSerialPort.IsOpen == false)
                {
                    curSerialPort.Open();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                return false;
            }
        }

        [Obsolete("ReConnect has been deprecated.  Use ReConnectPLC.")]
        public bool ReConnect()
        {
            while (curSerialPort.PortName != "" && curSerialPort.IsOpen == false && Close == false)
            {
                try
                {
                    curSerialPort.Open();
                    //SPReadTh = new Thread(SPRead);
                    //SPReadTh.Start();
                }
                catch (Exception ex)
                {
                    Thread.Sleep(mTime);
                    Debug.Print("ReConnect:" + ex.Message + mTime);
                    mTime = mTime < 1000 ? mTime + 50 : 1000;
                }
            }
            return true;
        }

        [Obsolete("Closed has been deprecated.  Use DisConnectPLC.")]
        public void Closed()
        {
            try
            {
                Close = true;
                if (curSerialPort != null)
                    curSerialPort.Close();
            }
            catch { }
        }
    }
}