using System;
using System.Threading.Tasks;

namespace NNostrUnitySDK
{
    public interface INostrClient : IDisposable
    {
        bool IsConnected { get; }
        string[] Relays { get; }
        
        event EventHandler<string> MessageReceived;
        event EventHandler<Exception> ErrorOccurred;

        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendMessageAsync(string message);
    }
} 