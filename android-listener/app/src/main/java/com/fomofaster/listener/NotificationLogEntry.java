package com.fomofaster.listener;

import org.json.JSONException;
import org.json.JSONObject;

public class NotificationLogEntry {
    private final String timestamp;
    private final String status;
    private final String notificationText;
    private final String contractAddress;
    private final String extractionStatus;
    private final String response;

    public NotificationLogEntry(String timestamp, String status, String notificationText,
                                String contractAddress, String extractionStatus, String response) {
        this.timestamp = timestamp;
        this.status = status;
        this.notificationText = notificationText;
        this.contractAddress = contractAddress;
        this.extractionStatus = extractionStatus;
        this.response = response;
    }

    public String getTimestamp() {
        return timestamp;
    }

    public String getStatus() {
        return status;
    }

    public String getNotificationText() {
        return notificationText;
    }

    public String getContractAddress() {
        return contractAddress;
    }

    public String getExtractionStatus() {
        return extractionStatus;
    }

    public String getResponse() {
        return response;
    }

    // Convert to JSON for storage
    public JSONObject toJSON() throws JSONException {
        JSONObject json = new JSONObject();
        json.put("timestamp", timestamp);
        json.put("status", status);
        json.put("notificationText", notificationText);
        json.put("contractAddress", contractAddress);
        json.put("extractionStatus", extractionStatus);
        json.put("response", response);
        return json;
    }

    // Create from JSON
    public static NotificationLogEntry fromJSON(JSONObject json) throws JSONException {
        return new NotificationLogEntry(
            json.getString("timestamp"),
            json.getString("status"),
            json.getString("notificationText"),
            json.getString("contractAddress"),
            json.getString("extractionStatus"),
            json.getString("response")
        );
    }
}
