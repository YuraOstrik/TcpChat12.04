using System.Net;
using System.Net.Sockets;

namespace TcpChat12._03
{
    public partial class Form1 : Form
    {
        private TcpListener tcpListener;
        private List<ClientObject> clients;
        private CancellationTokenSource cancellationTokenSource;

        public Form1()
        {
            InitializeComponent();
            clients = new List<ClientObject>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 8888);
                tcpListener.Start();
                MessageBox.Show("Сервер запущен.");

                await ListenForClientsAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска сервера: {ex.Message}");
            }
        }

        private async Task ListenForClientsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await tcpListener.AcceptTcpClientAsync();
                    var clientObject = new ClientObject(tcpClient, this);
                    clients.Add(clientObject);
                    Task.Run(() => clientObject.ProcessAsync());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        public async Task BroadcastMessageAsync(string message, string senderId)
        {
            if (message.Contains("вошел в чат"))
            {
                UpdateListBox(message); 
            }

            foreach (var client in clients)
            {
                if (client.Id != senderId)
                {
                    await client.Writer.WriteLineAsync(message);
                    await client.Writer.FlushAsync();
                }
            }
        }

        public void UpdateListBox(string message)
        {
            if (listBox1.InvokeRequired)
            {
                listBox1.Invoke(new Action(() => listBox1.Items.Add(message)));
            }
            else
            {
                listBox1.Items.Add(message);
            }
        }

        public void RemoveConnection(string id)
        {
            var client = clients.Find(c => c.Id == id);
            if (client != null)
            {
                clients.Remove(client);
                client.Close();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancellationTokenSource.Cancel();
            tcpListener?.Stop();
            foreach (var client in clients)
            {
                client.Close();
            }
        }
    }

    public class ClientObject
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public StreamWriter Writer { get; }
        private StreamReader Reader { get; }
        private TcpClient client;
        private Form1 serverForm;

        public ClientObject(TcpClient tcpClient, Form1 server)
        {
            client = tcpClient;
            serverForm = server;

            var stream = client.GetStream();
            Reader = new StreamReader(stream);
            Writer = new StreamWriter(stream);
        }

        public async Task ProcessAsync()
        {
            try
            {
                string userName = await Reader.ReadLineAsync();
                string message = $"{userName} вошел в чат";
                await serverForm.BroadcastMessageAsync(message, Id);

                while (true)
                {
                    message = await Reader.ReadLineAsync();
                    if (message == null) break;

                    message = $"{userName}: {message}";
                    await serverForm.BroadcastMessageAsync(message, Id);
                }
            }
            catch (Exception)
            {
                string message = "Пользователь покинул чат";
                await serverForm.BroadcastMessageAsync(message, Id);
            }
            finally
            {
                serverForm.RemoveConnection(Id);
            }
        }

        public void Close()
        {
            Writer.Close();
            Reader.Close();
            client.Close();
        }
    }
}
