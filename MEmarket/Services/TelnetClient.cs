using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocketEx;

namespace MEmarket.Services
{
    public class TelnetClient : IDisposable
    {
        private readonly TimeSpan _sendRate;
        private readonly SemaphoreSlim _sendRateLimit;
        private readonly CancellationTokenSource _internalCancellation;

        private TcpClient _tcpClient;
        private StreamReader _tcpReader;
        private StreamWriter _tcpWriter;
        private bool _disposed;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ErrorReceived;
        public event EventHandler ConnectionClosed;

        public TelnetClient(TimeSpan sendRate, CancellationToken externalToken)
        {
            _sendRate = sendRate;
            _sendRateLimit = new SemaphoreSlim(1);
            _internalCancellation = new CancellationTokenSource();
            externalToken.Register(() => _internalCancellation.Cancel());
        }

        public void Connect(string ip, int port)
        {
            if (_tcpClient != null)
            {
                OnErrorReceived("Подключение отклонено: повторное подключение не поддерживается. Пересоздайте экземпляр класса.");
                return;
            }

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);

                var stream = _tcpClient.GetStream();
                _tcpReader = new StreamReader(this._tcpClient.GetStream(), Encoding.UTF8, false);
                var utf8NoBom = new UTF8Encoding(false);
                _tcpWriter = new StreamWriter(_tcpClient.GetStream(), utf8NoBom)
                {
                    AutoFlush = true
                };

                Task.Run(() => WaitForMessage());
            }
            catch (Exception ex)
            {
                OnErrorReceived("Ошибка при подключении: " + ex.Message);
            }
        }

        public void Send(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                string trimmed = message.Trim();
                if (trimmed.StartsWith("[") || trimmed.StartsWith("]") || trimmed.Equals("}") ||
                    trimmed.Equals("//") || trimmed.Equals("/*") || trimmed.Equals("#") ||
                    trimmed.Equals("{"))
                {
                    return;
                }

                if (!message.EndsWith("\r\n"))
                {
                    message += "\r\n";
                }

                _sendRateLimit.Wait(_internalCancellation.Token);

                if (_tcpWriter != null)
                {
                    _tcpWriter.Write(message);
                    _tcpWriter.Flush();
                    OnMessageReceived(message.Replace("\r\n", ""));
                }

                Task.Delay(_sendRate, _internalCancellation.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                OnErrorReceived("Отправка отменена.");
            }
            catch (Exception ex)
            {
                OnErrorReceived("Ошибка при отправке: " + ex.Message);
            }
            finally
            {
                _sendRateLimit.Release();
            }
        }

        private void WaitForMessage()
        {
            try
            {
                while (!_internalCancellation.IsCancellationRequested)
                {
                    if (_tcpClient == null || !_tcpClient.Connected)
                    {
                        OnErrorReceived("Отключение: TCP-клиент не подключен.");
                        break;
                    }

                    string line = _tcpReader.ReadLine();
                    if (line == null)
                    {
                        OnErrorReceived("Достигнут конец потока чтения.");
                        break;
                    }

                    OnMessageReceived(line);
                }
            }
            catch (Exception ex)
            {
                OnErrorReceived("Ошибка чтения данных: " + ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                _internalCancellation.Cancel();
                _tcpReader?.Dispose();
                _tcpWriter?.Dispose();
                _tcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                OnErrorReceived("Ошибка при отключении: " + ex.Message);
            }
            finally
            {
                OnConnectionClosed();
            }
        }

        protected virtual void OnMessageReceived(string message) => MessageReceived?.Invoke(this, message);
        protected virtual void OnErrorReceived(string message) => ErrorReceived?.Invoke(this, message);
        protected virtual void OnConnectionClosed() => ConnectionClosed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }
}