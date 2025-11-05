package com.fomofaster.listener;

import android.app.Notification;
import android.app.PendingIntent;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
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

    private OkHttpClient httpClient;
    private ClipboardManager clipboardManager;
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

        // Get clipboard manager
        clipboardManager = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);

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

        // Try to extract contract address by triggering clipboard action
        extractContractAddress(notification, title, text, timestamp);
    }

    private void extractContractAddress(Notification notification, String title, String text, long timestamp) {
        // Look for notification actions (clipboard button)
        Notification.Action[] actions = notification.actions;

        String contractAddress = "";

        if (actions != null && actions.length > 0) {
            Log.d(TAG, "Found " + actions.length + " notification action(s)");

            // Try to find and trigger clipboard action
            for (Notification.Action action : actions) {
                String actionTitle = action.title != null ? action.title.toString() : "";
                Log.d(TAG, "Action found: " + actionTitle);

                // The clipboard button might not have a title, or might be labeled
                // We'll try the first action, which is typically the clipboard in FOMO
                if (action.actionIntent != null) {
                    try {
                        // Clear clipboard first to detect new content
                        clearClipboard();

                        // Trigger the action
                        action.actionIntent.send();
                        Log.d(TAG, "Action triggered, waiting for clipboard...");

                        // Wait briefly for clipboard to update (100ms should be enough)
                        try {
                            Thread.sleep(100);
                        } catch (InterruptedException e) {
                            Log.e(TAG, "Sleep interrupted", e);
                        }

                        // Read clipboard content
                        contractAddress = readClipboard();
                        Log.d(TAG, "Clipboard content: " + contractAddress);

                        // If we got a contract address, break
                        if (!contractAddress.isEmpty() && contractAddress.startsWith("0x")) {
                            break;
                        }
                    } catch (PendingIntent.CanceledException e) {
                        Log.e(TAG, "Failed to trigger notification action", e);
                    }
                }
            }
        } else {
            Log.d(TAG, "No notification actions found");
        }

        // Send data to backend
        sendToBackend(title, text, contractAddress, timestamp);
    }

    private void clearClipboard() {
        if (clipboardManager != null) {
            ClipData clip = ClipData.newPlainText("", "");
            clipboardManager.setPrimaryClip(clip);
        }
    }

    private String readClipboard() {
        if (clipboardManager == null || !clipboardManager.hasPrimaryClip()) {
            return "";
        }

        ClipData clipData = clipboardManager.getPrimaryClip();
        if (clipData == null || clipData.getItemCount() == 0) {
            return "";
        }

        ClipData.Item item = clipData.getItemAt(0);
        CharSequence text = item.getText();

        return text != null ? text.toString().trim() : "";
    }

    private void sendToBackend(String title, String text, String contractAddress, long timestamp) {
        // Reload backend URL in case it changed
        loadBackendUrl();

        try {
            // Combine title and text into single message (exact FOMO notification format)
            String message = title + " " + text;

            // Build JSON payload - SIMPLE!
            JSONObject json = new JSONObject();
            json.put("message", message);
            json.put("contractAddress", contractAddress);

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
                }

                @Override
                public void onResponse(Call call, Response response) throws IOException {
                    if (response.isSuccessful()) {
                        Log.d(TAG, "Successfully sent to backend: " + response.code());
                    } else {
                        Log.e(TAG, "Backend responded with error: " + response.code());
                    }
                    response.close();
                }
            });

        } catch (Exception e) {
            Log.e(TAG, "Error sending to backend", e);
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
