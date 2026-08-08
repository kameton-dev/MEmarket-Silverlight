//using System;
//using System.IO;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using SocketEx;

//namespace MEmarket.Services
//{
//    public class TelnetClient : IDisposable
//    {
//        public TelnetClient(TimeSpan sendRate, CancellationToken token)
//        {
//            this._port = 23;
//            this._sendRate = sendRate;
//            this._sendRateLimit = new SemaphoreSlim(1);
//            this._internalCancellation = new CancellationTokenSource();
//            token.Register(delegate ()
//            {
//                this._internalCancellation.Cancel();
//            });
//        }

//        public void Connect()
//        {
//            if (this._tcpClient != null)
//            {
//                this.OnErrorReceived("Connect aborted: Reconnecting is not supported. You must dispose of this instance and instantiate a new TelnetClient");
//                return;
//            }
//            try
//            {
//                this._port = (int)short.Parse(this.TelnetPort);
//            }
//            catch (Exception)
//            {
//            }
//            this._tcpClient = new TcpClient();
//            this._tcpClient.Connect(this.TelnetIP, this._port);
//            this._tcpReader = new StreamReader(this._tcpClient.GetStream());
//            this._tcpWriter = new StreamWriter(this._tcpClient.GetStream())
//            {
//                AutoFlush = true
//            };
//            Task.Run(delegate ()
//            {
//                this.WaitForMessage();
//            });
//        }

//        public void Connect(string TelnetIP, int _port)
//        {
//            if (this._tcpClient != null)
//            {
//                this.OnErrorReceived("Connect aborted: Reconnecting is not supported. You must dispose of this instance and instantiate a new TelnetClient");
//                return;
//            }
//            this._tcpClient = new TcpClient();
//            this._tcpClient.Connect(TelnetIP, _port);
//            this._tcpReader = new StreamReader(this._tcpClient.GetStream());
//            this._tcpWriter = new StreamWriter(this._tcpClient.GetStream())
//            {
//                AutoFlush = true
//            };
//            Task.Run(delegate ()
//            {
//                this.WaitForMessage();
//            });
//        }

//        public void Send(string message)
//        {
//            try
//            {
//                foreach (string text in this.securityCheck)
//                {
//                    if (message.Trim().ToLower().Contains(text.ToLower()))
//                    {
//                        this.OnErrorReceived("Command blocked due security concerns");
//                        return;
//                    }
//                }
//                if (!message.Trim().StartsWith("[") && !message.Trim().StartsWith("]") && !message.Trim().Equals("}") && !message.Trim().Equals("//") && !message.Trim().Equals("/*") && !message.Trim().Equals("#") && !message.Trim().Equals("{") && !message.Trim().ToLower().StartsWith("rem ") && !message.Trim().ToLower().StartsWith("::") && !message.Trim().EndsWith("++") && !message.Trim().EndsWith("--"))
//                {
//                    if (message.StartsWith("cmd:"))
//                    {
//                        message = message.Replace("cmd:", "");
//                    }
//                    if (!message.EndsWith("\r\n"))
//                    {
//                        message += "\r\n";
//                    }
//                    this._sendRateLimit.Wait(this._internalCancellation.Token);
//                    this._tcpWriter.WriteLine(message);
//                    Task.Delay(this._sendRate, this._internalCancellation.Token);
//                    try
//                    {
//                        this.OnMessageReceived(message.Replace("\r\n", ""));
//                    }
//                    catch (Exception)
//                    {
//                    }
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                this.OnErrorReceived("Send aborted: IsCancellationRequested == true");
//            }
//            catch (ObjectDisposedException)
//            {
//                this.OnErrorReceived("Send failed: _tcpWriter or _tcpWriter.BaseStream disposed");
//            }
//            catch (IOException)
//            {
//                this.OnErrorReceived("Send failed: Socket disconnected unexpectedly");
//            }
//            catch (Exception ex)
//            {
//                this.OnErrorReceived("Send failed: " + ex);
//            }
//            finally
//            {
//                this._sendRateLimit.Release();
//            }
//        }

//        private void WaitForMessage()
//        {
//            try
//            {
//                while (!this._internalCancellation.IsCancellationRequested)
//                {
//                    string text;
//                    try
//                    {
//                        if (!this._tcpClient.Connected)
//                        {
//                            this.OnErrorReceived("WaitForMessage aborted: _tcpClient is not connected");
//                            goto IL_C0;
//                        }
//                        text = this._tcpReader.ReadLine();
//                        if (text == null)
//                        {
//                            this.OnErrorReceived("WaitForMessage aborted: _tcpReader reached end of stream");
//                            goto IL_C0;
//                        }
//                    }
//                    catch (ObjectDisposedException)
//                    {
//                        this.OnErrorReceived("WaitForMessage aborted: _tcpReader or _tcpReader.BaseStream disposed. This is expected after calling Disconnect()");
//                        goto IL_C0;
//                    }
//                    catch (IOException)
//                    {
//                        this.OnErrorReceived("WaitForMessage aborted: Socket disconnected unexpectedly");
//                        goto IL_C0;
//                    }
//                    catch (Exception ex)
//                    {
//                        this.OnErrorReceived("WaitForMessage aborted: " + ex);
//                        goto IL_C0;
//                    }
//                    this.OnErrorReceived("WaitForMessage received: message [" + text.Length + "]");
//                    try
//                    {
//                        this.OnMessageReceived(text);
//                    }
//                    catch (Exception)
//                    {
//                    }
//                    Task.Delay(1);
//                    continue;
//                    IL_C0:
//                    return;
//                }
//                this.OnErrorReceived("WaitForMessage aborted: IsCancellationRequested == true");
//            }
//            finally
//            {
//                this.OnErrorReceived("WaitForMessage completed: Calling Disconnect");
//                this.Disconnect();
//            }
//        }

//        private void ParseTelnet(StringBuilder sb)
//        {
//            while (this._tcpClient.Available > 0)
//            {
//                int num = this._tcpClient.GetStream().ReadByte();
//                int num2 = num;
//                if (num2 != -1)
//                {
//                    if (num2 == 255)
//                    {
//                        int num3 = this._tcpClient.GetStream().ReadByte();
//                        if (num3 != -1)
//                        {
//                            switch (num3)
//                            {
//                                case 251:
//                                case 252:
//                                case 253:
//                                case 254:
//                                    {
//                                        int num4 = this._tcpClient.GetStream().ReadByte();
//                                        if (num4 != -1)
//                                        {
//                                            this._tcpClient.GetStream().WriteByte(byte.MaxValue);
//                                            if (num4 == 3)
//                                            {
//                                                this._tcpClient.GetStream().WriteByte((byte)((num3 == 253) ? 251 : 253));
//                                            }
//                                            else
//                                            {
//                                                this._tcpClient.GetStream().WriteByte((byte)((num3 == 253) ? 252 : 254));
//                                            }
//                                            this._tcpClient.GetStream().WriteByte((byte)num4);
//                                        }
//                                        break;
//                                    }
//                                case 255:
//                                    sb.Append(num3);
//                                    break;
//                            }
//                        }
//                    }
//                    else
//                    {
//                        sb.Append((char)num);
//                    }
//                }
//            }
//        }

//        public void Disconnect()
//        {
//            try
//            {
//                this._internalCancellation.Cancel();
//                this._tcpReader.Dispose();
//                this._tcpWriter.Dispose();
//                this._tcpClient.Dispose();
//            }
//            catch (Exception ex)
//            {
//                this.OnErrorReceived("Disconnect error: " + ex);
//            }
//            finally
//            {
//                this.OnConnectionClosed();
//            }
//        }

//        private void OnMessageReceived(string message)
//        {
//            EventHandler<string> messageReceived = this.MessageReceived;
//            if (messageReceived != null)
//            {
//                messageReceived.Invoke(this, message);
//            }
//        }

//        private void OnErrorReceived(string message)
//        {
//            EventHandler<string> errorReceived = this.ErrorReceived;
//            if (errorReceived != null)
//            {
//                errorReceived.Invoke(this, message);
//            }
//        }

//        private void OnConnectionClosed()
//        {
//            EventHandler connectionClosed = this.ConnectionClosed;
//            if (connectionClosed != null)
//            {
//                connectionClosed.Invoke(this, new EventArgs());
//            }
//        }

//        public void Dispose()
//        {
//            this.Dispose(true);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (this._disposed)
//            {
//                return;
//            }
//            if (disposing)
//            {
//                this.Disconnect();
//            }
//            this._disposed = true;
//        }

//        private int _port;
//        private readonly TimeSpan _sendRate;
//        private readonly SemaphoreSlim _sendRateLimit;
//        private readonly CancellationTokenSource _internalCancellation;
//        private TcpClient _tcpClient;
//        private StreamReader _tcpReader;
//        private StreamWriter _tcpWriter;
//        public EventHandler<string> ErrorReceived;
//        public EventHandler<string> MessageReceived;
//        public EventHandler ConnectionClosed;

//        private string[] securityCheck = new string[]
//        {
//            "\\.\\globalroot\\device\\condrv\\kernelconnect",
//            "\\\\.\\\\globalroot\\\\device\\\\condrv\\\\kernelconnect",
//            "\"%0|%0\""
//        };

//        private string TelnetPort = "23";
//        private string TelnetIP = "127.0.0.1";
//        private bool _disposed;
//    }
//}