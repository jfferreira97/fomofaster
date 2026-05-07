package com.fomofaster.listener;

public class NotificationLogEntry {
    private final String timestamp;
    private final String status;
    private final String notificationText;
    private final String response;
    private final String fcmKey;
    private final long receivedAtMs;
    private final int attempt;

    public NotificationLogEntry(String timestamp, String status, String notificationText, String response, String fcmKey, long receivedAtMs, int attempt) {
        this.timestamp = timestamp;
        this.status = status;
        this.notificationText = notificationText;
        this.response = response;
        this.fcmKey = fcmKey;
        this.receivedAtMs = receivedAtMs;
        this.attempt = attempt;
    }

    public String getTimestamp() { return timestamp; }
    public String getStatus() { return status; }
    public String getNotificationText() { return notificationText; }
    public String getResponse() { return response; }
    public String getFcmKey() { return fcmKey; }
    public long getReceivedAtMs() { return receivedAtMs; }
    public int getAttempt() { return attempt; }
}
