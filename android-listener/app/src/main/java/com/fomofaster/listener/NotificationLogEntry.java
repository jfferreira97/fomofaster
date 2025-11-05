package com.fomofaster.listener;

public class NotificationLogEntry {
    private final String timestamp;
    private final String status;
    private final String message;
    private final String response;

    public NotificationLogEntry(String timestamp, String status, String message, String response) {
        this.timestamp = timestamp;
        this.status = status;
        this.message = message;
        this.response = response;
    }

    public String getTimestamp() {
        return timestamp;
    }

    public String getStatus() {
        return status;
    }

    public String getMessage() {
        return message;
    }

    public String getResponse() {
        return response;
    }
}
