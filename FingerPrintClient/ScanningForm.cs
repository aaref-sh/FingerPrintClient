using Microsoft.AspNetCore.SignalR.Client;

namespace FingerPrintClient;

public partial class ScanningForm : Form
{
    private HubConnection connection;
    public ScanningForm()
    {
        InitializeComponent();

        connection = new HubConnectionBuilder()
                .WithUrl("https://example.com/chat")
                .Build();
        // Register an event handler for the ReceiveMessage hub method
        connection.On<string, string>("ReceiveMessage", handler);
    }

    private void handler(string arg1, string arg2)
    {

    }

    void initFingerprintDevice ()
    {
        FingerPrintClient.FP.FPScanner scanner = new();
        scanner.Start();

    }

    private async void connectButton_Click(object sender, EventArgs e)
    {
        try
        {
            // Start the connection
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
        }
    }
    private async void sendButton_Click(object sender, EventArgs e)
    {
        try
        {
            // Call the SendMessage hub method from the client
            await connection.InvokeAsync("SendMessage","arg");
        }
        catch (Exception ex)
        {
        }
    }
}
