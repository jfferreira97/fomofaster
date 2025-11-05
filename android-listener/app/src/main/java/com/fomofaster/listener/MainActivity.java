package com.fomofaster.listener;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.provider.Settings;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

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
    private Button testButton;
    private TextView statusText;
    private TextView instructionsText;

    private OkHttpClient httpClient;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        // Initialize HTTP client
        httpClient = new OkHttpClient();

        // Find views
        backendUrlInput = findViewById(R.id.backend_url_input);
        saveButton = findViewById(R.id.save_button);
        enableListenerButton = findViewById(R.id.enable_listener_button);
        testButton = findViewById(R.id.test_button);
        statusText = findViewById(R.id.status_text);
        instructionsText = findViewById(R.id.instructions_text);

        // Load saved backend URL
        loadBackendUrl();

        // Set up button listeners
        saveButton.setOnClickListener(v -> saveBackendUrl());
        enableListenerButton.setOnClickListener(v -> openNotificationSettings());
        testButton.setOnClickListener(v -> testConnection());

        // Update status
        updateListenerStatus();
    }

    @Override
    protected void onResume() {
        super.onResume();
        // Update status when returning from settings
        updateListenerStatus();
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
}
