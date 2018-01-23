using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

#region 版本1.0
//TCP客户端类，使用Socket方法
//可设置本地固定端口和远程IP和端口
//TCPSend方法可实现发送等待回应的一发一收功能，也可以发送后不等待回应
//ConnectState表示当前连接的状态
//TCPSendState表示发送等待回应的状态，没有回应或超时就为Fasle
//编写：丁晓函
#endregion

namespace DXHTCP
{
    public class DXHTCPClient
    {
        #region 通用连接属性，外部可设置

        /// <summary>
        /// 连接用的套接字
        /// </summary>
        public Socket DXHSocket;
        /// <summary>
        /// 要连接的目标远程IP地址
        /// </summary>
        public string RemoteIPAddress = "LocalHost";
        /// <summary>
        /// 要连接的目标远程端口
        /// </summary>
        public int RemoteIPPort = 9000;
        /// <summary>
        /// 绑定的本地端口,0表示不绑定
        /// </summary>
        public int LocalIPPort = 0;
        /// <summary>
        /// 接受超时时间
        /// </summary>
        public int ReceiveTimeout = 1000;
        /// <summary>
        /// 发送超时时间
        /// </summary>
        public int SendTimeout = 1000;
        /// <summary>
        /// 异常断开后自动重连
        /// </summary>
        public bool ReConnect = true;
        /// <summary>
        /// 接受数据是否回车结尾,默认false，true：接受的数据累加到回车符才返回；false：接受到数据立即返回
        /// </summary>
        public bool NewLine = false;
        /// <summary>
        /// 是否内部打印接受的数据(Console输出)
        /// </summary>
        public bool Print = true;

        #endregion

        #region 内部私有变量

        /// <summary>
        /// 阻塞线程用，供TCPSend方法使用，用于一发一收
        /// </summary>
        private ManualResetEvent Pause_Event = new ManualResetEvent(false);

        /// <summary>
        /// 锁定线程用，供TCPSend方法使用，防止收发数据之前互相错乱
        /// </summary>
        private System.Object TCPLock = new System.Object();

        /// <summary>
        /// 接受缓冲区转成的字符串
        /// </summary>
        private string TCPRecStr = "";

        /// <summary>
        /// 接受的字符串累加用判定回车的中间变量
        /// </summary>
        private string ToLine = "";

        /// <summary>
        /// 接受的最终一次数据
        /// </summary>
        private string OneRec = "";

        private string mConnectState = "Idle";
        /// <summary>
        /// 连接状态 Idle Connecting Connected Faulted Closing Closed
        /// </summary>
        private string _ConnectState
        {
            get { return mConnectState; }
            set
            {
                if (mConnectState != value)
                {
                    mConnectState = value;
                    if (ConnectStateChanged != null)
                        ConnectStateChanged(null, mConnectState);
                    if (mConnectState == "Faulted")
                    {//如果连接断开，重连
                        if (ReConnect)
                            StartTCPConnect();
                    }
                    else if (mConnectState == "Connected")
                    {//如果连接成功，接受
                        StartTCPReceive();
                    }
                }
            }
        }

        private bool mTCPSendState = false;
        /// <summary>
        /// 获取通信状态，true表示TCPSend有回应，false表示TCPSend无回应
        /// </summary>
        private bool _TCPSendState
        {
            get { return mTCPSendState; }
            set
            {
                if (mTCPSendState != value)
                {
                    mTCPSendState = value;
                    if (TCPSendStateChanged != null)
                        TCPSendStateChanged(null, mTCPSendState);
                    if (mTCPSendState == false)
                    {

                    }
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 接受到一次数据的事件
        /// </summary>
        public event EventHandler<string> Received;

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        public event EventHandler<string> ConnectStateChanged;

        /// <summary>
        /// 通信状态改变事件
        /// </summary>
        public event EventHandler<bool> TCPSendStateChanged;

        #endregion

        #region 外部可读状态

        /// <summary>
        /// 获取连接状态 Idle Connecting Connected Faulted Closing Closed 只读
        /// </summary>
        public string ConnectState
        {
            get { return mConnectState; }
        }
        /// <summary>
        /// 获取通信状态，true表示TCPSend有回应，false表示TCPSend无回应
        /// </summary>
        public bool TCPSendState
        {
            get { return mTCPSendState; }
        }
        #endregion


        #region 功能函数

        /// <summary>
        /// Received事件的小封装
        /// </summary>
        /// <param name="str"></param>
        private void OnReceived(string str)
        {
            if (Received != null)
                Received(this, str);
        }

        /// <summary>
        /// 通过低级操作模式，关闭套接字的保持连接功能，让TCP在异常断开时短时间内尝试重连然后立刻断开，解决了拔网线等连接不断开的问题
        /// </summary>
        /// <param name="KeepAlive"></param>
        /// <param name="KeepAliveTime"></param>
        /// <param name="KeepAliveInterval"></param>
        private void SetKeepAlive(int KeepAlive, int KeepAliveTime, int KeepAliveInterval)
        {
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)KeepAlive).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)KeepAliveTime).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
            BitConverter.GetBytes((uint)KeepAliveInterval).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
            DXHSocket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
        }

        bool HasStartTCPConnect = false;
        /// <summary>
        /// 开始连接，直到连接成功，设置ReConnect会在异常断开连接时主动重连
        /// </summary>
        public async void StartTCPConnect()
        {
            if (!HasStartTCPConnect)
                HasStartTCPConnect = true;
            else
                return;//防止重复执行

            DXHSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//实例化Socket

            DXHSocket.ReceiveTimeout = ReceiveTimeout;//设置Socket的一些属性
            DXHSocket.SendTimeout = SendTimeout;
            SetKeepAlive(1, 1000, 100);//缩短KeepAlive的时间，让连接异常断开后1秒关闭
            int mTime = 100;//重连的间隔时间，目的是间隔越来越长
            bool TempConnected = false;//临时变量，目的是异步操作后再更新状态，防止一些线程问题 
            while (_ConnectState!="Connected" && _ConnectState!="Closing" && HasStartTCPConnect)//如果已连接或在断开流程中，就退出连接循环
            {
                _ConnectState = "Connecting";//在异步操作之前设置状态为Connecting
                Task Task_Connect = Task.Run(() =>
                  {//异步方法
                      try
                      {
                          if (LocalIPPort != 0 && DXHSocket.IsBound == false)//如果本地端口设置不为0，并且Socket端口未绑定，说明需要绑定本地端口
                          {
                              //设置Socket关闭时立即关闭，才会不占用端口，否则关闭连接时需要等待Socket自动关闭，造成一段时间内重连该端口会被上一个套接字占用端口
                              DXHSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, false);//关闭套接字时不占用端口
                              DXHSocket.Bind(new IPEndPoint(System.Net.IPAddress.Any, LocalIPPort));//Socket绑定端口，IP地址也可以设置但意义不大
                          }
                          DXHSocket.Connect(RemoteIPAddress, RemoteIPPort);//开始连接远程服务器，连接失败会直接到异常处理
                          TempConnected = true;//置临时变量连接成功
                          Console.WriteLine("连接成功！");
                      }
                      catch (Exception ex)
                      {//连接失败延迟一会再连接
                          Console.WriteLine("ReConnect:" + ex.Message + ",将在" + mTime + "ms后重试！");
                          Thread.Sleep(mTime);
                          mTime = mTime < 1000 ? mTime + 50 : 1000;
                      }
                  });
                await Task_Connect;
                if (TempConnected)
                {//通过临时变量，在异步之后设置状态为Connected
                    _ConnectState = "Connected";
                }
            }

            HasStartTCPConnect = false;

            if(_ConnectState=="Closing")//如果状态在Closing中，设置状态为Closed
                _ConnectState = "Closed";
        }
        /// <summary>
        /// 关闭套接字，断开连接，不会主动重连
        /// </summary>
        public void Close()
        {
            try
            {
                _ConnectState = "Closing";//设置状态为Closing，Socket关闭后会结束一些循环

                DXHSocket.Close();//关闭Socket
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        bool HasStartTCPReceive = false;
        /// <summary>
        /// 开始结束数据，连接成功后自动开始
        /// </summary>
        private async void StartTCPReceive()
        {
            if (!HasStartTCPReceive)
                HasStartTCPReceive = true;
            else
                return;//防止重复执行

            while (DXHSocket.Connected)//Socket已连接时
            {
                bool TempConnected = true;
                OneRec = "";//复位数据
                Task TaskRec = Task.Run(() =>
                  {
                      if (DXHSocket.Poll(-1, SelectMode.SelectRead) && DXHSocket.Connected)//等待数据读取
                      {
                          byte[] Receivedbytes = new byte[1024];
                          try
                          {
                              int bytesRec = DXHSocket.Receive(Receivedbytes);//存入缓冲区
                              if (bytesRec == 0)
                              {//数据长度为0，说明连接断开
                                  TempConnected = false;
                                  Console.WriteLine("bytesRec == 0");
                              }
                              else
                              {//处理数据

                                  TCPRecStr = Encoding.UTF8.GetString(Receivedbytes, 0, bytesRec);//将字节数据转成UTF8编码字符串

                                  if (Print)//如果Print，打印
                                      Console.WriteLine("TCPRec:" + TCPRecStr);

                                  ToLine += TCPRecStr;//累加到接受到回车的字符串

                                  if (ToLine.Contains(Environment.NewLine) || NewLine == false)//如果选择了接受到回车并且ToLine累加到回车   或者未选择接受到回车
                                  {
                                      if (NewLine == false)
                                          OneRec = ToLine;//未选择接受到回车，就直接传出
                                      else
                                          OneRec = ToLine.Replace(Environment.NewLine, "");//选择了接受到回车，就把回车给去掉
                                      ToLine = "";//复位累加字符串
                                  }
                              }
                          }
                          catch (Exception ex)
                          {//连接异常断开时
                              Console.WriteLine("TCPRecEX:" + ex.Message);
                              TempConnected = false;
                          }
                      }
                      else
                      {//Socket被关闭时
                          Console.WriteLine("POLLFalse");
                          TempConnected = false;
                      }
                  });
                await TaskRec;
                if (TempConnected == false)
                {//连接断开
                    if (_ConnectState != "Closing")//不是自己关闭Socket
                    {
                        DXHSocket.Close();//关闭Socket并置状态为Faulted，会自动尝试重连
                        _ConnectState = "Faulted";
                    }
                    else
                    {//自己关闭Socket，置状态位Closed，不为自动重连
                        _ConnectState = "Closed";
                    }
                }
                if (OneRec != "")
                {
                    Pause_Event.Set();//发出信号，取消阻塞线程
                    OnReceived(OneRec);//处理接受的数据
                }
            }
            HasStartTCPReceive = false;
        }
        /// <summary>
        /// 发送数据给服务端，并返回服务端回复的字符串
        /// </summary>
        /// <param name="StrToSend">待发送的字符串</param>
        /// <param name="Wait">是否等待服务端回复，默认等待</param>
        /// <param name="WaitTimeout">等待服务端回复的时间，默认1000ms</param>
        /// <returns>返回服务端回复的字符串</returns>
        public string TCPSend(string StrToSend, bool Wait = true, int WaitTimeout = 1000)
        {
            if (DXHSocket != null)
            {
                if (DXHSocket.Connected)
                {
                    try
                    {
                        lock (TCPLock)//锁定线程，防止多个线程同时调用，导致收发顺序错乱
                        {
                            Pause_Event.Reset();//重置阻塞线程信号
                            ToLine = "";
                            OneRec = "";
                            Console.WriteLine("TCPToSend:" + StrToSend);
                            byte[] ByteToSend = System.Text.Encoding.UTF8.GetBytes(StrToSend);
                            DXHSocket.Send(ByteToSend);//发送数据
                            if (!Wait)//如果不等待回应就立即返回空值
                                return "";
                            if (Pause_Event.WaitOne(WaitTimeout))//开始阻塞线程，等待信号，直到Pause_Event.Set()，或者超出设置的时间
                            {//接受到回应数据，置状态为true
                                string str = OneRec;
                                ToLine = "";
                                OneRec = "";
                                _TCPSendState = true;
                                return str;
                            }
                            else
                            {//超时了远程服务器没回应，置状态为false
                                _TCPSendState = false;
                                return "TimeOut";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _TCPSendState = false;
                        Debug.Print(ex.Message);
                        Console.WriteLine(ex.Message);
                        return "Error";
                    }
                }
                else
                    return "Error";
            }
            else
                return "Error";
        }

        #endregion

        #region 线程操作，状态更新必须用委托，现已改成异步方法
        //private void Connect()
        //{
        //    DXHSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //    if (LocalIPPort != 0)
        //        DXHSocket.Bind(new IPEndPoint(System.Net.IPAddress.Any, LocalIPPort));
        //    DXHSocket.ReceiveTimeout = 1000;
        //    DXHSocket.SendTimeout = 1000;
        //    SetKeepAlive(1, 1000, 100);
        //    int mTime = 100;
        //    while (!ConnectState && !Closed)
        //    {
        //        try
        //        {
        //            DXHSocket.Connect(RemoteIPAddress, RemoteIPPort);
        //            ConnectState = true;
        //            Console.WriteLine("连接成功！");
        //            Thread RecThread = new Thread(TCPRec);
        //            RecThread.IsBackground = true;
        //            RecThread.Start();
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("ReConnect:" + ex.Message + ",将在" + mTime + "ms后重试！");
        //            Thread.Sleep(mTime);
        //            mTime = mTime < 1000 ? mTime + 50 : 1000;
        //        }
        //    }
        //    Closed = false;
        //}
        //public void BeginConnect()
        //{
        //    Thread ConnectThread = new Thread(Connect);
        //    ConnectThread.IsBackground = true;
        //    ConnectThread.Start();
        //}
        //private void TCPRec()
        //{
        //    while (DXHSocket.Connected)
        //    {
        //        if (DXHSocket.Poll(-1, SelectMode.SelectRead) && DXHSocket.Connected)
        //        {
        //            byte[] Receivedbytes = new byte[1024];
        //            try
        //            {
        //                int bytesRec = DXHSocket.Receive(Receivedbytes);
        //                if (bytesRec == 0)
        //                {
        //                    DXHSocket.Close();
        //                    ConnectState = false;
        //                }
        //                else
        //                {
        //                    TCPRecStr = Encoding.UTF8.GetString(Receivedbytes, 0, bytesRec);
        //                    if (Print)
        //                        Console.WriteLine("TCPRec:" + TCPRecStr);
        //                    ToLine += TCPRecStr;
        //                    if (ToLine.Contains(Environment.NewLine) || NewLine == false)
        //                    {
        //                        if (NewLine == false)
        //                            OneRec = ToLine;
        //                        else
        //                            OneRec = ToLine.Replace(Environment.NewLine, "");
        //                        ToLine = "";
        //                        Pause_Event.Set();
        //                        OnReceived(OneRec);
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Debug.Print("TCPRecEX:" + ex.Message);
        //                Console.WriteLine("TCPRecEX:" + ex.Message);
        //                DXHSocket.Close();
        //                ConnectState = false;
        //                return;
        //            }
        //        }
        //        else
        //        {
        //            Console.WriteLine("POLLFalse");
        //            ConnectState = false;
        //        }
        //    }
        //}
        #endregion
    }
}
