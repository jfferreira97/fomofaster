# FomoFaster

Captures FOMO app notifications → Sends to Telegram instantly.

## Quick Start

See **[QUICKSTART.md](QUICKSTART.md)** (30 min setup)

## What It Does

```
FOMO App (trader buys token)
         ↓
Android Listener (intercepts notification)
         ↓
Telegram Bot (C# server - receives POST + sends to Telegram)
         ↓
Your Telegram Channel (instant notification)
```

## Components

**1. android-listener/** - Android app that intercepts FOMO notifications
**2. telegram-bot/** - C# server (REST API + Telegram sender in ONE app)

## Tech Stack

- Android: Java + NotificationListenerService
- Server: C# / ASP.NET Core 8.0
- Telegram: Telegram.Bot library

## Key Point

The `telegram-bot` folder is ONE C# app that:
1. Receives HTTP POST from Android on `/api/notifications`
2. Sends formatted messages to Telegram

It's not two separate things.

## Documentation

- **[QUICKSTART.md](QUICKSTART.md)** - Setup guide
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Production deployment
- **[android-listener/README.md](android-listener/README.md)** - Android details
- **[telegram-bot/README.md](telegram-bot/README.md)** - Backend details

## Quick Commands

**Build Android:**
```cmd
cd android-listener
gradlew.bat assembleDebug
adb install app\build\outputs\apk\debug\app-debug.apk
```

**Run server:**
```cmd
cd telegram-bot\FomoFaster.Backend
dotnet run
```

## Cost

- Development: Free
- Production: ~$5/month (VPS)

## Status

✅ Complete and working
