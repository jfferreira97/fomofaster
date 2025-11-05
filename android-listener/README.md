# FomoFaster Android Listener

Android notification listener app that captures FOMO app trading notifications and forwards them to a backend server.

## Features

- Intercepts FOMO app notifications in real-time
- Extracts trader names, trade amounts, and token information
- **Programmatically clicks clipboard button** to extract contract addresses
- Sends structured data to backend via HTTP POST
- Configurable backend URL
- Connection testing
- Notification listener status monitoring

## Architecture

```
FOMO App (BlueStacks)
    ↓ notification
FomoNotificationListener Service
    ↓ click clipboard button
Android ClipboardManager
    ↓ extract contract address
OkHttp Client
    ↓ HTTP POST
Backend Server (FastAPI)
```

## Prerequisites

1. **Android SDK** - Install Android Studio or SDK command-line tools
2. **Java Development Kit (JDK)** - Version 8 or higher
3. **Gradle** - Included in Android project (gradlew)
4. **BlueStacks** - Android emulator for Windows
5. **ADB** - Android Debug Bridge (included with Android SDK)

## Project Structure

```
android-listener/
├── app/
│   ├── src/main/
│   │   ├── java/com/fomofaster/listener/
│   │   │   ├── FomoNotificationListener.java   # Core notification service
│   │   │   └── MainActivity.java               # Configuration UI
│   │   ├── res/
│   │   │   ├── layout/
│   │   │   │   └── activity_main.xml           # UI layout
│   │   │   └── values/
│   │   │       ├── strings.xml                 # String resources
│   │   │       └── colors.xml                  # Color resources
│   │   └── AndroidManifest.xml                 # App configuration
│   └── build.gradle                            # App dependencies
├── build.gradle                                # Project configuration
├── settings.gradle                             # Gradle settings
└── README.md                                   # This file
```

## Building the App

### Option 1: Using Android Studio

1. Open Android Studio
2. Click "Open an existing project"
3. Navigate to `android-listener` folder
4. Wait for Gradle sync to complete
5. Click Build > Build Bundle(s) / APK(s) > Build APK(s)
6. APK will be in `app/build/outputs/apk/debug/app-debug.apk`

### Option 2: Using Command Line (Recommended)

1. Open Command Prompt or PowerShell
2. Navigate to the `android-listener` directory:
   ```cmd
   cd c:\Users\jose9\Desktop\repos\fomofaster\android-listener
   ```

3. Initialize Gradle wrapper (first time only):
   ```cmd
   gradle wrapper --gradle-version 8.0
   ```

4. Build the debug APK:
   ```cmd
   gradlew.bat assembleDebug
   ```

5. The APK will be generated at:
   ```
   app\build\outputs\apk\debug\app-debug.apk
   ```

## Installing on BlueStacks

### Step 1: Enable ADB in BlueStacks

1. Open BlueStacks
2. Click the hamburger menu (three lines) in the top-right
3. Go to Settings > Advanced
4. Enable "Android Debug Bridge (ADB)"
5. Note the connection address (usually `127.0.0.1:5555`)

### Step 2: Connect via ADB

```cmd
# Connect to BlueStacks
adb connect 127.0.0.1:5555

# Verify connection
adb devices
```

You should see:
```
List of devices attached
127.0.0.1:5555    device
```

### Step 3: Install APK

```cmd
adb install app\build\outputs\apk\debug\app-debug.apk
```

For reinstalling (if already installed):
```cmd
adb install -r app\build\outputs\apk\debug\app-debug.apk
```

## Configuration

### Step 1: Open the App

1. In BlueStacks, find "FomoFaster Listener" in the app drawer
2. Tap to open

### Step 2: Configure Backend URL

1. Enter your backend URL in the text field
   - For local testing: `http://10.0.2.2:8000` (BlueStacks uses this to reach host machine)
   - For remote server: `http://your-server-ip:8000`
2. Tap "Save Configuration"

### Step 3: Enable Notification Listener

1. Tap "Enable Notification Listener"
2. Android Settings will open
3. Find "FomoFaster Listener" in the list
4. Toggle it ON
5. Accept the permission prompt
6. Press back to return to the app

### Step 4: Test Connection

1. Tap "Test Connection"
2. Check for success message
3. Verify the backend receives the test payload

### Step 5: Verify Status

- Status should show: "Listener enabled ✓" (green text)
- If red, notification listener is not enabled

## Testing

### View Logs in Real-Time

```cmd
# Filter for FomoFaster logs only
adb logcat | findstr FomoListener
```

### Trigger a Test Notification

1. Make sure FOMO app is installed in BlueStacks
2. Log into FOMO app
3. Follow some active traders
4. Wait for a real trade notification

### Expected Log Output

```
D/FomoListener: FOMO notification detected!
D/FomoListener: Title: AIXBT at $76.2m MC
D/FomoListener: Text: @frankdegods sold $10,759.14
D/FomoListener: Timestamp: 1762121165048
D/FomoListener: Found 1 notification action(s)
D/FomoListener: Action found:
D/FomoListener: Action triggered, waiting for clipboard...
D/FomoListener: Clipboard content: 0xABC123...
D/FomoListener: Sending to backend: {"app":"fomo","title":"AIXBT at $76.2m MC",...}
D/FomoListener: Successfully sent to backend: 200
```

## API Payload Format

The app sends POST requests to `{backend_url}/api/notifications` with this JSON structure:

```json
{
  "app": "fomo",
  "title": "AIXBT at $76.2m MC",
  "text": "@frankdegods sold $10,759.14",
  "contractAddress": "0xABC123...",
  "timestamp": 1762121165048
}
```

## Troubleshooting

### "Listener not enabled" (red text)

**Solution:** Tap "Enable Notification Listener" and toggle on the permission in Android Settings.

### No notifications being captured

**Possible causes:**
1. Notification listener not enabled (check status in app)
2. FOMO app not installed or not logged in
3. No traders followed in FOMO app
4. Check logs: `adb logcat | findstr FomoListener`

### "Connection failed" on test

**Possible causes:**
1. Wrong backend URL
2. Backend not running
3. Firewall blocking connection
4. For local testing, use `http://10.0.2.2:8000` (not `localhost` or `127.0.0.1`)

### Contract address empty

**Possible causes:**
1. Notification doesn't have clipboard action button
2. Clipboard action failed to trigger
3. Check logs to see if action was found and triggered

### Gradle build fails

**Solutions:**
```cmd
# Clean and rebuild
gradlew.bat clean
gradlew.bat assembleDebug

# If Gradle wrapper is missing
gradle wrapper --gradle-version 8.0
```

### ADB connection issues

**Solutions:**
```cmd
# Kill and restart ADB server
adb kill-server
adb start-server
adb connect 127.0.0.1:5555

# Check BlueStacks ADB port in Settings > Advanced
```

## Development

### Making Changes

1. Edit Java files in `app/src/main/java/com/fomofaster/listener/`
2. Edit layouts in `app/src/main/res/layout/`
3. Build: `gradlew.bat assembleDebug`
4. Install: `adb install -r app\build\outputs\apk\debug\app-debug.apk`
5. View logs: `adb logcat | findstr FomoListener`

### Key Files

- **FomoNotificationListener.java** - Main service that intercepts notifications
  - `onNotificationPosted()` - Called when new notification appears
  - `extractContractAddress()` - Clicks clipboard button and reads address
  - `sendToBackend()` - POSTs data to backend

- **MainActivity.java** - Configuration UI
  - Backend URL management
  - Permission checking
  - Connection testing

### Adding Debug Logs

```java
import android.util.Log;

Log.d("FomoListener", "Your debug message here");
Log.e("FomoListener", "Error message", exception);
```

## Backend Requirements

Your backend must provide this endpoint:

```
POST /api/notifications
Content-Type: application/json

{
  "app": "fomo",
  "title": "string",
  "text": "string",
  "contractAddress": "string",
  "timestamp": number
}
```

Expected responses:
- `200 OK` - Notification processed successfully
- `400 Bad Request` - Invalid payload
- `500 Internal Server Error` - Server error

## Security Notes

- App uses `usesCleartextTraffic="true"` for HTTP (development only)
- For production, use HTTPS and remove this flag
- Backend URL is stored in SharedPreferences (plaintext)
- No authentication implemented (add API keys if needed)

## Next Steps

1. **Build Python backend** - FastAPI server to receive notifications
2. **Add Telegram bot** - Forward notifications to Telegram channels
3. **Add filtering** - Let users subscribe to specific traders
4. **Add persistence** - Store notifications in database
5. **Production deployment** - Deploy backend to VPS

## License

MIT License - See LICENSE file for details

## Support

For issues or questions:
1. Check logs: `adb logcat | findstr FomoListener`
2. Verify notification permissions are enabled
3. Test backend connection from app
4. Check FOMO app is working and logged in

---

**Built for FomoFaster** - Real-time crypto trading notifications from FOMO app to Telegram
