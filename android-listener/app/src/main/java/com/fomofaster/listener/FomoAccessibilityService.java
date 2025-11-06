package com.fomofaster.listener;

import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.GestureDescription;
import android.annotation.SuppressLint;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Path;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.accessibility.AccessibilityEvent;

public class FomoAccessibilityService extends AccessibilityService {
    private static final String TAG = "FomoAccessibility";
    private static final String FOMO_PACKAGE = "family.fomo.app";
    private static final String ACTION_CLICK_COPY_BUTTON = "com.fomofaster.listener.CLICK_COPY_BUTTON";

    // Coordinates for the copy button (top left area)
    private static final int COPY_BUTTON_X = 216;  // Center of [162, 271]
    private static final int COPY_BUTTON_Y = 81;   // Center of [50, 113]

    private Handler handler;
    private BroadcastReceiver clickReceiver;

    @SuppressLint("UnspecifiedRegisterReceiverFlag")
    @Override
    public void onCreate() {
        super.onCreate();
        Log.d(TAG, "FomoAccessibilityService created");

        handler = new Handler(Looper.getMainLooper());

        // Register broadcast receiver to listen for click requests
        clickReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (ACTION_CLICK_COPY_BUTTON.equals(intent.getAction())) {
                    Log.d(TAG, "Received click copy button request");
                    performCopyButtonClick();
                }
            }
        };

        IntentFilter filter = new IntentFilter(ACTION_CLICK_COPY_BUTTON);
        registerReceiver(clickReceiver, filter);
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        // We monitor window state changes to detect when FOMO app opens
        if (event.getEventType() == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) {
            String packageName = event.getPackageName() != null ? event.getPackageName().toString() : "";

            if (FOMO_PACKAGE.equals(packageName)) {
                Log.d(TAG, "FOMO app window detected");
            }
        }
    }

    private void performCopyButtonClick() {
        Log.d(TAG, "Attempting to click copy button at coordinates [" + COPY_BUTTON_X + ", " + COPY_BUTTON_Y + "]");

        // Create a gesture path for clicking
        Path clickPath = new Path();
        clickPath.moveTo(COPY_BUTTON_X, COPY_BUTTON_Y);

        // Create gesture description (tap at coordinates)
        GestureDescription.StrokeDescription strokeDescription =
                null;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            strokeDescription = new GestureDescription.StrokeDescription(clickPath, 0, 50);
        }

        GestureDescription.Builder gestureBuilder = null;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            gestureBuilder = new GestureDescription.Builder();
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            gestureBuilder.addStroke(strokeDescription);
        }

        // Perform the gesture
        boolean dispatched = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            dispatched = dispatchGesture(
                gestureBuilder.build(),
                new GestureResultCallback() {
                    @Override
                    public void onCompleted(GestureDescription gestureDescription) {
                        super.onCompleted(gestureDescription);
                        Log.d(TAG, "Click gesture completed successfully");

                        // Notify completion to read clipboard
                        Intent intent = new Intent(FomoNotificationListener.ACTION_COPY_COMPLETED);
                        sendBroadcast(intent);
                    }

                    @Override
                    public void onCancelled(GestureDescription gestureDescription) {
                        super.onCancelled(gestureDescription);
                        Log.e(TAG, "Click gesture was cancelled");

                        // Notify failure
                        Intent intent = new Intent(FomoNotificationListener.ACTION_COPY_FAILED);
                        sendBroadcast(intent);
                    }
                },
                null
            );
        }

        if (!dispatched) {
            Log.e(TAG, "Failed to dispatch click gesture");
            Intent intent = new Intent(FomoNotificationListener.ACTION_COPY_FAILED);
            sendBroadcast(intent);
        }
    }

    @Override
    public void onInterrupt() {
        Log.d(TAG, "FomoAccessibilityService interrupted");
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        if (clickReceiver != null) {
            unregisterReceiver(clickReceiver);
        }
        Log.d(TAG, "FomoAccessibilityService destroyed");
    }

    // Static method to request a copy button click from anywhere in the app
    public static void requestCopyButtonClick(Context context) {
        Intent intent = new Intent(ACTION_CLICK_COPY_BUTTON);
        intent.setPackage(context.getPackageName());  // Make it explicit for Android 8.0+
        context.sendBroadcast(intent);
        Log.d(TAG, "Broadcast sent: " + ACTION_CLICK_COPY_BUTTON);
    }
}
