# Nostr Unity SDK Game Examples

This directory contains example scenes demonstrating how to integrate Nostr functionality into your Unity games using the Nostr Unity SDK. For setup instructions, please refer to the main [README.md](../../README.md) in the root directory.

## Example Scenes

Each scene demonstrates practical game development use cases:

1. **Secret Key Authentication**
   - Input and validate Nostr secret keys
   - Publish game-related messages
   - Secure key storage and management

2. **High Score Submission**
   - Send player high scores to a specific npub
   - Format and structure score data
   - Handle submission responses

3. **Game Invitations**
   - Send game invitations to other players
   - Include download links and game information
   - Track invitation responses

4. **Leaderboard Setup**
   - Configure a game-specific npub for leaderboards
   - Set up score tracking and validation
   - Manage leaderboard updates and queries

## Implementation Notes

- All examples use the SDK's built-in connection management
- Scenes are designed to be easily integrated into existing projects
- Each example includes commented code explaining the implementation
- Error handling and user feedback are implemented in each scene

## Getting Started

1. Import the example scenes:
   - Copy the contents of the `Scenes` directory into your Unity project's Assets folder
   - Copy the contents of the `Scripts` directory into your Unity project's Assets folder

2. Open the scene you want to explore
3. Review the attached scripts and comments
4. Modify the example to fit your game's needs

## Troubleshooting

If you encounter any issues:
1. Check the Unity Console for error messages
2. Verify your internet connection
3. Ensure you have the correct npub/nsec keys configured
4. Check that your game-specific npub is properly set up

## Contributing

Feel free to submit issues and enhancement requests! 