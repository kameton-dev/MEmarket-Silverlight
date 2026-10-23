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
        private readonly SemaphoreSlim _sendRateLimit = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _internalCancellation = new CancellationTokenSource();
        private readonly CancellationTokenRegistration _externalRegistration;

        private TcpClient _tcpClient;
        private StreamReader _tcpReader;
        private StreamWriter _tcpWriter;
        private int _disconnected;
        private int _disposed;

        /// <summary>Строка, пришедшая от сервера.</summary>
        public event EventHandler<string> MessageReceived;
        /// <summary>Команда, которую мы отправили (эхо для лога).</summary>
        public event EventHandler<string> MessageSent;
        public event EventHandler<string> ErrorReceived;
        public event EventHandler ConnectionClosed;

        public TelnetClient(TimeSpan sendRate, CancellationToken externalToken)
        {
            _sendRate = sendRate;
            _externalRegistration = externalToken.Register(() =>
            {
                try { _internalCancellation.Cancel(); } catch (ObjectDisposedException) { }
            });
        }

        public Task<bool> ConnectAsync(string ip, int port)
        {
            return Task.Run(() => Connect(ip, port));
        }

        public bool Connect(string ip, int port)
        {
            if (_tcpClient != null)
            {
                OnErrorReceived("Подключение отклонено: повторное подключение не поддерживается. Пересоздайте экземпляр класса.");
                return false;
            }

            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.Connect(ip, port);

                var stream = client.GetStream();
                _tcpReader = new StreamReader(stream, Encoding.UTF8, false);
                _tcpWriter = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                _tcpClient = client;

                Task.Run(() => WaitForMessage());
                return true;
            }
            catch (Exception ex)
            {
                try { client?.Dispose(); } catch { }
                OnErrorReceived("Ошибка при подключении: " + ex.Message);
                return false;
            }
        }

        public Task<bool> SendAsync(string message)
        {
            return Task.Run(() => Send(message));
        }

        /// <summary>Отправляет команду. Возвращает true, если команда реально ушла в сокет.</summary>
        public bool Send(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            string trimmed = message.Trim();
            if (trimmed.StartsWith("[") || trimmed.StartsWith("]") || trimmed.Equals("}") ||
                trimmed.Equals("//") || trimmed.Equals("/*") || trimmed.Equals("#") ||
                trimmed.Equals("{"))
            {
                return false;
            }

            if (!message.EndsWith("\r\n"))
            {
                message += "\r\n";
            }

            // Release() только если семафор действительно захвачен.
            bool acquired = false;
            try
            {
                _sendRateLimit.Wait(_internalCancellation.Token);
                acquired = true;

                var writer = _tcpWriter;
                if (writer == null)
                {
                    OnErrorReceived("Отправка невозможна: нет подключения.");
                    return false;
                }

                writer.Write(message);
                writer.Flush();
                OnMessageSent(message.Replace("\r\n", ""));

                // Пауза между командами. WaitOne вместо Task.Delay().Wait(),
                // чтобы отмена не превращалась в AggregateException.
                if (_internalCancellation.Token.WaitHandle.WaitOne(_sendRate))
                {
                    OnErrorReceived("Отправка отменена.");
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                OnErrorReceived("Отправка отменена.");
                return false;
            }
            catch (Exception ex)
            {
                OnErrorReceived("Ошибка при отправке: " + ex.Message);
                return false;
            }
            finally
            {
                if (acquired)
                {
                    try { _sendRateLimit.Release(); } catch (ObjectDisposedException) { }
                }
            }
        }

        private void WaitForMessage()
        {
            try
            {
                while (!_internalCancellation.IsCancellationRequested)
                {
                    var client = _tcpClient;
                    if (client == null || !client.Connected)
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
                // При штатном Disconnect() поток закрывается под ReadLine — это не ошибка.
                if (!_internalCancellation.IsCancellationRequested)
                {
                    OnErrorReceived("Ошибка чтения данных: " + ex.Message);
                }
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>Идемпотентно: можно вызывать сколько угодно раз, событие ConnectionClosed придёт один раз.</summary>
        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) != 0) return;

            try
            {
                try { _internalCancellation.Cancel(); } catch (ObjectDisposedException) { }
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
        protected virtual void OnMessageSent(string message) => MessageSent?.Invoke(this, message);
        protected virtual void OnErrorReceived(string message) => ErrorReceived?.Invoke(this, message);
        protected virtual void OnConnectionClosed() => ConnectionClosed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            Disconnect();
            _externalRegistration.Dispose();
            _internalCancellation.Dispose();
            _sendRateLimit.Dispose();
        }
    }
}