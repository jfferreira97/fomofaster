package com.fomofaster.listener;

import android.app.Notification;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;
import android.util.Log;

import org.json.JSONObject;

import java.io.IOException;
import java.util.concurrent.TimeUnit;

import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;

public class FomoNotificationListener extends NotificationListenerService {
    private static final String TAG = "FomoListener";
    private static final String FOMO_PACKAGE_NAME = "family.fomo.app";
    private static final String PREFS_NAME = "FomoFasterPrefs";
    private static final String BACKEND_URL_KEY = "backend_url";

    public static final String LOG_BROADCAST_ACTION = "com.fomofaster.listener.LOG_ENTRY";
    public static final String EXTRA_STATUS = "status";
    public static final String EXTRA_NOTIFICATION_TEXT = "notification_text";
    public static final String EXTRA_RESPONSE = "response";

    private OkHttpClient httpClient;
    private String backendUrl;

    @Override
    public void onCreate() {
        super.onCreate();
        Log.d(TAG, "FomoNotificationListener service created");

        // Initialize HTTP client with timeouts
        httpClient = new OkHttpClient.Builder()
                .connectTimeout(10, TimeUnit.SECONDS)
                .writeTimeout(10, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .build();

        // Load backend URL from preferences
        loadBackendUrl();
    }

    private void loadBackendUrl() {
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        backendUrl = prefs.getString(BACKEND_URL_KEY, "http://10.0.2.2:8000");
        Log.d(TAG, "Backend URL loaded: " + backendUrl);
    }

    @Override
    public void onNotificationPosted(StatusBarNotification sbn) {
        String packageName = sbn.getPackageName();

        // Filter for FOMO app notifications only
        if (!FOMO_PACKAGE_NAME.equals(packageName)) {
            return;
        }

        Log.d(TAG, "FOMO notification detected!");

        // Extract notification data
        Notification notification = sbn.getNotification();
        Bundle extras = notification.extras;

        String title = extras.getString(Notification.EXTRA_TITLE, "");
        String text = extras.getCharSequence(Notification.EXTRA_TEXT, "").toString();
        long timestamp = sbn.getPostTime();

        Log.d(TAG, "Title: " + title);
        Log.d(TAG, "Text: " + text);
        Log.d(TAG, "Timestamp: " + timestamp);

        // Combine title and text into single message
        String message = title + " " + text;

        // Send to backend immediately
        sendToBackend(message, timestamp);
    }

    private void sendToBackend(String message, long timestamp) {
        // Reload backend URL in case it changed
        loadBackendUrl();

        try {
            // Build JSON payload
            JSONObject json = new JSONObject();
            json.put("message", message);

            String jsonString = json.toString();
            Log.d(TAG, "Sending to backend: " + jsonString);

            // Build HTTP request
            MediaType JSON_TYPE = MediaType.get("application/json; charset=utf-8");
            RequestBody body = RequestBody.create(jsonString, JSON_TYPE);

            String endpoint = backendUrl.endsWith("/")
                ? backendUrl + "api/notifications"
                : backendUrl + "/api/notifications";

            Request request = new Request.Builder()
                    .url(endpoint)
                    .post(body)
                    .build();

            // Send async request
            httpClient.newCall(request).enqueue(new Callback() {
                @Override
                public void onFailure(Call call, IOException e) {
                    Log.e(TAG, "Failed to send notification to backend", e);
                    broadcastLogEntry("FAILED", message, "Error: " + e.getMessage());
                }

                @Override
                public void onResponse(Call call, Response response) throws IOException {
                    String responseBody = "";
                    if (response.body() != null) {
                        responseBody = response.body().string();
                    }

                    if (response.isSuccessful()) {
                        Log.d(TAG, "Successfully sent to backend: " + response.code());
                        broadcastLogEntry("SUCCESS (" + response.code() + ")", message, responseBody);
                    } else {
                        Log.e(TAG, "Backend responded with error: " + response.code());
                        broadcastLogEntry("ERROR (" + response.code() + ")", message, responseBody);
                    }
                    response.close();
                }
            });

        } catch (Exception e) {
            Log.e(TAG, "Error sending to backend", e);
            broadcastLogEntry("EXCEPTION", message, "Error: " + e.getMessage());
        }
    }

    private void broadcastLogEntry(String status, String notificationText, String response) {
        // Save to persistent storage
        saveLogEntryToPersistentStorage(status, notificationText, response);

        // Send broadcast for real-time UI updates (if MainActivity is visible)
        Intent intent = new Intent(LOG_BROADCAST_ACTION);
        intent.putExtra(EXTRA_STATUS, status);
        intent.putExtra(EXTRA_NOTIFICATION_TEXT, notificationText);
        intent.putExtra(EXTRA_RESPONSE, response);
        sendBroadcast(intent);
        Log.d(TAG, "Broadcast log entry: " + status);
    }

    private void saveLogEntryToPersistentStorage(String status, String notificationText, String response) {
        try {
            SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            String logsJson = prefs.getString("notification_logs", "[]");

            org.json.JSONArray logsArray = new org.json.JSONArray(logsJson);

            // Create new log entry JSON
            org.json.JSONObject logEntry = new org.json.JSONObject();
            logEntry.put("timestamp", new java.text.SimpleDateFormat("MMM dd HH:mm:ss", java.util.Locale.US).format(new java.util.Date()));
            logEntry.put("status", status);
            logEntry.put("notificationText", notificationText);
            logEntry.put("response", response);

            // Add to beginning (most recent first)
            org.json.JSONArray newLogsArray = new org.json.JSONArray();
            newLogsArray.put(logEntry);

            // Copy existing entries (limit to 50)
            int limit = Math.min(logsArray.length(), 49);
            for (int i = 0; i < limit; i++) {
                newLogsArray.put(logsArray.get(i));
            }

            // Save back to SharedPreferences
            prefs.edit().putString("notification_logs", newLogsArray.toString()).apply();
            Log.d(TAG, "Saved log entry to persistent storage");

        } catch (Exception e) {
            Log.e(TAG, "Error saving log entry to persistent storage", e);
        }
    }

    @Override
    public void onNotificationRemoved(StatusBarNotification sbn) {
        // We don't need to do anything when notifications are removed
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        Log.d(TAG, "FomoNotificationListener service destroyed");
    }
}
