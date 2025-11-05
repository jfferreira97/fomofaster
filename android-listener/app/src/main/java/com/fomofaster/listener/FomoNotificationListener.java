package com.fomofaster.listener;

import android.annotation.SuppressLint;
import android.app.Notification;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
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
    public static final String EXTRA_CONTRACT_ADDRESS = "contract_address";
    public static final String EXTRA_EXTRACTION_STATUS = "extraction_status";
    public static final String EXTRA_RESPONSE = "response";

    public static final String ACTION_COPY_COMPLETED = "com.fomofaster.listener.COPY_COMPLETED";
    public static final String ACTION_COPY_FAILED = "com.fomofaster.listener.COPY_FAILED";

    private static final int WAIT_FOR_APP_OPEN_MS = 1500;  // 1.5 seconds
    private static final int TIMEOUT_MS = 2000;  // 2 seconds total timeout

    private OkHttpClient httpClient;
    private ClipboardManager clipboardManager;
    private String backendUrl;
    private Handler handler;
    private BroadcastReceiver copyResultReceiver;

    // Track current notification being processed
    private String currentTitle = "";
    private String currentText = "";
    private long currentTimestamp = 0;
    private String currentExtractionStatus = "";
    private Runnable timeoutRunnable = null;

    @SuppressLint("UnspecifiedRegisterReceiverFlag")
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

        // Initialize handler for delays
        handler = new Handler(Looper.getMainLooper());

        // Load backend URL from preferences
        loadBackendUrl();

        // Register receiver for copy completion/failure events
        copyResultReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                String action = intent.getAction();
                if (ACTION_COPY_COMPLETED.equals(action)) {
                    Log.d(TAG, "Copy button click completed");
                    onCopyCompleted();
                } else if (ACTION_COPY_FAILED.equals(action)) {
                    Log.e(TAG, "Copy button click failed");
                    onCopyFailed();
                }
            }
        };

        IntentFilter filter = new IntentFilter();
        filter.addAction(ACTION_COPY_COMPLETED);
        filter.addAction(ACTION_COPY_FAILED);
        registerReceiver(copyResultReceiver, filter);
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

        // Store current notification data
        currentTitle = title;
        currentText = text;
        currentTimestamp = timestamp;
        currentExtractionStatus = "";

        // Start the automation flow
        startContractAddressExtraction(notification);
    }

    private void startContractAddressExtraction(Notification notification) {
        // Cancel any pending timeout
        if (timeoutRunnable != null) {
            handler.removeCallbacks(timeoutRunnable);
        }

        // Clear clipboard to detect new content
        clearClipboard();

        // Click the notification to open FOMO app
        PendingIntent contentIntent = notification.contentIntent;
        if (contentIntent != null) {
            try {
                Log.d(TAG, "Clicking notification to open FOMO app...");
                contentIntent.send();

                // Wait 1.5 seconds for app to open, then trigger copy button click
                handler.postDelayed(() -> {
                    Log.d(TAG, "Requesting copy button click via accessibility service...");
                    FomoAccessibilityService.requestCopyButtonClick(this);
                }, WAIT_FOR_APP_OPEN_MS);

                // Set timeout to send request without contract address if needed
                timeoutRunnable = () -> {
                    Log.w(TAG, "Timeout reached, sending request without contract address");
                    onCopyFailed();
                };
                handler.postDelayed(timeoutRunnable, TIMEOUT_MS);

            } catch (PendingIntent.CanceledException e) {
                Log.e(TAG, "Failed to click notification", e);
                currentExtractionStatus = "Error: Failed to open notification";
                sendToBackend(currentTitle, currentText, "", currentTimestamp);
            }
        } else {
            Log.e(TAG, "No content intent in notification");
            currentExtractionStatus = "Error: No content intent in notification";
            sendToBackend(currentTitle, currentText, "", currentTimestamp);
        }
    }

    private void onCopyCompleted() {
        // Cancel timeout
        if (timeoutRunnable != null) {
            handler.removeCallbacks(timeoutRunnable);
            timeoutRunnable = null;
        }

        // Read clipboard
        String contractAddress = readClipboard();
        Log.d(TAG, "Contract address from clipboard: " + contractAddress);

        if (contractAddress.isEmpty()) {
            currentExtractionStatus = "Warning: Clipboard was empty";
        } else {
            currentExtractionStatus = "Success";
        }

        // Send to backend
        sendToBackend(currentTitle, currentText, contractAddress, currentTimestamp);
    }

    private void onCopyFailed() {
        // Cancel timeout
        if (timeoutRunnable != null) {
            handler.removeCallbacks(timeoutRunnable);
            timeoutRunnable = null;
        }

        currentExtractionStatus = "Error: Copy button click failed or timed out";

        // Send to backend without contract address
        sendToBackend(currentTitle, currentText, "", currentTimestamp);
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
                    broadcastLogEntry("FAILED", message, contractAddress, currentExtractionStatus, "Error: " + e.getMessage());
                }

                @Override
                public void onResponse(Call call, Response response) throws IOException {
                    String responseBody = "";
                    if (response.body() != null) {
                        responseBody = response.body().string();
                    }

                    if (response.isSuccessful()) {
                        Log.d(TAG, "Successfully sent to backend: " + response.code());
                        broadcastLogEntry("SUCCESS (" + response.code() + ")", message, contractAddress, currentExtractionStatus, responseBody);
                    } else {
                        Log.e(TAG, "Backend responded with error: " + response.code());
                        broadcastLogEntry("ERROR (" + response.code() + ")", message, contractAddress, currentExtractionStatus, responseBody);
                    }
                    response.close();
                }
            });

        } catch (Exception e) {
            Log.e(TAG, "Error sending to backend", e);
            broadcastLogEntry("EXCEPTION", title + " " + text, contractAddress, currentExtractionStatus, "Error: " + e.getMessage());
        }
    }

    private void broadcastLogEntry(String status, String notificationText, String contractAddress, String extractionStatus, String response) {
        // Save to persistent storage
        saveLogEntryToPersistentStorage(status, notificationText, contractAddress, extractionStatus, response);

        // Send broadcast for real-time UI updates (if MainActivity is visible)
        Intent intent = new Intent(LOG_BROADCAST_ACTION);
        intent.putExtra(EXTRA_STATUS, status);
        intent.putExtra(EXTRA_NOTIFICATION_TEXT, notificationText);
        intent.putExtra(EXTRA_CONTRACT_ADDRESS, contractAddress);
        intent.putExtra(EXTRA_EXTRACTION_STATUS, extractionStatus);
        intent.putExtra(EXTRA_RESPONSE, response);
        sendBroadcast(intent);
        Log.d(TAG, "Broadcast log entry: " + status);
    }

    private void saveLogEntryToPersistentStorage(String status, String notificationText, String contractAddress, String extractionStatus, String response) {
        try {
            SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            String logsJson = prefs.getString("notification_logs", "[]");

            org.json.JSONArray logsArray = new org.json.JSONArray(logsJson);

            // Create new log entry JSON
            org.json.JSONObject logEntry = new org.json.JSONObject();
            logEntry.put("timestamp", new java.text.SimpleDateFormat("MMM dd HH:mm:ss", java.util.Locale.US).format(new java.util.Date()));
            logEntry.put("status", status);
            logEntry.put("notificationText", notificationText);
            logEntry.put("contractAddress", contractAddress);
            logEntry.put("extractionStatus", extractionStatus);
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
        if (copyResultReceiver != null) {
            unregisterReceiver(copyResultReceiver);
        }
        if (timeoutRunnable != null) {
            handler.removeCallbacks(timeoutRunnable);
        }
        Log.d(TAG, "FomoNotificationListener service destroyed");
    }
}
