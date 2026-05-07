package com.fomofaster.listener;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

import java.util.ArrayList;
import java.util.List;

public class NotificationLogDatabase extends SQLiteOpenHelper {
    private static final String DB_NAME = "fomo_listener_logs.db";
    private static final int DB_VERSION = 1;

    static final String TABLE = "notification_log";
    static final String COL_ID = "id";
    static final String COL_RECEIVED_AT_MS = "received_at_ms";
    static final String COL_FCM_KEY = "fcm_key";
    static final String COL_MESSAGE = "message";
    static final String COL_STATUS = "status";
    static final String COL_RESPONSE = "response";
    static final String COL_ATTEMPT = "attempt";
    static final String COL_CREATED_AT = "created_at";

    private static NotificationLogDatabase instance;

    public static synchronized NotificationLogDatabase getInstance(Context ctx) {
        if (instance == null) {
            instance = new NotificationLogDatabase(ctx.getApplicationContext());
        }
        return instance;
    }

    private NotificationLogDatabase(Context context) {
        super(context, DB_NAME, null, DB_VERSION);
    }

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE " + TABLE + " (" +
            COL_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
            COL_RECEIVED_AT_MS + " INTEGER NOT NULL, " +
            COL_FCM_KEY + " TEXT, " +
            COL_MESSAGE + " TEXT NOT NULL, " +
            COL_STATUS + " TEXT NOT NULL, " +
            COL_RESPONSE + " TEXT, " +
            COL_ATTEMPT + " INTEGER DEFAULT 1, " +
            COL_CREATED_AT + " TEXT NOT NULL" +
        ")");
        db.execSQL("CREATE INDEX idx_received_at ON " + TABLE + "(" + COL_RECEIVED_AT_MS + ")");
        db.execSQL("CREATE INDEX idx_fcm_key ON " + TABLE + "(" + COL_FCM_KEY + ")");
    }

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        db.execSQL("DROP TABLE IF EXISTS " + TABLE);
        onCreate(db);
    }

    public void insert(long receivedAtMs, String fcmKey, String message, String status, String response, int attempt, String createdAt) {
        ContentValues values = new ContentValues();
        values.put(COL_RECEIVED_AT_MS, receivedAtMs);
        values.put(COL_FCM_KEY, fcmKey);
        values.put(COL_MESSAGE, message);
        values.put(COL_STATUS, status);
        values.put(COL_RESPONSE, response);
        values.put(COL_ATTEMPT, attempt);
        values.put(COL_CREATED_AT, createdAt);
        getWritableDatabase().insert(TABLE, null, values);
    }

    public List<NotificationLogEntry> getRecent(int limit) {
        List<NotificationLogEntry> entries = new ArrayList<>();
        Cursor cursor = getReadableDatabase().query(
            TABLE, null, null, null, null, null,
            COL_ID + " DESC", String.valueOf(limit)
        );
        while (cursor.moveToNext()) {
            entries.add(new NotificationLogEntry(
                cursor.getString(cursor.getColumnIndexOrThrow(COL_CREATED_AT)),
                cursor.getString(cursor.getColumnIndexOrThrow(COL_STATUS)),
                cursor.getString(cursor.getColumnIndexOrThrow(COL_MESSAGE)),
                cursor.getString(cursor.getColumnIndexOrThrow(COL_RESPONSE)),
                cursor.getString(cursor.getColumnIndexOrThrow(COL_FCM_KEY)),
                cursor.getLong(cursor.getColumnIndexOrThrow(COL_RECEIVED_AT_MS)),
                cursor.getInt(cursor.getColumnIndexOrThrow(COL_ATTEMPT))
            ));
        }
        cursor.close();
        return entries;
    }

    public void clearAll() {
        getWritableDatabase().delete(TABLE, null, null);
    }
}
