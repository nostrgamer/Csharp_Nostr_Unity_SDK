# NNostr Unity SDK

A Unity SDK for integrating Nostr protocol into your Unity applications.

## Installation

Add the package to your Unity project using the Package Manager:
1. Open Package Manager (Window > Package Manager)
2. Click the + button
3. Select "Add package from disk"
4. Navigate to and select the `package.json` file

## Usage

### Basic Setup

1. Add the NostrManager component to a GameObject in your scene
2. Configure the relay URLs in the inspector (default is "wss://relay.damus.io")
3. Subscribe to events in your code:

```csharp
public class NostrExample : MonoBehaviour
{
    private NostrManager _nostrManager;

    void Start()
    {
        _nostrManager = GetComponent<NostrManager>();
        _nostrManager.OnMessageReceived += HandleMessage;
        _nostrManager.OnError += HandleError;
        _nostrManager.ConnectToRelay();
    }

    private void HandleMessage(object sender, string message)
    {
        Debug.Log($"Received: {message}");
    }

    private void HandleError(object sender, Exception ex)
    {
        Debug.LogError($"Error: {ex.Message}");
    }

    public void SendMessage(string message)
    {
        _nostrManager.SendNostrMessage(message);
    }

    private void OnDestroy()
    {
        if (_nostrManager != null)
        {
            _nostrManager.DisconnectFromRelay();
        }
    }
}
```

### Features

- Connect to multiple Nostr relays
- Send and receive messages
- Automatic error handling and logging
- Thread-safe operations
- Proper cleanup on scene changes

### Requirements

- Unity 2022.3 or newer
- .NET Standard 2.1 compatible runtime

### API Reference

`NostrManager` Methods:
- `ConnectToRelay()` - Connect to configured relay(s)
- `DisconnectFromRelay()` - Disconnect from all relays
- `SendNostrMessage(string message)` - Send a message to connected relay(s)

Events:
- `OnMessageReceived` - Fired when a message is received
- `OnError` - Fired when an error occurs

## License

MIT License 