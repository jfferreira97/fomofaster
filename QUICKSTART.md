# FomoFaster Quick Start Guide

Get FomoFaster running in under 30 minutes!

## Prerequisites

- [x] Windows 10/11 PC
- [x] .NET 8.0 SDK installed
- [x] Android Studio or Android SDK
- [x] BlueStacks 5 installed

## Step 1: Build Android App (5 minutes)

```cmd
cd android-listener
gradle wrapper --gradle-version 8.0
gradlew.bat assembleDebug
```

APK location: `app\build\outputs\apk\debug\app-debug.apk`

## Step 2: Install on BlueStacks (5 minutes)

**Enable ADB in BlueStacks:**
1. Settings → Advanced → Enable ADB

**Install app:**
```cmd
adb connect 127.0.0.1:5555
adb install -r app\build\outputs\apk\debug\app-debug.apk
```

**Configure app in BlueStacks:**
1. Open "FomoFaster Listener"
2. Backend URL: `http://10.0.2.2:8000`
3. Tap "Save Configuration"
4. Tap "Enable Notification Listener"
5. Enable permission in Android Settings

## Step 3: Create Telegram Bot (5 minutes)

**Talk to @BotFather:**
```
You: /newbot
BotFather: Choose a name
You: FomoFaster Notifications
BotFather: Choose a username
You: fomofaster_yourname_bot
BotFather: Done! Token: 1234567890:ABC...
```

**Create channel:**
1. Create new Telegram channel
2. Add bot as administrator
3. Give "Post Messages" permission

**Get channel ID:**
1. Send test message to channel
2. Visit: `https://api.telegram.org/bot<TOKEN>/getUpdates`
3. Find: `"chat":{"id":-1001234567890}`

## Step 4: Configure Backend (2 minutes)

Edit `telegram-bot\FomoFaster.Backend\appsettings.Development.json`:

```json
{
  "Telegram": {
    "BotToken": "1234567890:ABCdefGHIjklMNOpqrsTUVwxyz",
    "ChannelId": -1001234567890
  }
}
```

## Step 5: Run Backend (1 minute)

```cmd
cd telegram-bot\FomoFaster.Backend
dotnet restore
dotnet run
```

Should see:
```
Now listening on: http://0.0.0.0:8000
Telegram bot client initialized successfully
```

## Step 6: Test (2 minutes)

**Test from Android app:**
1. Open FomoFaster app in BlueStacks
2. Tap "Test Connection"
3. Should see "Connection successful!"
4. Check your Telegram channel for test message

**Test from command line:**
```cmd
curl -X POST http://localhost:8000/api/notifications ^
  -H "Content-Type: application/json" ^
  -d "{\"app\":\"fomo\",\"title\":\"TEST at $1m MC\",\"text\":\"@trader bought $1000\",\"contractAddress\":\"0xTEST\",\"timestamp\":1704067200000}"
```

## Step 7: Install FOMO App (5 minutes)

**In BlueStacks:**
1. Open Google Play Store
2. Search "FOMO - Social Trading"
3. Install
4. Create account or log in
5. Follow active traders:
   - @ansem
   - @frankdegods
   - @blknoiz06
   - @coopahtroopa

## Done! 🎉

FomoFaster is now running. When traders make moves:
1. FOMO app shows notification
2. Android app intercepts it
3. Backend sends to Telegram
4. You see it instantly in your channel!

## Monitoring

**View Android logs:**
```cmd
adb logcat | findstr FomoListener
```

**View backend logs:**
Just watch the terminal where you ran `dotnet run`

## Next Steps

1. ✅ Keep backend running 24/7 → Deploy to VPS (see [DEPLOYMENT.md](DEPLOYMENT.md))
2. ✅ Add database for history → Use Entity Framework Core
3. ✅ Add filtering → Filter by specific traders
4. ✅ Multiple channels → Support different trader groups

## Troubleshooting

**"Listener not enabled"**
→ Enable permission in Android Settings

**"Connection failed"**
→ Check backend is running on port 8000
→ Windows Firewall allows port 8000

**"No notifications appearing"**
→ Check FOMO app is logged in
→ Check you're following traders
→ View logs: `adb logcat | findstr FomoListener`

**"Telegram not receiving messages"**
→ Verify bot token is correct
→ Verify channel ID includes minus sign
→ Bot must be administrator in channel

## Support Files

- **Full deployment guide:** [DEPLOYMENT.md](DEPLOYMENT.md)
- **Android app details:** [android-listener/README.md](android-listener/README.md)
- **Backend details:** [telegram-bot/README.md](telegram-bot/README.md)
- **Notification bot verification:** [NOTIFICATION-BOT-VERIFICATION.md](NOTIFICATION-BOT-VERIFICATION.md)

---

**Estimated total time:** 25-30 minutes

**Cost:** $0 for development, ~$5/month for VPS in production
