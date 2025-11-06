package com.fomofaster.listener;

import android.annotation.SuppressLint;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

import org.json.JSONObject;

import java.io.IOException;

import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;

public class MainActivity extends AppCompatActivity {
    private static final String TAG = "MainActivity";
    private static final String PREFS_NAME = "FomoFasterPrefs";
    private static final String BACKEND_URL_KEY = "backend_url";

    private EditText backendUrlInput;
    private Button saveButton;
    private Button enableListenerButton;
    private Button enableAccessibilityButton;
    private Button testButton;
    private Button testClipboardButton;
    private Button clearLogButton;
    private TextView statusText;
    private TextView instructionsText;
    private TextView clipboardResultText;
    private LinearLayout logEntriesContainer;

    private OkHttpClient httpClient;
    private List<NotificationLogEntry> logEntries;
    private SimpleDateFormat dateFormat;
    private BroadcastReceiver logReceiver;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        // Initialize HTTP client
        httpClient = new OkHttpClient();

        // Initialize log
        logEntries = new ArrayList<>();
        dateFormat = new SimpleDateFormat("MMM dd HH:mm:ss", Locale.US);

        // Find views
        backendUrlInput = findViewById(R.id.backend_url_input);
        saveButton = findViewById(R.id.save_button);
        enableListenerButton = findViewById(R.id.enable_listener_button);
        enableAccessibilityButton = findViewById(R.id.enable_accessibility_button);
        testButton = findViewById(R.id.test_button);
        testClipboardButton = findViewById(R.id.test_clipboard_button);
        clearLogButton = findViewById(R.id.clear_log_button);
        statusText = findViewById(R.id.status_text);
        instructionsText = findViewById(R.id.instructions_text);
        clipboardResultText = findViewById(R.id.clipboard_result_text);
        logEntriesContainer = findViewById(R.id.log_entries_container);

        // Load saved backend URL
        loadBackendUrl();

        // Load saved log entries from persistent storage (after views are initialized)
        loadSavedLogEntries();

        // Set up button listeners
        saveButton.setOnClickListener(v -> saveBackendUrl());
        enableListenerButton.setOnClickListener(v -> openNotificationSettings());
        enableAccessibilityButton.setOnClickListener(v -> openAccessibilitySettings());
        testButton.setOnClickListener(v -> testConnection());
        testClipboardButton.setOnClickListener(v -> testClipboard());
        clearLogButton.setOnClickListener(v -> clearLog());

        // Set up broadcast receiver for log updates
        setupLogReceiver();

        // Update status
        updateListenerStatus();
    }

    @SuppressLint("UnspecifiedRegisterReceiverFlag")
    @Override
    protected void onResume() {
        super.onResume();
        // Update status when returning from settings
        updateListenerStatus();

        // Re-register receiver
        if (logReceiver != null) {
            IntentFilter filter = new IntentFilter(FomoNotificationListener.LOG_BROADCAST_ACTION);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                registerReceiver(logReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
            } else {
                registerReceiver(logReceiver, filter);
            }
        }
    }

    @Override
    protected void onPause() {
        super.onPause();
        // Unregister receiver to avoid memory leaks
        if (logReceiver != null) {
            try {
                unregisterReceiver(logReceiver);
            } catch (IllegalArgumentException e) {
                // Receiver was not registered
            }
        }
    }

    private void loadBackendUrl() {
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        String url = prefs.getString(BACKEND_URL_KEY, "http://10.0.2.2:8000");
        backendUrlInput.setText(url);
    }

    private void saveBackendUrl() {
        String url = backendUrlInput.getText().toString().trim();

        if (url.isEmpty()) {
            Toast.makeText(this, "Please enter a backend URL", Toast.LENGTH_SHORT).show();
            return;
        }

        // Save to preferences
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString(BACKEND_URL_KEY, url).apply();

        Toast.makeText(this, R.string.config_saved, Toast.LENGTH_SHORT).show();
        Log.d(TAG, "Backend URL saved: " + url);
    }

    private void openNotificationSettings() {
        // Open notification listener settings
        Intent intent = new Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS);
        startActivity(intent);
        Toast.makeText(this, "Enable 'FomoFaster Listener' in the list", Toast.LENGTH_LONG).show();
    }

    private void openAccessibilitySettings() {
        // Open accessibility settings
        Intent intent = new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS);
        startActivity(intent);
        Toast.makeText(this, "Enable 'FomoFaster Listener' under Downloaded Services", Toast.LENGTH_LONG).show();
    }

    private void updateListenerStatus() {
        if (isNotificationListenerEnabled()) {
            statusText.setText(R.string.status_listener_enabled);
            statusText.setTextColor(getResources().getColor(android.R.color.holo_green_dark));
        } else {
            statusText.setText(R.string.status_listener_disabled);
            statusText.setTextColor(getResources().getColor(android.R.color.holo_red_dark));
        }
    }

    private boolean isNotificationListenerEnabled() {
        String pkgName = getPackageName();
        final String flat = Settings.Secure.getString(getContentResolver(), "enabled_notification_listeners");

        if (!TextUtils.isEmpty(flat)) {
            final String[] names = flat.split(":");
            for (String name : names) {
                final ComponentName cn = ComponentName.unflattenFromString(name);
                if (cn != null) {
                    if (TextUtils.equals(pkgName, cn.getPackageName())) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void testConnection() {
        String url = backendUrlInput.getText().toString().trim();

        if (url.isEmpty()) {
            Toast.makeText(this, "Please enter a backend URL first", Toast.LENGTH_SHORT).show();
            return;
        }

        // Show testing message
        Toast.makeText(this, "Testing connection...", Toast.LENGTH_SHORT).show();
        testButton.setEnabled(false);

        // Build test payload
        try {
            JSONObject json = new JSONObject();
            json.put("app", "fomo");
            json.put("title", "TEST");
            json.put("text", "This is a test notification from FomoFaster Listener");
            json.put("contractAddress", "0xTEST");
            json.put("timestamp", System.currentTimeMillis());

            String jsonString = json.toString();
            Log.d(TAG, "Sending test request: " + jsonString);

            // Build HTTP request
            MediaType JSON_TYPE = MediaType.get("application/json; charset=utf-8");
            RequestBody body = RequestBody.create(jsonString, JSON_TYPE);

            String endpoint = url.endsWith("/")
                    ? url + "api/notifications"
                    : url + "/api/notifications";

            Request request = new Request.Builder()
                    .url(endpoint)
                    .post(body)
                    .build();

            // Send async request
            httpClient.newCall(request).enqueue(new Callback() {
                @Override
                public void onFailure(Call call, IOException e) {
                    Log.e(TAG, "Test connection failed", e);
                    runOnUiThread(() -> {
                        Toast.makeText(MainActivity.this, R.string.test_failed, Toast.LENGTH_LONG).show();
                        testButton.setEnabled(true);
                    });
                }

                @Override
                public void onResponse(Call call, Response response) throws IOException {
                    final int code = response.code();
                    response.close();

                    runOnUiThread(() -> {
                        if (code >= 200 && code < 300) {
                            Toast.makeText(MainActivity.this, R.string.test_success, Toast.LENGTH_SHORT).show();
                        } else {
                            Toast.makeText(MainActivity.this,
                                    "Server responded with code: " + code,
                                    Toast.LENGTH_LONG).show();
                        }
                        testButton.setEnabled(true);
                    });

                    Log.d(TAG, "Test response code: " + code);
                }
            });

        } catch (Exception e) {
            Log.e(TAG, "Error building test request", e);
            Toast.makeText(this, R.string.test_failed, Toast.LENGTH_SHORT).show();
            testButton.setEnabled(true);
        }
    }

    private void testClipboard() {
        android.content.ClipboardManager clipboardManager =
            (android.content.ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);

        if (clipboardManager == null || !clipboardManager.hasPrimaryClip()) {
            clipboardResultText.setText("Clipboard is empty");
            Toast.makeText(this, "Clipboard is empty", Toast.LENGTH_SHORT).show();
            return;
        }

        android.content.ClipData clipData = clipboardManager.getPrimaryClip();
        if (clipData == null || clipData.getItemCount() == 0) {
            clipboardResultText.setText("Clipboard is empty");
            Toast.makeText(this, "Clipboard is empty", Toast.LENGTH_SHORT).show();
            return;
        }

        android.content.ClipData.Item item = clipData.getItemAt(0);
        CharSequence text = item.getText();

        if (text != null && !text.toString().isEmpty()) {
            String clipboardText = text.toString();
            clipboardResultText.setText(clipboardText);
            Toast.makeText(this, "Clipboard read successfully", Toast.LENGTH_SHORT).show();
            Log.d(TAG, "Clipboard content: " + clipboardText);
        } else {
            clipboardResultText.setText("Clipboard contains no text");
            Toast.makeText(this, "Clipboard contains no text", Toast.LENGTH_SHORT).show();
        }
    }

    @SuppressWarnings("UnspecifiedRegisterReceiverFlag")
    private void setupLogReceiver() {
        logReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                String status = intent.getStringExtra(FomoNotificationListener.EXTRA_STATUS);
                String notificationText = intent.getStringExtra(FomoNotificationListener.EXTRA_NOTIFICATION_TEXT);
                String contractAddress = intent.getStringExtra(FomoNotificationListener.EXTRA_CONTRACT_ADDRESS);
                String extractionStatus = intent.getStringExtra(FomoNotificationListener.EXTRA_EXTRACTION_STATUS);
                String response = intent.getStringExtra(FomoNotificationListener.EXTRA_RESPONSE);

                Log.d(TAG, "Received log broadcast: " + status);
                addLogEntryToUI(status, notificationText, contractAddress, extractionStatus, response);
            }
        };

        IntentFilter filter = new IntentFilter(FomoNotificationListener.LOG_BROADCAST_ACTION);

        // Register receiver with appropriate flags for Android 13+
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(logReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(logReceiver, filter);
        }
    }

    private void loadSavedLogEntries() {
        try {
            SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            String logsJson = prefs.getString("notification_logs", "[]");

            org.json.JSONArray logsArray = new org.json.JSONArray(logsJson);

            for (int i = 0; i < logsArray.length(); i++) {
                org.json.JSONObject logJson = logsArray.getJSONObject(i);
                NotificationLogEntry entry = NotificationLogEntry.fromJSON(logJson);
                logEntries.add(entry);
            }

            Log.d(TAG, "Loaded " + logEntries.size() + " log entries from persistent storage");
            refreshLogDisplay();

        } catch (Exception e) {
            Log.e(TAG, "Error loading saved log entries", e);
        }
    }

    private void clearLog() {
        logEntries.clear();

        // Also clear from persistent storage
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString("notification_logs", "[]").apply();

        refreshLogDisplay();
        Toast.makeText(this, "Log cleared", Toast.LENGTH_SHORT).show();
    }

    private void addLogEntryToUI(String status, String notificationText, String contractAddress, String extractionStatus, String response) {
        String timestamp = dateFormat.format(new Date());
        NotificationLogEntry entry = new NotificationLogEntry(timestamp, status, notificationText, contractAddress, extractionStatus, response);
        logEntries.add(0, entry); // Add to beginning (most recent first)

        // Limit to 50 entries
        if (logEntries.size() > 50) {
            logEntries.remove(logEntries.size() - 1);
        }

        refreshLogDisplay();
    }

    private void refreshLogDisplay() {
        runOnUiThread(() -> {
            logEntriesContainer.removeAllViews();

            if (logEntries.isEmpty()) {
                TextView emptyView = new TextView(this);
                emptyView.setText("No notifications logged yet");
                emptyView.setTextSize(12);
                emptyView.setPadding(8, 8, 8, 8);
                emptyView.setGravity(android.view.Gravity.CENTER);
                emptyView.setTextColor(0xFF999999);
                logEntriesContainer.addView(emptyView);
                return;
            }

            for (NotificationLogEntry entry : logEntries) {
                LinearLayout row = new LinearLayout(this);
                row.setOrientation(LinearLayout.HORIZONTAL);
                row.setPadding(8, 8, 8, 8);

                // Alternate row colors
                int bgColor = (logEntries.indexOf(entry) % 2 == 0) ? 0xFFF5F5F5 : 0xFFFFFFFF;
                row.setBackgroundColor(bgColor);

                // Timestamp
                TextView timestampView = new TextView(this);
                timestampView.setText(entry.getTimestamp());
                timestampView.setTextSize(11);
                timestampView.setWidth(dpToPx(150));
                timestampView.setPadding(0, 0, dpToPx(8), 0);
                row.addView(timestampView);

                // Status
                TextView statusView = new TextView(this);
                statusView.setText(entry.getStatus());
                statusView.setTextSize(11);
                statusView.setWidth(dpToPx(100));
                statusView.setPadding(0, 0, dpToPx(8), 0);

                // Color code status
                if (entry.getStatus().contains("SUCCESS") || entry.getStatus().contains("200")) {
                    statusView.setTextColor(0xFF4CAF50); // Green
                } else if (entry.getStatus().contains("FAIL") || entry.getStatus().contains("ERROR")) {
                    statusView.setTextColor(0xFFF44336); // Red
                } else {
                    statusView.setTextColor(0xFFFF9800); // Orange
                }
                row.addView(statusView);

                // Notification Text
                TextView notificationView = new TextView(this);
                notificationView.setText(entry.getNotificationText());
                notificationView.setTextSize(11);
                notificationView.setWidth(dpToPx(250));
                notificationView.setPadding(0, 0, dpToPx(8), 0);
                notificationView.setMaxLines(2);
                notificationView.setEllipsize(android.text.TextUtils.TruncateAt.END);
                row.addView(notificationView);

                // Contract Address
                TextView contractView = new TextView(this);
                String contractAddr = entry.getContractAddress();
                if (contractAddr == null || contractAddr.isEmpty()) {
                    contractView.setText("(none)");
                    contractView.setTextColor(0xFF999999);
                } else {
                    contractView.setText(contractAddr);
                }
                contractView.setTextSize(11);
                contractView.setWidth(dpToPx(150));
                contractView.setPadding(0, 0, dpToPx(8), 0);
                contractView.setMaxLines(1);
                contractView.setEllipsize(android.text.TextUtils.TruncateAt.MIDDLE);
                row.addView(contractView);

                // Extraction Status
                TextView extractionView = new TextView(this);
                String extractStatus = entry.getExtractionStatus();
                if (extractStatus == null || extractStatus.isEmpty()) {
                    extractionView.setText("-");
                } else {
                    extractionView.setText(extractStatus);
                    // Color code extraction status
                    if (extractStatus.contains("Success")) {
                        extractionView.setTextColor(0xFF4CAF50); // Green
                    } else if (extractStatus.contains("Error")) {
                        extractionView.setTextColor(0xFFF44336); // Red
                    } else if (extractStatus.contains("Warning")) {
                        extractionView.setTextColor(0xFFFF9800); // Orange
                    }
                }
                extractionView.setTextSize(11);
                extractionView.setWidth(dpToPx(180));
                extractionView.setPadding(0, 0, dpToPx(8), 0);
                extractionView.setMaxLines(2);
                extractionView.setEllipsize(android.text.TextUtils.TruncateAt.END);
                row.addView(extractionView);

                // Response
                TextView responseView = new TextView(this);
                responseView.setText(entry.getResponse());
                responseView.setTextSize(11);
                responseView.setWidth(dpToPx(200));
                responseView.setMaxLines(2);
                responseView.setEllipsize(android.text.TextUtils.TruncateAt.END);
                row.addView(responseView);

                logEntriesContainer.addView(row);
            }
        });
    }

    private int dpToPx(int dp) {
        float density = getResources().getDisplayMetrics().density;
        return Math.round(dp * density);
    }
}
