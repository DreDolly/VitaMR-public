package com.dredolly.vitamr

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.Uri
import android.os.Bundle
import android.provider.MediaStore
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.compose.foundation.background
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AttachFile
import androidx.compose.material.icons.filled.Bookmark
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.Send
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.fragment.app.FragmentActivity
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.draw.clip
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.BufferedReader
import java.io.File
import java.io.IOException
import java.io.InputStreamReader
import java.net.ConnectException
import java.net.HttpURLConnection
import java.net.NoRouteToHostException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import java.net.URL
import java.net.URLEncoder
import java.security.KeyStore
import java.time.OffsetDateTime
import java.time.LocalTime
import java.util.Base64
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

private const val PrefsName = "vitamr_mobile"
private const val MaxCaptureDimension = 1000
private const val RequestCameraCapture = 1001
private const val RequestPhotoPicker = 1002
private const val PersonalGeminiModel = "gemini-2.5-flash-lite"
private const val PersonalGeminiKeyAlias = "vitamr_personal_gemini_api_key"
private const val PersonalGeminiEncryptedPref = "personal_gemini_api_key_encrypted"
private const val PersonalGeminiLegacyPref = "personal_gemini_api_key"
private const val PersonalCloudCooldownPref = "personal_cloud_cooldown_until_ms"
private const val PersonalCloudCooldownSeconds = 60L
private const val PersonalThreadBriefPref = "personal_thread_brief"
private const val PersonalThreadTopicPref = "personal_thread_topic"
private const val PersonalThreadGoalPref = "personal_thread_goal"

class MainActivity : FragmentActivity() {
    private var pendingCameraUri: Uri? = null
    private var pendingCameraCallback: ((Uri?) -> Unit)? = null
    private var pendingGalleryCallback: ((Uri?) -> Unit)? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            VitaMRApp()
        }
    }

    fun openCamera(uri: Uri, onResult: (Uri?) -> Unit): String? {
        pendingCameraUri = uri
        pendingCameraCallback = onResult
        return try {
            val intent = Intent(MediaStore.ACTION_IMAGE_CAPTURE)
                .putExtra(MediaStore.EXTRA_OUTPUT, uri)
                .addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION or Intent.FLAG_GRANT_READ_URI_PERMISSION)
            startActivityForResult(intent, RequestCameraCapture)
            null
        } catch (exception: ActivityNotFoundException) {
            pendingCameraCallback = null
            "Camera app is not available on this phone."
        } catch (exception: RuntimeException) {
            pendingCameraCallback = null
            "Camera could not open: ${exception.message}"
        }
    }

    fun openPhotoLibrary(onResult: (Uri?) -> Unit): String? {
        pendingGalleryCallback = onResult
        return try {
            val intent = Intent(Intent.ACTION_PICK, MediaStore.Images.Media.EXTERNAL_CONTENT_URI)
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            startActivityForResult(intent, RequestPhotoPicker)
            null
        } catch (exception: ActivityNotFoundException) {
            pendingGalleryCallback = null
            "Photo library is not available on this phone."
        } catch (exception: RuntimeException) {
            pendingGalleryCallback = null
            "Photo library could not open: ${exception.message}"
        }
    }

    fun openDeveloperOptions(): String? {
        return try {
            val intent = Intent(android.provider.Settings.ACTION_APPLICATION_DEVELOPMENT_SETTINGS)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            startActivity(intent)
            null
        } catch (exception: ActivityNotFoundException) {
            try {
                val fallback = Intent(android.provider.Settings.ACTION_SETTINGS)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                startActivity(fallback)
                "Developer Options was not available, so Android Settings opened instead."
            } catch (fallback: RuntimeException) {
                "Could not open Android Settings: ${fallback.message}"
            }
        } catch (exception: RuntimeException) {
            "Could not open Developer Options: ${exception.message}"
        }
    }

    @Deprecated("Deprecated by Android; retained for broad device compatibility.")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)

        when (requestCode) {
            RequestCameraCapture -> {
                val callback = pendingCameraCallback
                pendingCameraCallback = null
                val uri = if (resultCode == Activity.RESULT_OK) pendingCameraUri else null
                pendingCameraUri = null
                callback?.invoke(uri)
            }
            RequestPhotoPicker -> {
                val callback = pendingGalleryCallback
                pendingGalleryCallback = null
                callback?.invoke(if (resultCode == Activity.RESULT_OK) data?.data else null)
            }
        }
    }
}

data class ChatMessage(val author: String, val text: String)

class GeminiHttpException(
    val code: Int,
    message: String
) : IllegalStateException(message)

data class ChartRow(val chartId: String, val displayName: String, val isActive: Boolean)

data class ChatResult(
    val reply: String,
    val activeChartId: String,
    val activePatientDisplayName: String,
    val packet: ChartPacket?
)

data class HealthResult(
    val message: String,
    val activeChartId: String,
    val activePatientDisplayName: String,
    val activeLifeMode: String
)

data class PersonalSummaryResult(
    val status: String,
    val updatedAt: String,
    val summary: String,
    val message: String
)

data class ChartPhotoResult(
    val status: String,
    val chartId: String,
    val patientDisplayName: String,
    val hasPhoto: Boolean,
    val contentType: String,
    val base64Data: String,
    val message: String
)

data class ChartPacket(
    val status: String,
    val chartId: String,
    val patientDisplayName: String,
    val packetType: String,
    val mode: String,
    val createdAt: String,
    val title: String,
    val body: String,
    val message: String
)

data class VitaMasteryQuest(
    val questId: String,
    val title: String,
    val stage: String,
    val status: String,
    val evidenceHint: String,
    val points: Int
)

data class DataHunterXpPop(
    val id: Long,
    val points: Int
)

data class VitaMastery(
    val status: String,
    val chartId: String,
    val patientDisplayName: String,
    val percentComplete: Int,
    val currentStage: String,
    val earnedPoints: Int,
    val possiblePoints: Int,
    val summaryLine: String,
    val dataHunterTitle: String,
    val dataHunterStage: String,
    val dataHunterPercent: Int,
    val dataHunterQuestion: String,
    val dataHunterQuestCategory: String,
    val dataHunterQuestStatus: String,
    val dataHunterQuestXp: Int,
    val dataHunterXpTotal: Int,
    val dataHunterMasterTarget: String,
    val dataHunterMasterTargetDetail: String,
    val dataHunterMasterAcceptedCount: Int,
    val dataHunterMasterPendingCount: Int,
    val dataHunterQuestControlPrompt: String,
    val dataHunterPrompt: String,
    val activeMicroQuests: List<VitaMasteryQuest>
)

data class OfflineItem(
    val localId: String,
    val createdAt: String,
    val chartId: String,
    val patientDisplayName: String,
    val kind: String,
    val note: String,
    val status: String,
    val fileUri: String = "",
    val fileName: String = "",
    val contentType: String = ""
)

data class OfflineSyncResult(
    val status: String,
    val message: String,
    val contextQuestion: String
)

data class PendingAttachment(
    val uri: Uri,
    val label: String,
    val kind: String,
    val contentType: String
)

class VitaMRClient(private val context: Context) {
    private val prefs = context.getSharedPreferences(PrefsName, Context.MODE_PRIVATE)

    var host: String
        get() = prefs.getString("host", "http://192.168.1.100:5057") ?: "http://192.168.1.100:5057"
        set(value) = prefs.edit().putString("host", value.trim().trimEnd('/')).apply()

    private var token: String
        get() = prefs.getString("token", "") ?: ""
        set(value) = prefs.edit().putString("token", value).apply()

    var biometricEnabled: Boolean
        get() = prefs.getBoolean("manager_biometric_enabled", false)
        set(value) = prefs.edit().putBoolean("manager_biometric_enabled", value).apply()

    var personalCloudEnabled: Boolean
        get() = prefs.getBoolean("personal_cloud_enabled", false)
        set(value) = prefs.edit().putBoolean("personal_cloud_enabled", value).apply()

    private var personalGeminiApiKey: String
        get() {
            val encrypted = prefs.getString(PersonalGeminiEncryptedPref, "") ?: ""
            if (encrypted.isNotBlank()) {
                return decryptPersonalGeminiKey(encrypted).orEmpty()
            }

            val legacy = prefs.getString(PersonalGeminiLegacyPref, "")?.trim().orEmpty()
            if (legacy.isNotBlank()) {
                personalGeminiApiKey = legacy
            }
            return legacy
        }
        set(value) {
            val trimmed = value.trim()
            val editor = prefs.edit().remove(PersonalGeminiLegacyPref)
            if (trimmed.isBlank()) {
                editor.remove(PersonalGeminiEncryptedPref).apply()
            } else {
                editor.putString(PersonalGeminiEncryptedPref, encryptPersonalGeminiKey(trimmed)).apply()
            }
        }

    fun isPaired(): Boolean = token.isNotBlank()

    fun hasPersonalGeminiKey(): Boolean = personalGeminiApiKey.isNotBlank()

    fun savePersonalGeminiKey(value: String) {
        personalGeminiApiKey = value
    }

    fun clearPersonalGeminiKey() {
        personalGeminiApiKey = ""
    }

    fun personalCloudLabel(): String =
        when {
            !personalCloudEnabled -> "Personal Cloud: off"
            !hasPersonalGeminiKey() -> "Personal Cloud: Gemini key needed"
            else -> "Personal Cloud: Gemini Flash-Lite"
        }

    private var personalCloudCooldownUntilMs: Long
        get() = prefs.getLong(PersonalCloudCooldownPref, 0L)
        set(value) = prefs.edit().putLong(PersonalCloudCooldownPref, value).apply()

    private fun personalCloudCooldownRemainingSeconds(): Long {
        val remainingMs = personalCloudCooldownUntilMs - System.currentTimeMillis()
        return if (remainingMs <= 0L) 0L else (remainingMs + 999L) / 1000L
    }

    private fun startPersonalCloudCooldown(seconds: Long = PersonalCloudCooldownSeconds) {
        personalCloudCooldownUntilMs = System.currentTimeMillis() + seconds * 1000L
    }

    private fun personalCloudCoolingDownReply(seconds: Long): String =
        "Saved to Personal Mode. Personal Cloud is busy right now, so I paused Gemini for about $seconds second${if (seconds == 1L) "" else "s"}. Please wait a little and try the next reply after that. I will keep syncing your saved notes to the host when available."

    var personalThreadBrief: String
        get() = prefs.getString(PersonalThreadBriefPref, "") ?: ""
        set(value) = prefs.edit().putString(PersonalThreadBriefPref, value.take(900)).apply()

    private var personalThreadTopic: String
        get() = prefs.getString(PersonalThreadTopicPref, "") ?: ""
        set(value) = prefs.edit().putString(PersonalThreadTopicPref, value.take(80)).apply()

    private var personalThreadGoal: String
        get() = prefs.getString(PersonalThreadGoalPref, "") ?: ""
        set(value) = prefs.edit().putString(PersonalThreadGoalPref, value.take(180)).apply()

    fun updatePersonalThreadBrief(userNote: String, dollyReply: String) {
        val topic = inferPersonalThreadTopic(userNote).ifBlank { personalThreadTopic.ifBlank { "Personal planning" } }
        val goal = inferPersonalThreadGoal(userNote).ifBlank { personalThreadGoal.ifBlank { userNote.take(160) } }
        personalThreadTopic = topic
        personalThreadGoal = goal
        personalThreadBrief = buildString {
            appendLine("Topic: $topic")
            appendLine("Current goal: ${goal.take(180)}")
            appendLine("Latest user point: ${userNote.trim().take(240)}")
            val conciseReply = dollyReply
                .lineSequence()
                .firstOrNull { it.isNotBlank() }
                ?.take(180)
                .orEmpty()
            if (conciseReply.isNotBlank()) {
                appendLine("Latest Dolly response: $conciseReply")
            }
        }.trim()
    }

    private fun encryptPersonalGeminiKey(value: String): String {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreatePersonalGeminiSecretKey())
        val encrypted = cipher.doFinal(value.toByteArray(Charsets.UTF_8))
        val iv = Base64.getEncoder().encodeToString(cipher.iv)
        val payload = Base64.getEncoder().encodeToString(encrypted)
        return "$iv:$payload"
    }

    private fun decryptPersonalGeminiKey(value: String): String? {
        return runCatching {
            val parts = value.split(":", limit = 2)
            if (parts.size != 2) return@runCatching null
            val iv = Base64.getDecoder().decode(parts[0])
            val payload = Base64.getDecoder().decode(parts[1])
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, getOrCreatePersonalGeminiSecretKey(), GCMParameterSpec(128, iv))
            String(cipher.doFinal(payload), Charsets.UTF_8)
        }.getOrNull()
    }

    private fun getOrCreatePersonalGeminiSecretKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(PersonalGeminiKeyAlias, null) as? SecretKey)?.let { return it }

        val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        val keySpec = KeyGenParameterSpec.Builder(
            PersonalGeminiKeyAlias,
            KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
        )
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setRandomizedEncryptionRequired(true)
            .build()
        keyGenerator.init(keySpec)
        return keyGenerator.generateKey()
    }

    var personalMemorySummary: String
        get() = prefs.getString("personal_memory_summary", "") ?: ""
        set(value) = prefs.edit().putString("personal_memory_summary", value.take(6000)).apply()

    fun wirelessDebuggingEnabled(): Boolean? {
        return runCatching {
            android.provider.Settings.Global.getInt(context.contentResolver, "adb_wifi_enabled", 0) == 1
        }.getOrNull()
    }

    suspend fun health(): HealthResult {
        if (!isOnWifi()) {
            return HealthResult(
                "Not connected: phone is not on Wi-Fi. Join the same Wi-Fi as VitaMR desktop.",
                "",
                "",
                ""
            )
        }

        return try {
            val response = getJson("/mobile/health")
            val chartId = response.optString("activeChartId")
            val patientName = response.optString("activePatientDisplayName")
            val lifeMode = normalizeLifeMode(response.optString("activeLifeMode", "Medical"))
            val message = when {
                !isPaired() -> "Connected to VitaMR desktop. Pair this phone to chat."
                patientName.isBlank() -> "Connected. Dolly is ready."
                else -> "Connected. Dolly ready: $patientName."
            }
            HealthResult(message, chartId, patientName, lifeMode)
        } catch (exception: Exception) {
            HealthResult(describeConnectionProblem(exception), "", "", "")
        }
    }

    fun canUseDesktopNetwork(): Boolean = isOnWifi()

    suspend fun lifeMode(mode: String): String {
        val response = postJson(
            "/mobile/life-mode",
            JSONObject().put("mode", normalizeLifeMode(mode)),
            authenticated = true
        )
        return normalizeLifeMode(response.optString("mode", mode))
    }

    suspend fun startPairing(): String = postJson("/mobile/pair/start", JSONObject()).getString("pairingCode")

    suspend fun completePairing(code: String, deviceName: String): String {
        val body = JSONObject()
            .put("pairingCode", code)
            .put("deviceName", deviceName)
        val response = postJson("/mobile/pair/complete", body)
        token = response.getString("token")
        return response.optString("deviceName", deviceName)
    }

    suspend fun charts(): List<ChartRow> {
        val response = getJson("/mobile/charts", authenticated = true)
        val rows = response.optJSONArray("charts") ?: JSONArray()
        return (0 until rows.length()).map { index ->
            rows.getJSONObject(index).toChartRow()
        }
    }

    suspend fun chat(message: String, chartId: String): ChatResult {
        val body = JSONObject()
            .put("message", message)
            .put("chartId", chartId)
            .put("wasVoiceInput", false)
        val response = postJson("/mobile/chat", body, authenticated = true)
        return ChatResult(
            response.optString("reply", "No reply."),
            response.optString("activeChartId"),
            response.optString("activePatientDisplayName"),
            response.optJSONObject("packet")?.toChartPacket()
        )
    }

    suspend fun personalChat(message: String, localId: String, createdAt: String): ChatResult {
        val body = JSONObject()
            .put("message", message)
            .put("localId", localId)
            .put("createdAt", createdAt)
        val response = postJson("/mobile/personal-chat", body, authenticated = true)
        return ChatResult(
            response.optString("reply", "Saved to Personal Mode."),
            response.optString("activeChartId"),
            response.optString("activePatientDisplayName"),
            response.optJSONObject("packet")?.toChartPacket()
        )
    }

    suspend fun personalCloudChat(message: String, fallbackConversationContext: String): String {
        if (!personalCloudEnabled) {
            return "Personal Cloud is off. Turn it on in Settings if you want an interactive Dolly reply on the go."
        }

        val apiKey = personalGeminiApiKey
        if (apiKey.isBlank()) {
            return "Add a Personal Gemini key in Settings to use interactive Dolly on the go."
        }

        val cooldownSeconds = personalCloudCooldownRemainingSeconds()
        if (cooldownSeconds > 0L) {
            return personalCloudCoolingDownReply(cooldownSeconds)
        }

        val scrubbedMessage = scrubPersonalCloudText(message)
        val scrubbedBrief = scrubPersonalCloudText(personalThreadBrief).take(900)
        val scrubbedContext = if (scrubbedBrief.isBlank()) {
            scrubPersonalCloudText(fallbackConversationContext).take(900)
        } else {
            ""
        }
        val prompt =
            "You are Dolly in VitaMR Personal Mode on the user's phone.\n" +
            "User profile: The user is building VitaMR/Dolly, a longevity-first medical-record sovereignty project. The user prefers concise, practical brainstorming with continuity. Personal Mode is non-sensitive and is not medical evidence.\n" +
            "Safety: Do not use or request medical chart context. Do not diagnose, triage, prescribe, handle legal/financial/password/identity secrets, or claim this is evidence.\n" +
            "The latest user message has already been saved locally on the phone. Reply concisely and ask one useful follow-up question when helpful.\n\n" +
            if (scrubbedBrief.isNotBlank()) {
                "Current thread brief:\n$scrubbedBrief\n\n"
            } else {
                "Recent Personal conversation:\n$scrubbedContext\n\n"
            } +
                "Latest user note:\n$scrubbedMessage"
        val body = JSONObject()
            .put(
                "contents",
                JSONArray().put(
                    JSONObject().put(
                        "parts",
                        JSONArray().put(JSONObject().put("text", prompt))
                    )
                )
            )
            .put(
                "generationConfig",
                JSONObject()
                    .put("temperature", 0.45)
                    .put("maxOutputTokens", 220)
            )
        val url =
            "https://generativelanguage.googleapis.com/v1beta/models/$PersonalGeminiModel:generateContent?key=" +
                URLEncoder.encode(apiKey, Charsets.UTF_8.name())
        val response = try {
            postAbsoluteJson(url, body)
        } catch (exception: GeminiHttpException) {
            if (exception.code == 429 || exception.code == 503) {
                startPersonalCloudCooldown()
                return personalCloudCoolingDownReply(PersonalCloudCooldownSeconds)
            }
            throw exception
        }
        return response
            .optJSONArray("candidates")
            ?.optJSONObject(0)
            ?.optJSONObject("content")
            ?.optJSONArray("parts")
            ?.optJSONObject(0)
            ?.optString("text")
            ?.trim()
            ?.ifBlank { null }
            ?: "Saved to Personal Mode. Gemini did not return a useful reply, but your note stayed saved."
    }

    suspend fun chartPacket(chartId: String, packetType: String, mode: String = "current"): ChartPacket {
        val body = JSONObject()
            .put("chartId", chartId)
            .put("packetType", packetType)
            .put("mode", mode)
        return postJson("/mobile/chart-packet", body, authenticated = true).toChartPacket()
    }

    suspend fun vitaMastery(chartId: String): VitaMastery {
        val body = JSONObject().put("chartId", chartId)
        return postJson("/mobile/vita-mastery", body, authenticated = true).toVitaMastery()
    }

    suspend fun dataHunterQuest(chartId: String): ChatResult {
        val body = JSONObject().put("chartId", chartId)
        val response = postJson("/mobile/data-hunter", body, authenticated = true)
        return ChatResult(
            response.optString("reply", "No reply."),
            response.optString("activeChartId"),
            response.optString("activePatientDisplayName"),
            response.optJSONObject("packet")?.toChartPacket()
        )
    }

    suspend fun personalSummary(): PersonalSummaryResult {
        val response = getJson("/mobile/personal-summary", authenticated = true)
        return PersonalSummaryResult(
            response.optString("status"),
            response.optString("updatedAt"),
            response.optString("summary"),
            response.optString("message")
        )
    }

    fun offlineItems(): List<OfflineItem> {
        val raw = prefs.getString("offline_items", "[]") ?: "[]"
        val rows = runCatching { JSONArray(raw) }.getOrElse { JSONArray() }
        return (0 until rows.length()).mapNotNull { index ->
            rows.optJSONObject(index)?.toOfflineItem()
        }
    }

    fun saveOfflineItems(items: List<OfflineItem>) {
        val rows = JSONArray()
        items.forEach { item -> rows.put(item.toJson()) }
        prefs.edit().putString("offline_items", rows.toString()).apply()
    }

    fun savedPackets(): List<ChartPacket> {
        val raw = prefs.getString("chart_packets", "[]") ?: "[]"
        val rows = runCatching { JSONArray(raw) }.getOrElse { JSONArray() }
        return (0 until rows.length()).mapNotNull { index ->
            rows.optJSONObject(index)?.toChartPacket()
        }
    }

    fun saveChartPackets(packets: Collection<ChartPacket>) {
        val rows = JSONArray()
        packets.forEach { packet -> rows.put(packet.toJson()) }
        prefs.edit().putString("chart_packets", rows.toString()).apply()
    }

    fun savedCharts(): List<ChartRow> {
        val raw = prefs.getString("chart_rows", "[]") ?: "[]"
        val rows = runCatching { JSONArray(raw) }.getOrElse { JSONArray() }
        return (0 until rows.length()).mapNotNull { index ->
            rows.optJSONObject(index)?.toChartRow()
        }
    }

    fun saveCharts(charts: Collection<ChartRow>) {
        val rows = JSONArray()
        charts.forEach { chart -> rows.put(chart.toJson()) }
        prefs.edit().putString("chart_rows", rows.toString()).apply()
    }

    fun saveOfflineAttachment(uri: Uri, chartId: String, patientDisplayName: String, kind: String, contentType: String, note: String): OfflineItem? {
        val localId = UUID.randomUUID().toString()
        val folder = File(context.filesDir, "offline_outbox").apply { mkdirs() }
        val extension = if (contentType.contains("jpeg", ignoreCase = true)) ".jpg" else ".bin"
        val target = File(folder, "$localId$extension")
        context.contentResolver.openInputStream(uri)?.use { input ->
            target.outputStream().use { output -> input.copyTo(output) }
        } ?: return null
        return OfflineItem(
            localId = localId,
            createdAt = OffsetDateTime.now().toString(),
            chartId = chartId,
            patientDisplayName = patientDisplayName.ifBlank { "Current chart" },
            kind = kind,
            note = note,
            status = "pending",
            fileUri = target.absolutePath,
            fileName = target.name,
            contentType = contentType
        )
    }

    fun deleteOfflineFile(item: OfflineItem) {
        if (item.fileUri.isBlank()) return
        runCatching { File(item.fileUri).delete() }
    }

    suspend fun sendOfflineItem(item: OfflineItem): OfflineSyncResult {
        if (item.kind == "personal_text" ||
            item.kind == "personal_session" ||
            item.kind == "pet_note" ||
            item.kind == "lockbox_note"
        ) {
            val body = JSONObject()
                .put("localId", item.localId)
                .put("createdAt", item.createdAt)
                .put("kind", item.kind)
                .put("note", item.note)
            val response = postJson("/mobile/personal-item", body, authenticated = true)
            val fallbackStatus = when (item.kind) {
                "pet_note" -> "pet_vault_synced"
                "lockbox_note" -> "lockbox_vault_synced"
                else -> "personal_vault_synced"
            }
            return OfflineSyncResult(
                response.optString("status", fallbackStatus),
                response.optString("message", "Saved to desktop Personal Vault."),
                ""
            )
        }

        if (item.fileUri.isNotBlank()) {
            val message = captureStoredFile(item)
            return OfflineSyncResult(
                "synced",
                message,
                "Attachment is now in the desktop Phone Inbox for review."
            )
        }

        val chatResult = chat(item.note, item.chartId)
        return OfflineSyncResult(
            "sent_to_dolly",
            chatResult.reply,
            ""
        )
    }

    suspend fun capture(uri: Uri, chartId: String, note: String): String {
        val bytes = compressImageUri(uri) ?: return "Could not read selected photo."
        val fileName = uri.lastPathSegment?.substringAfterLast('/') ?: "mobile-capture.jpg"
        return captureBytes(bytes, chartId, note, "gallery", fileName, "image/jpeg")
    }

    suspend fun captureRaw(uri: Uri, chartId: String, kind: String, contentType: String, note: String): String {
        val resolver = context.contentResolver
        val bytes = resolver.openInputStream(uri)?.use { it.readBytes() } ?: return "Could not read selected file."
        val fileName = uri.lastPathSegment?.substringAfterLast('/') ?: "mobile-capture.bin"
        return captureBytes(bytes, chartId, note, kind, fileName, contentType)
    }

    suspend fun captureCameraBitmap(bitmap: Bitmap, chartId: String, note: String): String {
        val output = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.JPEG, 78, output)
        return captureBytes(output.toByteArray(), chartId, note, "camera", "camera-capture.jpg", "image/jpeg")
    }

    suspend fun chartPhoto(chartId: String): ChartPhotoResult {
        val body = JSONObject().put("chartId", chartId)
        return postJson("/mobile/chart-photo", body, authenticated = true).toChartPhotoResult()
    }

    suspend fun setChartPhoto(uri: Uri, chartId: String): ChartPhotoResult {
        val bytes = compressImageUri(uri) ?: throw IllegalStateException("Could not read selected photo.")
        val body = JSONObject()
            .put("chartId", chartId)
            .put("contentType", "image/jpeg")
            .put("base64Data", Base64.getEncoder().encodeToString(bytes))
        return postJson("/mobile/chart-photo", body, authenticated = true).toChartPhotoResult()
    }

    suspend fun captureAudio(file: File, chartId: String): String {
        val bytes = file.readBytes()
        return captureBytes(bytes, chartId, "", "voice", file.name, "audio/mp4")
    }

    private suspend fun captureStoredFile(item: OfflineItem): String {
        val file = File(item.fileUri)
        if (!file.exists()) {
            return "Saved attachment file is missing on this phone."
        }

        return captureBytes(
            file.readBytes(),
            item.chartId,
            item.note,
            item.kind,
            item.fileName.ifBlank { file.name },
            item.contentType.ifBlank { "application/octet-stream" }
        )
    }

    private fun compressImageUri(uri: Uri): ByteArray? {
        val resolver = context.contentResolver
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        resolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, bounds) }

        val decodeOptions = BitmapFactory.Options().apply {
            inSampleSize = calculateSampleSize(bounds.outWidth, bounds.outHeight)
        }

        val bitmap = resolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, decodeOptions) } ?: return null
        return bitmap.useCompressedJpeg()
    }

    private suspend fun captureBytes(
        bytes: ByteArray,
        chartId: String,
        note: String,
        captureType: String,
        fileName: String,
        contentType: String
    ): String {
        val body = JSONObject()
            .put("chartId", chartId)
            .put("note", note)
            .put("captureType", captureType)
            .put("fileName", fileName)
            .put("contentType", contentType)
            .put("base64Data", Base64.getEncoder().encodeToString(bytes))
        val response = postJson("/mobile/capture", body, authenticated = true)
        return response.optString("message", "Capture queued.")
    }

    private suspend fun getJson(path: String, authenticated: Boolean = false): JSONObject =
        request("GET", path, null, authenticated)

    private suspend fun postJson(path: String, body: JSONObject, authenticated: Boolean = false): JSONObject =
        request("POST", path, body, authenticated)

    private suspend fun postAbsoluteJson(url: String, body: JSONObject): JSONObject =
        withContext(Dispatchers.IO) {
            val connection = URL(url).openConnection() as HttpURLConnection
            connection.requestMethod = "POST"
            connection.connectTimeout = 10000
            connection.readTimeout = 45000
            connection.setRequestProperty("Content-Type", "application/json")
            connection.doOutput = true
            connection.outputStream.use { it.write(body.toString().toByteArray(Charsets.UTF_8)) }
            val stream = if (connection.responseCode in 200..299) connection.inputStream else connection.errorStream
            val text = BufferedReader(InputStreamReader(stream)).use { it.readText() }
            if (connection.responseCode !in 200..299) {
                val error = runCatching { JSONObject(text).optJSONObject("error") }.getOrNull()
                val message = error?.optString("message")?.ifBlank { null }
                    ?: runCatching { JSONObject(text).optString("error") }.getOrNull()?.ifBlank { null }
                    ?: "Gemini HTTP ${connection.responseCode}"
                throw GeminiHttpException(connection.responseCode, message)
            }
            JSONObject(text)
        }

    private suspend fun request(method: String, path: String, body: JSONObject?, authenticated: Boolean): JSONObject =
        withContext(Dispatchers.IO) {
            val connection = URL(host + path).openConnection() as HttpURLConnection
            connection.requestMethod = method
            connection.connectTimeout = 7000
            connection.readTimeout = 60000
            connection.setRequestProperty("Content-Type", "application/json")
            if (authenticated && token.isNotBlank()) {
                connection.setRequestProperty("Authorization", "Bearer $token")
            }
            if (body != null) {
                connection.doOutput = true
                connection.outputStream.use { it.write(body.toString().toByteArray(Charsets.UTF_8)) }
            }
            val stream = if (connection.responseCode in 200..299) connection.inputStream else connection.errorStream
            val text = BufferedReader(InputStreamReader(stream)).use { it.readText() }
            if (connection.responseCode !in 200..299) {
                throw IllegalStateException(JSONObject(text).optString("message", "HTTP ${connection.responseCode}"))
            }
            JSONObject(text)
        }

    private fun isOnWifi(): Boolean {
        val manager = context.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager ?: return true
        val network = manager.activeNetwork ?: return false
        val capabilities = manager.getNetworkCapabilities(network) ?: return false
        return capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
    }

    private fun describeConnectionProblem(exception: Exception): String =
        when (exception) {
            is UnknownHostException -> "Not connected: desktop address is not valid or cannot be found."
            is ConnectException,
            is NoRouteToHostException -> "Not connected: VitaMR desktop host is not reachable. Open VitaMR on the computer."
            is SocketTimeoutException -> "Not connected: desktop took too long to answer. Check same Wi-Fi and firewall."
            is IOException -> "Not connected: check Wi-Fi and make sure VitaMR desktop is open."
            else -> "Not connected: ${exception.message ?: "VitaMR desktop did not answer."}"
        }
}

private fun calculateSampleSize(width: Int, height: Int): Int {
    var sampleSize = 1
    var scaledWidth = width
    var scaledHeight = height
    while (scaledWidth > MaxCaptureDimension || scaledHeight > MaxCaptureDimension) {
        sampleSize *= 2
        scaledWidth /= 2
        scaledHeight /= 2
    }

    return sampleSize
}

private fun Bitmap.useCompressedJpeg(): ByteArray {
    val output = ByteArrayOutputStream()
    compress(Bitmap.CompressFormat.JPEG, 78, output)
    recycle()
    return output.toByteArray()
}

private fun JSONObject.toChartPacket(): ChartPacket =
    ChartPacket(
        status = optString("status"),
        chartId = optString("chartId"),
        patientDisplayName = optString("patientDisplayName"),
        packetType = optString("packetType"),
        mode = optString("mode"),
        createdAt = optString("createdAt"),
        title = optString("title"),
        body = optString("body"),
        message = optString("message")
    )

private fun JSONObject.toChartRow(): ChartRow =
    ChartRow(
        chartId = optString("chartId"),
        displayName = optString("patientDisplayName", optString("displayName")),
        isActive = optBoolean("isActive")
    )

private fun JSONObject.toChartPhotoResult(): ChartPhotoResult =
    ChartPhotoResult(
        status = optString("status"),
        chartId = optString("chartId"),
        patientDisplayName = optString("patientDisplayName"),
        hasPhoto = optBoolean("hasPhoto"),
        contentType = optString("contentType"),
        base64Data = optString("base64Data"),
        message = optString("message")
    )

private fun ChartPhotoResult.toBitmapOrNull(): Bitmap? {
    if (!hasPhoto || base64Data.isBlank()) return null
    return runCatching {
        val bytes = Base64.getDecoder().decode(base64Data)
        BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
    }.getOrNull()
}

private fun JSONObject.toVitaMastery(): VitaMastery {
    val rows = optJSONArray("activeMicroQuests") ?: JSONArray()
    val quests = (0 until rows.length()).mapNotNull { index ->
        rows.optJSONObject(index)?.let { item ->
            VitaMasteryQuest(
                questId = item.optString("questId"),
                title = item.optString("title"),
                stage = item.optString("stage"),
                status = item.optString("status"),
                evidenceHint = item.optString("evidenceHint"),
                points = item.optInt("points")
            )
        }
    }

    return VitaMastery(
        status = optString("status"),
        chartId = optString("chartId"),
        patientDisplayName = optString("patientDisplayName"),
        percentComplete = optInt("percentComplete"),
        currentStage = optString("currentStage"),
        earnedPoints = optInt("earnedPoints"),
        possiblePoints = optInt("possiblePoints"),
        summaryLine = optString("summaryLine"),
        dataHunterTitle = optString("dataHunterTitle"),
        dataHunterStage = optString("dataHunterStage"),
        dataHunterPercent = optInt("dataHunterPercent"),
        dataHunterQuestion = optString("dataHunterQuestion"),
        dataHunterQuestCategory = optString("dataHunterQuestCategory"),
        dataHunterQuestStatus = optString("dataHunterQuestStatus"),
        dataHunterQuestXp = optInt("dataHunterQuestXp"),
        dataHunterXpTotal = optInt("dataHunterXpTotal"),
        dataHunterMasterTarget = optString("dataHunterMasterTarget"),
        dataHunterMasterTargetDetail = optString("dataHunterMasterTargetDetail"),
        dataHunterMasterAcceptedCount = optInt("dataHunterMasterAcceptedCount"),
        dataHunterMasterPendingCount = optInt("dataHunterMasterPendingCount"),
        dataHunterQuestControlPrompt = optString("dataHunterQuestControlPrompt"),
        dataHunterPrompt = optString("dataHunterPrompt"),
        activeMicroQuests = quests
    )
}

private fun JSONObject.toOfflineItem(): OfflineItem =
    OfflineItem(
        localId = optString("localId"),
        createdAt = optString("createdAt"),
        chartId = optString("chartId"),
        patientDisplayName = optString("patientDisplayName"),
        kind = optString("kind", "text"),
        note = optString("note"),
        status = optString("status", "pending"),
        fileUri = optString("fileUri"),
        fileName = optString("fileName"),
        contentType = optString("contentType")
    )

private fun OfflineItem.toJson(): JSONObject =
    JSONObject()
        .put("localId", localId)
        .put("createdAt", createdAt)
        .put("chartId", chartId)
        .put("patientDisplayName", patientDisplayName)
        .put("kind", kind)
        .put("note", note)
        .put("status", status)
        .put("fileUri", fileUri)
        .put("fileName", fileName)
        .put("contentType", contentType)

private fun ChartPacket.toJson(): JSONObject =
    JSONObject()
        .put("status", status)
        .put("chartId", chartId)
        .put("patientDisplayName", patientDisplayName)
        .put("packetType", packetType)
        .put("mode", mode)
        .put("createdAt", createdAt)
        .put("title", title)
        .put("body", body)
        .put("message", message)

private fun ChartRow.toJson(): JSONObject =
    JSONObject()
        .put("chartId", chartId)
        .put("patientDisplayName", displayName)
        .put("isActive", isActive)

private fun packetKey(chartId: String, packetType: String, mode: String = "current"): String =
    "${chartId.trim().lowercase()}::${packetType.trim().lowercase()}::${mode.trim().lowercase().ifBlank { "current" }}"

private fun hasSavedPacket(packets: Map<String, ChartPacket>, chartId: String, packetType: String): Boolean =
    packets.values.any {
        it.chartId.equals(chartId, ignoreCase = true) &&
            it.packetType.equals(packetType, ignoreCase = true)
    }

private fun recordsForChart(packets: Map<String, ChartPacket>, chartId: String): List<ChartPacket> =
    packets.values
        .filter { it.chartId.equals(chartId, ignoreCase = true) }
        .sortedWith(compareBy<ChartPacket> { it.packetType }.thenByDescending { it.createdAt }.thenBy { it.title })

private fun removePacketsForChart(packets: MutableMap<String, ChartPacket>, chartId: String): Int {
    val keysToRemove = packets
        .filterValues { it.chartId.equals(chartId, ignoreCase = true) }
        .keys
        .toList()
    keysToRemove.forEach { key -> packets.remove(key) }
    return keysToRemove.size
}

private fun chartsFromPackets(packets: Collection<ChartPacket>): List<ChartRow> =
    packets
        .filter { it.chartId.isNotBlank() && it.patientDisplayName.isNotBlank() }
        .distinctBy { it.chartId.trim().lowercase() }
        .map { packet -> ChartRow(packet.chartId, packet.patientDisplayName, false) }

private fun mergeChartRows(primary: Collection<ChartRow>, fallback: Collection<ChartRow>): List<ChartRow> {
    val rows = linkedMapOf<String, ChartRow>()
    fallback.forEach { chart ->
        if (chart.chartId.isNotBlank()) {
            rows[chart.chartId.trim().lowercase()] = chart
        }
    }
    primary.forEach { chart ->
        if (chart.chartId.isNotBlank()) {
            rows[chart.chartId.trim().lowercase()] = chart
        }
    }
    return rows.values.toList()
}

private fun createCameraImageUri(context: Context): Uri {
    val file = File.createTempFile("vitamr-camera-", ".jpg", context.cacheDir)
    return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
}

private fun createVoiceFile(context: Context): File =
    File.createTempFile("vitamr-voice-", ".m4a", context.cacheDir)

private fun buildWelcomeMessage(patientName: String): String {
    val greeting = when (LocalTime.now().hour) {
        in 5..11 -> "Good morning"
        in 12..16 -> "Good afternoon"
        in 17..21 -> "Good evening"
        else -> "Good day"
    }

    val firstName = patientName.trim().split(Regex("\\s+")).firstOrNull().orEmpty()
    val name = firstName.ifBlank { "there" }
    return "$greeting, $name. I have ${patientName.trim().ifBlank { "your" }}'s chart open.\n\nWhat would you like to do first: ask a chart question, add a note, send a photo, or keep building the Data Hunter map?"
}

private fun looksHealthRelated(text: String): Boolean {
    val normalized = text.lowercase()
    return listOf(
        "ache",
        "allergy",
        "blood",
        "doctor",
        "dose",
        "er ",
        "fever",
        "headache",
        "hospital",
        "hurt",
        "lab",
        "med",
        "pain",
        "prescription",
        "rash",
        "sick",
        "sleep",
        "surgery",
        "symptom",
        "vaccine"
    ).any { normalized.contains(it) }
}

private fun scrubPersonalCloudText(text: String): String {
    var scrubbed = Regex("""\b\d{3}-\d{2}-\d{4}\b""").replace(text, "[redacted-id]")
    scrubbed = Regex("""\b(?:\d[ -]*?){13,16}\b""").replace(scrubbed, "[redacted-card-or-number]")
    scrubbed = Regex("""(?i)\b(password|passcode|pin)\s*[:=]\s*\S+""").replace(scrubbed, "$1: [redacted]")
    return scrubbed.trim()
}

private fun inferPersonalThreadTopic(note: String): String {
    val lower = note.lowercase()
    return when {
        listOf("youtube", "video", "thumbnail", "title", "channel", "story").any { lower.contains(it) } -> "YouTube project"
        listOf("vitamr", "dolly", "app", "software", "agentic").any { lower.contains(it) } -> "VitaMR project"
        listOf("early detection", "prevention", "longevity", "healthspan").any { lower.contains(it) } -> "Early detection and longevity"
        listOf("shopping", "buy", "store", "list").any { lower.contains(it) } -> "Shopping list"
        listOf("schedule", "appointment", "calendar", "remind").any { lower.contains(it) } -> "Planning and reminders"
        else -> extractPersonalTopicPhrase(note)
    }
}

private fun inferPersonalThreadGoal(note: String): String {
    return note
        .trim()
        .replace(Regex("""\s+"""), " ")
        .take(180)
}

private fun extractPersonalTopicPhrase(note: String): String {
    val stopWords = setOf("that", "this", "with", "from", "have", "want", "need", "about", "into", "here", "there")
    val words = note
        .replace(Regex("""[^\p{L}\p{N}\s]"""), " ")
        .split(Regex("""\s+"""))
        .filter { it.length > 3 }
        .filterNot { it.lowercase() in stopWords }
        .take(4)
    return if (words.isEmpty()) "Personal planning" else words.joinToString(" ").replaceFirstChar { it.uppercase() }
}

private fun buildPersonalConversationContext(messages: List<ChatMessage>): String =
    messages
        .takeLast(8)
        .joinToString("\n") { message ->
            "${message.author}: ${message.text.take(380)}"
        }
        .take(1800)

private fun buildPersonalSessionSummary(messages: List<ChatMessage>, reason: String): String {
    val recent = messages
        .takeLast(16)
        .filter { it.text.isNotBlank() }
    val userLines = recent
        .filter { it.author == "You" }
        .map { "- ${it.text.take(220)}" }
        .takeLast(8)
    val dollyLines = recent
        .filter { it.author == "Dolly" }
        .map { "- ${it.text.take(220)}" }
        .takeLast(6)
    return buildString {
        appendLine("Personal Mode session summary synced because $reason.")
        appendLine()
        appendLine("## User ideas and requests")
        if (userLines.isEmpty()) {
            appendLine("- No user Personal messages captured in this session window.")
        } else {
            userLines.forEach { appendLine(it) }
        }
        appendLine()
        appendLine("## Dolly responses and follow-ups")
        if (dollyLines.isEmpty()) {
            appendLine("- No Dolly Personal replies captured in this session window.")
        } else {
            dollyLines.forEach { appendLine(it) }
        }
        appendLine()
        appendLine("## Raw recent Personal conversation")
        appendLine(buildPersonalConversationContext(recent))
    }
}

private fun normalizeLifeMode(mode: String): String =
    when (mode.trim().lowercase()) {
        "personal" -> "Personal"
        "lockbox", "secret", "secret mode", "local lockbox" -> "Lockbox"
        "pets" -> "Pets"
        else -> "Medical"
    }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun VitaMRApp() {
    val context = LocalContext.current
    val activity = context as? MainActivity
    val client = remember { VitaMRClient(context) }
    val scope = rememberCoroutineScope()
    val messages = remember { mutableStateListOf<ChatMessage>() }
    val charts = remember { mutableStateListOf<ChartRow>() }
    val pendingAttachments = remember { mutableStateListOf<PendingAttachment>() }
    val offlineItems = remember { mutableStateListOf<OfflineItem>() }
    val chartPackets = remember { mutableStateMapOf<String, ChartPacket>() }
    val vitaMasteryByChart = remember { mutableStateMapOf<String, VitaMastery>() }
    var selectedTab by remember { mutableStateOf("Chat") }
    var host by remember { mutableStateOf(client.host) }
    var status by remember { mutableStateOf("Not checked") }
    var pairingCode by remember { mutableStateOf("") }
    var selectedChartId by remember { mutableStateOf("") }
    var selectedChartName by remember { mutableStateOf("") }
    var chartHome by remember { mutableStateOf<ChartRow?>(null) }
    var showVitaMasteryDetail by remember { mutableStateOf(false) }
    var chartPacketTitle by remember { mutableStateOf("") }
    var chartPacketText by remember { mutableStateOf("") }
    var input by remember { mutableStateOf("") }
    var welcomedChartId by remember { mutableStateOf("") }
    var isDollyThinking by remember { mutableStateOf(false) }
    var isTravelSyncing by remember { mutableStateOf(false) }
    var travelSyncMessage by remember { mutableStateOf("") }
    var showNoteOptions by remember { mutableStateOf(false) }
    var showOtherRequest by remember { mutableStateOf(false) }
    var otherRequestText by remember { mutableStateOf("") }
    var selectedRecord by remember { mutableStateOf<ChartPacket?>(null) }
    var biometricEnabled by remember { mutableStateOf(client.biometricEnabled) }
    var biometricUnlocked by remember { mutableStateOf(!client.biometricEnabled) }
    var personalCloudEnabled by remember { mutableStateOf(client.personalCloudEnabled) }
    var personalGeminiKeyConfigured by remember { mutableStateOf(client.hasPersonalGeminiKey()) }
    var personalGeminiKeyInput by remember { mutableStateOf("") }
    var personalCloudStatus by remember { mutableStateOf(client.personalCloudLabel()) }
    var personalMemorySummaryStatus by remember {
        mutableStateOf(
            if (client.personalMemorySummary.isBlank()) {
                "Personal memory summary not synced yet."
            } else {
                "Personal memory summary cached on phone."
            }
        )
    }
    var wirelessDebuggingEnabled by remember { mutableStateOf(client.wirelessDebuggingEnabled()) }
    var dataHunterXpPop by remember { mutableStateOf<DataHunterXpPop?>(null) }
    var showDataHunterQuickActions by remember { mutableStateOf(false) }
    var healthspanModeEnabled by remember { mutableStateOf(false) }
    var healthspanIntensity by remember { mutableStateOf("Support") }
    var showHealthspanIntensityChoices by remember { mutableStateOf(false) }
    var activeLifeMode by remember { mutableStateOf("Medical") }
    var activeChartPhoto by remember { mutableStateOf<Bitmap?>(null) }
    var hostDetected by remember { mutableStateOf(false) }
    var lastPersonalHostSnapshotSignature by remember { mutableStateOf("") }

    fun showDollyWelcomeForChart(chartId: String, patientName: String) {
        if (chartId.isBlank() || patientName.isBlank() || welcomedChartId == chartId) {
            return
        }

        val welcome = ChatMessage("Dolly", buildWelcomeMessage(patientName))
        if (messages.isEmpty()) {
            messages += welcome
        } else if (messages.size == 1 && messages.first().author == "Dolly" && welcomedChartId.isNotBlank()) {
            messages[0] = welcome
        } else {
            return
        }

        welcomedChartId = chartId
    }

    fun refreshWirelessDebuggingState(): Boolean? {
        val enabled = client.wirelessDebuggingEnabled()
        wirelessDebuggingEnabled = enabled
        return enabled
    }

    fun persistOfflineItems() {
        client.saveOfflineItems(offlineItems.toList())
    }

    fun persistChartPackets() {
        client.saveChartPackets(chartPackets.values)
    }

    fun refreshPersonalCloudStatus() {
        personalCloudEnabled = client.personalCloudEnabled
        personalGeminiKeyConfigured = client.hasPersonalGeminiKey()
        personalCloudStatus = client.personalCloudLabel()
    }

    fun persistCharts() {
        client.saveCharts(charts.toList())
    }

    fun markHostDetected(health: HealthResult): Boolean {
        val detected = health.activeLifeMode.isNotBlank()
        val becameReachable = detected && !hostDetected
        hostDetected = detected
        return becameReachable
    }

    fun refreshVitaMastery(chartId: String) {
        if (chartId.isBlank() || !client.isPaired()) return
        scope.launch {
            runCatching { client.vitaMastery(chartId) }.onSuccess { mastery ->
                val previousXp = vitaMasteryByChart[chartId]?.dataHunterXpTotal ?: mastery.dataHunterXpTotal
                vitaMasteryByChart[chartId] = mastery
                val gainedXp = mastery.dataHunterXpTotal - previousXp
                if (gainedXp > 0) {
                    dataHunterXpPop = DataHunterXpPop(System.currentTimeMillis(), gainedXp)
                }
            }
        }
    }

    fun refreshChartPhoto(chartId: String) {
        if (chartId.isBlank() || !client.isPaired()) {
            activeChartPhoto = null
            return
        }

        scope.launch {
            runCatching { client.chartPhoto(chartId) }
                .onSuccess { result ->
                    if (result.chartId.isBlank() || result.chartId == chartId) {
                        activeChartPhoto = result.toBitmapOrNull()
                    }
                }
                .onFailure {
                    activeChartPhoto = null
                }
        }
    }

    fun captureChartPhotoFromCamera() {
        if (selectedChartId.isBlank()) {
            status = "Open a chart before adding a photo."
            return
        }

        val captureUri = createCameraImageUri(context)
        val error = activity?.openCamera(captureUri) { uri ->
            if (uri == null) {
                status = "Camera capture canceled."
                return@openCamera
            }

            scope.launch {
                status = "Saving chart photo..."
                runCatching { client.setChartPhoto(uri, selectedChartId) }
                    .onSuccess { result ->
                        activeChartPhoto = result.toBitmapOrNull()
                        status = result.message.ifBlank { "Chart photo saved." }
                    }
                    .onFailure { exception ->
                        status = "Chart photo could not be saved: ${exception.message ?: "unknown error"}"
                    }
            }
        } ?: "VitaMR could not open the phone camera."
        if (error != null) status = error
    }

    fun openVitaMasteryProgress() {
        if (selectedChartId.isBlank()) return
        showVitaMasteryDetail = true
        refreshVitaMastery(selectedChartId)
    }

    fun resetChartReviewState(clearOpenChart: Boolean = false) {
        selectedRecord = null
        showNoteOptions = false
        showOtherRequest = false
        otherRequestText = ""
        chartPacketTitle = ""
        chartPacketText = ""
        travelSyncMessage = ""
        if (clearOpenChart) {
            chartHome = null
        }
    }

    fun saveOfflineText(note: String) {
        val item = OfflineItem(
            localId = UUID.randomUUID().toString(),
            createdAt = OffsetDateTime.now().toString(),
            chartId = selectedChartId,
            patientDisplayName = selectedChartName.ifBlank { "Current chart" },
            kind = "text",
            note = note,
            status = "pending"
        )
        offlineItems += item
        persistOfflineItems()
    }

    fun saveLocalPersonalItem(note: String, kind: String = "personal_text"): OfflineItem {
        val item = OfflineItem(
            localId = UUID.randomUUID().toString(),
            createdAt = OffsetDateTime.now().toString(),
            chartId = "",
            patientDisplayName = "Personal",
            kind = kind,
            note = note,
            status = "local_only"
        )
        offlineItems += item
        persistOfflineItems()
        return item
    }

    fun setLifeMode(mode: String, syncDesktop: Boolean = true, announce: Boolean = true) {
        val normalizedMode = normalizeLifeMode(mode)
        activeLifeMode = normalizedMode
        showDataHunterQuickActions = false
        showHealthspanIntensityChoices = false
        if (syncDesktop && client.isPaired()) {
            scope.launch {
                runCatching { client.lifeMode(normalizedMode) }
                    .onSuccess { activeLifeMode = it }
            }
        }
        if (!announce) return
        val reply = when (normalizedMode) {
            "Personal" ->
                "Personal Mode is on.\n\nUse this for non-sensitive ideas, tasks, shopping lists, projects, work notes, YouTube ideas, and ordinary life capture. ${client.personalCloudLabel()}. Do not enter sensitive medical, financial, legal, password, identity, or deeply private information here; use Local Lockbox for that."
            "Lockbox" ->
                "Local Lockbox is on.\n\nUse this for sensitive personal notes. I will save them locally and sync only to the trusted desktop when connected. I will not send Lockbox notes to Gemini or the medical chart."
            "Pets" ->
                "Pets Mode is on.\n\nPet notes are treated as animal records, not human medical charts. Start with a pet's name and species, then add vet records, vaccine notes, meds, boarding info, or questions for the vet."
            else ->
                "Medical Mode is on.\n\nI will treat messages as chart-related context for the active medical record. Personal notes should stay in Personal Mode unless you ask me to switch."
        }
        messages += ChatMessage("Dolly", reply)
    }

    fun handlePersonalOrPetMessage(text: String): Boolean {
        if (text.isBlank()) return false
        if (activeLifeMode == "Personal") {
            val item = saveLocalPersonalItem(text)
            val healthHint = if (looksHealthRelated(text)) {
                "\n\nThis may be health-related. I saved it only in Personal Mode for now; switch to Medical Mode when you want to review it as pending medical context."
            } else {
                ""
            }
            val conversationContext = buildPersonalConversationContext(messages)
            fun syncPersonalNoteToDesktop() {
                if (!client.isPaired() || !client.canUseDesktopNetwork()) return
                scope.launch {
                    runCatching { client.sendOfflineItem(item) }
                        .onSuccess { result ->
                            val index = offlineItems.indexOfFirst { it.localId == item.localId }
                            if (index >= 0) {
                                offlineItems[index] = item.copy(status = result.status.ifBlank { "personal_vault_synced" })
                                persistOfflineItems()
                            }
                            status = result.message.ifBlank { "Personal note synced to desktop." }
                        }
                }
            }

            fun answerWithPhoneGemini() {
                if (!client.personalCloudEnabled || !client.hasPersonalGeminiKey()) {
                    messages += ChatMessage(
                        "Dolly",
                        "Saved to Personal Mode. Turn on Personal Cloud and save a Personal Gemini key in Settings for interactive Dolly replies on the go.$healthHint"
                    )
                    status = "Saved personal note locally."
                    return
                }

                scope.launch {
                    isDollyThinking = true
                    runCatching { client.personalCloudChat(text, conversationContext) }
                        .onSuccess { reply ->
                            client.updatePersonalThreadBrief(text, reply)
                            messages += ChatMessage("Dolly", "$reply$healthHint")
                            status = "Personal Cloud reply from Gemini Flash-Lite."
                            syncPersonalNoteToDesktop()
                        }
                        .onFailure { exception ->
                            client.updatePersonalThreadBrief(text, "Personal Cloud could not answer yet.")
                            messages += ChatMessage(
                                "Dolly",
                                "Saved to Personal Mode locally. Personal Cloud could not answer: ${exception.message ?: "Gemini was not reachable."}$healthHint"
                            )
                            status = "Saved personal note locally."
                        }
                    isDollyThinking = false
                }
            }

            if (hostDetected && client.isPaired() && client.canUseDesktopNetwork()) {
                scope.launch {
                    isDollyThinking = true
                    runCatching { client.personalChat(text, item.localId, item.createdAt) }
                        .onSuccess { chatResult ->
                            val index = offlineItems.indexOfFirst { it.localId == item.localId }
                            if (index >= 0) {
                                offlineItems[index] = item.copy(status = "personal_vault_synced")
                                persistOfflineItems()
                            }
                            messages += ChatMessage("Dolly", "${chatResult.reply}$healthHint")
                            status = "Saved to desktop Personal Vault."
                        }
                        .onFailure {
                            answerWithPhoneGemini()
                        }
                    isDollyThinking = false
                }
            } else if (client.personalCloudEnabled && client.hasPersonalGeminiKey()) {
                answerWithPhoneGemini()
            } else {
                answerWithPhoneGemini()
            }
            status = "Saved personal note locally."
            return true
        }

        if (activeLifeMode == "Lockbox") {
            val item = saveLocalPersonalItem(text, "lockbox_note")
            if (client.isPaired()) {
                scope.launch {
                    runCatching { client.sendOfflineItem(item) }
                        .onSuccess { result ->
                            val index = offlineItems.indexOfFirst { it.localId == item.localId }
                            if (index >= 0) {
                                offlineItems[index] = item.copy(status = result.status.ifBlank { "lockbox_vault_synced" })
                                persistOfflineItems()
                            }
                            messages += ChatMessage("Dolly", result.message.ifBlank { "Saved to Lockbox on the trusted desktop. No Gemini call was made." })
                            status = "Saved Lockbox note to trusted desktop."
                        }
                        .onFailure {
                            messages += ChatMessage("Dolly", "Saved to Local Lockbox on this phone. No Gemini call was made.")
                            status = "Saved Lockbox note locally."
                        }
                }
            } else {
                messages += ChatMessage("Dolly", "Saved to Local Lockbox on this phone. No Gemini call was made.")
            }
            status = "Saved Lockbox note locally."
            return true
        }

        if (activeLifeMode == "Pets") {
            saveLocalPersonalItem(text, "pet_note")
            messages += ChatMessage(
                "Dolly",
                "Saved to Pets Mode. I will treat this as animal/pet context, not a human chart. For a new pet, include name and species when you can."
            )
            status = "Saved pet note locally."
            return true
        }

        return false
    }

    fun closesDataHunterQuestControls(text: String): Boolean {
        val normalized = text.trim().lowercase()
        return normalized == "later" ||
            normalized == "pause" ||
            normalized == "not now" ||
            normalized == "stop" ||
            normalized.contains("pause") ||
            normalized.contains("later")
    }

    fun sendQuickChat(text: String) {
        if (text.isBlank()) return
        if (handlePersonalOrPetMessage(text)) return
        if (selectedChartId.isBlank()) return
        if (closesDataHunterQuestControls(text)) {
            showDataHunterQuickActions = false
        }
        messages += ChatMessage("You", text)
        scope.launch {
            isDollyThinking = true
            runCatching { client.chat(text, selectedChartId) }
                .onSuccess { chatResult ->
                    if (chatResult.activeChartId.isNotBlank()) {
                        selectedChartId = chatResult.activeChartId
                    }
                    if (chatResult.activePatientDisplayName.isNotBlank()) {
                        selectedChartName = chatResult.activePatientDisplayName
                        status = "Active chart: ${chatResult.activePatientDisplayName}"
                    }
                    messages += ChatMessage("Dolly", chatResult.reply)
                    refreshVitaMastery(selectedChartId)
                }
                .onFailure {
                    saveOfflineText(text)
                    messages += ChatMessage(
                        "Dolly",
                        "I could not reach VitaMR desktop, so I saved this on the phone. I will send it when Dolly reconnects."
                    )
                    status = "Saved locally for later sync."
                }
            isDollyThinking = false
        }
    }

    fun setPhoneHealthspanMode(enabled: Boolean) {
        if (selectedChartId.isBlank()) return
        if (healthspanModeEnabled == enabled && enabled) {
            showHealthspanIntensityChoices = true
            return
        }
        healthspanModeEnabled = enabled
        if (enabled) {
            showHealthspanIntensityChoices = true
            messages += ChatMessage(
                "Dolly",
                "Healthspan Mode is on.\n\nChoose how you want Dolly to coach: Support, Performance, or Experimental."
            )
            return
        }
        showHealthspanIntensityChoices = false
        sendQuickChat("switch to record assistant mode")
    }

    fun setPhoneHealthspanIntensity(intensity: String) {
        if (selectedChartId.isBlank()) return
        healthspanModeEnabled = true
        healthspanIntensity = intensity
        showHealthspanIntensityChoices = false
        val command = when (intensity) {
            "Performance" -> "switch to longevity performance mode"
            "Experimental" -> "switch to elite experimental longevity mode"
            else -> "switch to healthspan support mode"
        }
        sendQuickChat(command)
    }

    fun syncPendingOfflineItems() {
        if (!client.isPaired()) return
        val pending = offlineItems.filter {
            it.status == "pending" ||
                it.status == "local_only" ||
                (it.kind == "text" && it.status == "synced")
        }
        if (pending.isEmpty()) return

        scope.launch {
            var synced = 0
            pending.forEach { item ->
                runCatching { client.sendOfflineItem(item) }.onSuccess { result ->
                    val index = offlineItems.indexOfFirst { it.localId == item.localId }
                    if (index >= 0) {
                        offlineItems[index] = item.copy(status = result.status.ifBlank { "synced" })
                    }
                    if (item.fileUri.isNotBlank()) {
                        client.deleteOfflineFile(item)
                    }
                    synced++
                    messages += ChatMessage("Dolly", listOf(result.message, result.contextQuestion).filter { it.isNotBlank() }.joinToString("\n\n"))
                }
            }

            if (synced > 0) {
                persistOfflineItems()
                status = "Sent $synced saved phone item${if (synced == 1) "" else "s"}."
            }
        }
    }

    fun syncPersonalConversationSnapshotToHost(reason: String = "host detected") {
        if (!client.isPaired() || !client.canUseDesktopNetwork()) return
        val conversation = buildPersonalConversationContext(messages)
        if (conversation.isBlank() || conversation == lastPersonalHostSnapshotSignature) return
        lastPersonalHostSnapshotSignature = conversation
        val item = OfflineItem(
            localId = UUID.randomUUID().toString(),
            createdAt = OffsetDateTime.now().toString(),
            chartId = "",
            patientDisplayName = "Personal",
            kind = "personal_session",
            note = buildPersonalSessionSummary(messages, reason),
            status = "local_only"
        )
        offlineItems += item
        persistOfflineItems()
        scope.launch {
            runCatching { client.sendOfflineItem(item) }
                .onSuccess { result ->
                    val index = offlineItems.indexOfFirst { it.localId == item.localId }
                    if (index >= 0) {
                        offlineItems[index] = item.copy(status = result.status.ifBlank { "personal_vault_synced" })
                        persistOfflineItems()
                    }
                    status = "Host detected. Personal conversation synced to desktop."
                }
        }
    }

    fun syncPersonalMemorySummaryFromHost() {
        if (!client.isPaired() || !client.canUseDesktopNetwork()) return
        scope.launch {
            runCatching { client.personalSummary() }
                .onSuccess { result ->
                    if (result.summary.isNotBlank()) {
                        client.personalMemorySummary = result.summary
                        personalMemorySummaryStatus = "Personal memory summary synced from host."
                        status = personalMemorySummaryStatus
                    }
                }
                .onFailure {
                    personalMemorySummaryStatus = "Personal memory summary could not sync from host."
                }
        }
    }

    fun announceHostDetectedAndSync(reason: String = "host detected") {
        val pendingCount = offlineItems.count {
            it.status == "pending" ||
                it.status == "local_only" ||
                (it.kind == "text" && it.status == "synced")
        }
        val note = if (pendingCount > 0) {
            "We are now connected to the host, and your saved phone data is being synced."
        } else {
            "We are now connected to the host. Dolly will use the trusted desktop for Personal replies and sync."
        }
        messages += ChatMessage("Dolly", note)
        status = if (pendingCount > 0) {
            "Host detected. Syncing saved phone data."
        } else {
            "Host detected. Personal host route ready."
        }
        syncPendingOfflineItems()
        syncPersonalConversationSnapshotToHost(reason)
        syncPersonalMemorySummaryFromHost()
    }

    fun syncChartForTravel(chart: ChartRow) {
        if (!client.isPaired()) {
            travelSyncMessage = "Pair this phone with VitaMR desktop before travel sync."
            status = travelSyncMessage
            return
        }

        scope.launch {
            isTravelSyncing = true
            travelSyncMessage = "Preparing ${chart.displayName} for travel..."
            status = travelSyncMessage

            var savedCount = 0
            var removedCount = 0
            val failedPackets = mutableListOf<String>()
            val refreshedPackets = mutableListOf<ChartPacket>()
            val travelPackets = listOf(
                "labs" to "latest",
                "notes" to "pcp",
                "vaccines" to "current",
                "imaging" to "recent",
                "questions" to "current"
            )

            travelPackets.forEach { (packetType, mode) ->
                runCatching { client.chartPacket(chart.chartId, packetType, mode) }
                    .onSuccess { packet ->
                        if (packet.status.equals("ready", ignoreCase = true)) {
                            refreshedPackets += packet
                            chartPacketTitle = packet.title
                            chartPacketText = packet.body.ifBlank { packet.message }
                            savedCount++
                        } else {
                            failedPackets += packetType
                        }
                    }
                    .onFailure {
                        failedPackets += packetType
                    }
            }

            if (refreshedPackets.isNotEmpty()) {
                removedCount = removePacketsForChart(chartPackets, chart.chartId)
                refreshedPackets.forEach { packet ->
                    chartPackets[packetKey(packet.chartId, packet.packetType, packet.mode)] = packet
                }
            }

            runCatching { client.vitaMastery(chart.chartId) }
                .onSuccess { mastery -> vitaMasteryByChart[chart.chartId] = mastery }

            val mergedCharts = mergeChartRows(
                charts + chart.copy(isActive = true),
                chartsFromPackets(chartPackets.values)
            )
            charts.clear()
            charts.addAll(mergedCharts)
            persistCharts()
            persistChartPackets()

            travelSyncMessage = when {
                savedCount == travelPackets.size -> "${chart.displayName} refreshed for travel. Replaced $removedCount old record${if (removedCount == 1) "" else "s"}."
                savedCount > 0 -> "${chart.displayName}: refreshed $savedCount packet${if (savedCount == 1) "" else "s"} and replaced $removedCount old record${if (removedCount == 1) "" else "s"}; ${failedPackets.joinToString(", ")} still need desktop."
                else -> "Travel sync could not reach VitaMR desktop. Saved packets already on this phone still work."
            }
            status = travelSyncMessage
            isTravelSyncing = false
        }
    }

    LaunchedEffect(Unit) {
        offlineItems.clear()
        offlineItems.addAll(client.offlineItems())
        chartPackets.clear()
        client.savedPackets().forEach { packet ->
            chartPackets[packetKey(packet.chartId, packet.packetType, packet.mode)] = packet
        }
        charts.clear()
        charts.addAll(mergeChartRows(client.savedCharts(), chartsFromPackets(chartPackets.values)))
        charts.firstOrNull()?.let { chart ->
            selectedChartId = chart.chartId
            selectedChartName = chart.displayName
            showDollyWelcomeForChart(chart.chartId, chart.displayName)
        }
        scope.launch {
            status = runCatching {
                val health = client.health()
                val becameReachable = markHostDetected(health)
                if (health.activeChartId.isNotBlank()) {
                    selectedChartId = health.activeChartId
                }
                if (health.activePatientDisplayName.isNotBlank()) {
                    selectedChartName = health.activePatientDisplayName
                }
                if (health.activeLifeMode.isNotBlank()) {
                    setLifeMode(health.activeLifeMode, syncDesktop = false, announce = false)
                }
                refreshVitaMastery(health.activeChartId)
                refreshChartPhoto(health.activeChartId)
                if (client.isPaired() && health.activePatientDisplayName.isNotBlank()) {
                    showDollyWelcomeForChart(health.activeChartId, health.activePatientDisplayName)
                }
                if (becameReachable) {
                    announceHostDetectedAndSync("app start")
                } else {
                    syncPendingOfflineItems()
                }
                if (health.activeChartId.isBlank() && charts.isNotEmpty()) {
                    "Offline chart cache ready."
                } else {
                    health.message
                }
            }.getOrElse { "Not connected: VitaMR desktop did not answer." }
        }
    }

    LaunchedEffect(selectedChartId) {
        refreshChartPhoto(selectedChartId)
    }

    LaunchedEffect(Unit) {
        while (true) {
            if (client.isPaired()) {
                runCatching { client.health() }.onSuccess { health ->
                    val becameReachable = markHostDetected(health)
                    if (health.activeLifeMode.isNotBlank()) {
                        setLifeMode(health.activeLifeMode, syncDesktop = false, announce = false)
                    }
                    if (becameReachable) {
                        announceHostDetectedAndSync()
                    }
                }
            }
            delay(5000)
        }
    }

    LaunchedEffect(selectedTab) {
        if (selectedTab == "Settings") {
            refreshWirelessDebuggingState()
        }

        if (selectedTab == "Charts" && client.isPaired()) {
            val cachedCharts = mergeChartRows(client.savedCharts(), chartsFromPackets(chartPackets.values))
            if (cachedCharts.isNotEmpty()) {
                charts.clear()
                charts.addAll(cachedCharts)
                status = "Showing saved chart cache."
            } else {
                status = "Refreshing charts..."
            }
            status = runCatching {
                val desktopCharts = client.charts()
                charts.clear()
                charts.addAll(mergeChartRows(desktopCharts, chartsFromPackets(chartPackets.values)))
                persistCharts()
                charts.firstOrNull { it.isActive }?.let {
                    selectedChartId = it.chartId
                    selectedChartName = it.displayName
                    refreshVitaMastery(it.chartId)
                    if (chartHome?.chartId == it.chartId) {
                        chartHome = it
                    }
                }
                "Charts ready."
            }.getOrElse {
                if (charts.isNotEmpty()) {
                    "Offline chart cache ready."
                } else {
                    "Charts unavailable: ${it.message}"
                }
            }
        }
    }

    if (biometricEnabled && !biometricUnlocked) {
        BiometricGateScreen(
            onUnlock = {
                (context as? FragmentActivity)?.requestVitaMRBiometric(
                    title = "Unlock VitaMR",
                    subtitle = "Manager biometric unlock is enabled for this phone.",
                    onResult = { success, message ->
                        status = message
                        biometricUnlocked = success
                    }
                )
            }
        )
        return
    }

    fun goBack() {
        when {
            selectedRecord != null -> selectedRecord = null
            showOtherRequest -> showOtherRequest = false
            showNoteOptions -> showNoteOptions = false
            showVitaMasteryDetail -> showVitaMasteryDetail = false
            chartHome != null -> {
                resetChartReviewState(clearOpenChart = true)
            }
            selectedTab != "Chat" -> selectedTab = "Chat"
        }
    }

    val bottomTabs = listOf("Chat", "Charts", "Saved", "Settings")

    fun selectBottomTab(tab: String) {
        showVitaMasteryDetail = false
        resetChartReviewState(clearOpenChart = true)
        selectedTab = tab
    }

    fun swipeBottomTab(forward: Boolean) {
        if (!forward && (selectedRecord != null || showOtherRequest || showNoteOptions || showVitaMasteryDetail || chartHome != null)) {
            goBack()
            return
        }

        val currentIndex = bottomTabs.indexOf(selectedTab).takeIf { it >= 0 } ?: 0
        val targetIndex = if (forward) {
            (currentIndex + 1).coerceAtMost(bottomTabs.lastIndex)
        } else {
            (currentIndex - 1).coerceAtLeast(0)
        }

        if (targetIndex != currentIndex) {
            selectBottomTab(bottomTabs[targetIndex])
        }
    }

    MaterialTheme {
        BackHandler(
            enabled = selectedRecord != null ||
                showOtherRequest ||
                showNoteOptions ||
                showVitaMasteryDetail ||
                chartHome != null ||
                selectedTab != "Chat"
        ) {
            goBack()
        }

        Scaffold(
            topBar = {
                TopAppBar(
                    title = {
                        Column(Modifier.padding(vertical = 2.dp)) {
                            Text(
                                "VitaMR",
                                style = MaterialTheme.typography.headlineSmall,
                                fontWeight = FontWeight.Bold,
                                color = Color(0xFF111318)
                            )
                            Text(
                                if (selectedChartName.isBlank()) "Active chart: No chart" else "Active chart: $selectedChartName",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.SemiBold,
                                color = Color(0xFF1F2933)
                            )
                        }
                    },
                    actions = {
                        ChartAvatar(
                            name = selectedChartName,
                            photo = activeChartPhoto,
                            onClick = { captureChartPhotoFromCamera() }
                        )
                    }
                )
            },
            bottomBar = {
                NavigationBar(
                    containerColor = Color(0xFFEDE6F7),
                    tonalElevation = 6.dp
                ) {
                    bottomTabs.forEach { tab ->
                        val selected = selectedTab == tab
                        NavigationBarItem(
                            selected = selected,
                            onClick = {
                                selectBottomTab(tab)
                            },
                            icon = {
                                Icon(
                                    imageVector = when (tab) {
                                        "Chat" -> Icons.Default.Chat
                                        "Charts" -> Icons.Default.Folder
                                        "Saved" -> Icons.Default.Bookmark
                                        else -> Icons.Default.Settings
                                    },
                                    contentDescription = tab
                                )
                            },
                            label = {
                                Text(
                                    tab,
                                    fontWeight = if (selected) FontWeight.Bold else FontWeight.SemiBold
                                )
                            },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = Color.White,
                                selectedTextColor = Color(0xFF3F2368),
                                indicatorColor = Color(0xFF6D4BA3),
                                unselectedIconColor = Color(0xFF5E5470),
                                unselectedTextColor = Color(0xFF4F465D)
                            )
                        )
                    }
                }
            }
        ) { padding ->
            Surface(
                Modifier
                    .padding(padding)
                    .fillMaxSize()
                    .pointerInput(selectedTab, selectedRecord, showOtherRequest, showNoteOptions, showVitaMasteryDetail, chartHome) {
                        var dragTotal = 0f
                        detectHorizontalDragGestures(
                            onDragStart = { dragTotal = 0f },
                            onHorizontalDrag = { _, dragAmount -> dragTotal += dragAmount },
                            onDragEnd = {
                                when {
                                    dragTotal < -90f -> swipeBottomTab(forward = true)
                                    dragTotal > 90f -> swipeBottomTab(forward = false)
                                }
                            }
                        )
                    },
                color = Color(0xFFF8FBFC)
            ) {
                if (showVitaMasteryDetail) {
                    VitaMasteryDetailScreen(
                        chart = ChartRow(
                            selectedChartId,
                            selectedChartName.ifBlank { "Current chart" },
                            true
                        ),
                        vitaMastery = vitaMasteryByChart[selectedChartId],
                        onBack = {
                            showVitaMasteryDetail = false
                        }
                    )
                } else when (selectedTab) {
                    "Chat" -> ChatScreen(
                        messages = messages,
                        vitaMastery = vitaMasteryByChart[selectedChartId],
                        dataHunterXpPop = dataHunterXpPop,
                        onDataHunterXpPopShown = { id ->
                            if (dataHunterXpPop?.id == id) {
                                dataHunterXpPop = null
                            }
                        },
                        onDataHunterTap = {
                            if (selectedChartId.isNotBlank()) {
                                scope.launch {
                                    isDollyThinking = true
                                    runCatching { client.dataHunterQuest(selectedChartId) }
                                        .onSuccess { chatResult ->
                                            if (chatResult.activeChartId.isNotBlank()) {
                                                selectedChartId = chatResult.activeChartId
                                            }
                                            if (chatResult.activePatientDisplayName.isNotBlank()) {
                                                selectedChartName = chatResult.activePatientDisplayName
                                                status = "Active chart: ${chatResult.activePatientDisplayName}"
                                            }
                                            messages += ChatMessage("Dolly", chatResult.reply)
                                            showDataHunterQuickActions = true
                                            refreshVitaMastery(selectedChartId)
                                        }
                                        .onFailure {
                                            status = "Data Hunter needs desktop connection."
                                        }
                                    isDollyThinking = false
                                }
                            }
                        },
                        onQuickReply = { reply ->
                            sendQuickChat(reply)
                        },
                        showDataHunterQuickActions = showDataHunterQuickActions,
                        healthspanModeEnabled = healthspanModeEnabled,
                        healthspanIntensity = healthspanIntensity,
                        activeLifeMode = activeLifeMode,
                        personalCloudStatus = personalCloudStatus,
                        personalMemorySummaryStatus = personalMemorySummaryStatus,
                        hostDetected = hostDetected,
                        showHealthspanIntensityChoices = showHealthspanIntensityChoices,
                        onRecordsMode = { setPhoneHealthspanMode(false) },
                        onHealthspanMode = { setPhoneHealthspanMode(true) },
                        onMedicalMode = { setLifeMode("Medical") },
                        onPersonalMode = { setLifeMode("Personal") },
                        onLockboxMode = { setLifeMode("Lockbox") },
                        onPetsMode = { setLifeMode("Pets") },
                        onHealthspanIntensity = { setPhoneHealthspanIntensity(it) },
                        isDollyThinking = isDollyThinking,
                        input = input,
                        onInputChange = { input = it },
                        onSend = {
                            val text = input.trim()
                            val attachmentsToSend = pendingAttachments.toList()
                            if (text.isBlank() && attachmentsToSend.isEmpty()) return@ChatScreen
                            if (closesDataHunterQuestControls(text)) {
                                showDataHunterQuickActions = false
                            }
                            if (activeLifeMode != "Medical" && attachmentsToSend.isNotEmpty()) {
                                messages += ChatMessage(
                                    "You",
                                    if (text.isBlank()) "Tried to send attachment" else "$text (${attachmentsToSend.size} attachment${if (attachmentsToSend.size == 1) "" else "s"})"
                                )
                                if (text.isNotBlank()) {
                                    handlePersonalOrPetMessage(text)
                                    input = ""
                                }
                                messages += ChatMessage(
                                    "Dolly",
                                    "I did not send the attachment. Phone attachments still route to the medical inbox, so switch to Medical Mode before sending files or photos."
                                )
                                status = "Attachment held. Switch to Medical Mode to send."
                                return@ChatScreen
                            }
                            val attachmentLine = if (attachmentsToSend.isEmpty()) {
                                ""
                            } else {
                                " (${attachmentsToSend.size} attachment${if (attachmentsToSend.size == 1) "" else "s"})"
                            }
                            messages += ChatMessage("You", if (text.isBlank()) "Sent attachment$attachmentLine" else "$text$attachmentLine")
                            input = ""
                            pendingAttachments.clear()
                            if (text.isNotBlank() && attachmentsToSend.isEmpty() && handlePersonalOrPetMessage(text)) {
                                return@ChatScreen
                            }
                            scope.launch {
                                isDollyThinking = true
                                if (text.isNotBlank()) {
                                    val result = runCatching { client.chat(text, selectedChartId) }
                                    result.onSuccess { chatResult ->
                                        if (chatResult.activeChartId.isNotBlank()) {
                                            selectedChartId = chatResult.activeChartId
                                        }
                                        if (chatResult.activePatientDisplayName.isNotBlank()) {
                                            selectedChartName = chatResult.activePatientDisplayName
                                            status = "Active chart: ${chatResult.activePatientDisplayName}"
                                        }
                                        chatResult.packet?.takeIf { it.status.equals("ready", ignoreCase = true) }?.let { packet ->
                                            chartPackets[packetKey(packet.chartId, packet.packetType, packet.mode)] = packet
                                            persistChartPackets()
                                            chartHome = ChartRow(packet.chartId, packet.patientDisplayName, true)
                                            chartPacketTitle = packet.title
                                            chartPacketText = packet.body.ifBlank { packet.message }
                                            status = "${packet.title} saved for ${packet.patientDisplayName}."
                                        }
                                        messages += ChatMessage("Dolly", chatResult.reply)
                                        refreshVitaMastery(selectedChartId)
                                    }.onFailure {
                                        saveOfflineText(text)
                                        messages += ChatMessage(
                                            "Dolly",
                                            "I could not reach VitaMR desktop, so I saved this on the phone. I will send it when Dolly reconnects."
                                        )
                                        status = "Saved locally for later sync."
                                    }
                                }

                                if (attachmentsToSend.isNotEmpty()) {
                                    status = "Sending attachment${if (attachmentsToSend.size == 1) "" else "s"}..."
                                    var uploaded = 0
                                    var savedForLater = 0
                                    attachmentsToSend.forEach { attachment ->
                                        val sent = runCatching {
                                            if (attachment.kind == "voice") {
                                                client.captureRaw(attachment.uri, selectedChartId, attachment.kind, attachment.contentType, text)
                                            } else {
                                                client.capture(attachment.uri, selectedChartId, text)
                                            }
                                        }.onFailure {
                                            client.saveOfflineAttachment(
                                                attachment.uri,
                                                selectedChartId,
                                                selectedChartName,
                                                attachment.kind,
                                                attachment.contentType,
                                                text
                                            )?.let { saved ->
                                                offlineItems += saved
                                                persistOfflineItems()
                                                savedForLater++
                                            }
                                        }.isSuccess
                                        if (sent) uploaded++
                                    }
                                    status = when {
                                        uploaded == attachmentsToSend.size -> "Attachment${if (uploaded == 1) "" else "s"} sent."
                                        savedForLater > 0 -> "Saved $savedForLater attachment${if (savedForLater == 1) "" else "s"} locally for later sync."
                                        else -> "$uploaded of ${attachmentsToSend.size} attachments sent."
                                    }
                                    messages += ChatMessage(
                                        "Dolly",
                                        when {
                                            uploaded == attachmentsToSend.size ->
                                                "Got it. I sent ${if (uploaded == 1) "that attachment" else "those attachments"} to VitaMR desktop."
                                            savedForLater > 0 ->
                                                "VitaMR desktop was not reachable, so I saved ${if (savedForLater == 1) "that attachment" else "$savedForLater attachments"} on this phone. It will sync from Saved when Dolly reconnects."
                                            else ->
                                                "I sent $uploaded of ${attachmentsToSend.size} attachments. One may need another try."
                                        }
                                    )
                                }
                                isDollyThinking = false
                            }
                        },
                        onGallery = {
                            val error = activity?.openPhotoLibrary { uri ->
                                if (uri != null) {
                                    pendingAttachments += PendingAttachment(uri, "Photo from library", "gallery", "image/jpeg")
                                    status = "Photo attached."
                                } else {
                                    status = "Photo selection canceled."
                                }
                            } ?: "VitaMR could not open the phone photo library."
                            if (error != null) status = error
                        },
                        onCamera = {
                            val captureUri = createCameraImageUri(context)
                            val error = activity?.openCamera(captureUri) { uri ->
                                if (uri != null) {
                                    pendingAttachments += PendingAttachment(uri, "New photo", "camera", "image/jpeg")
                                    status = "Photo attached."
                                } else {
                                    status = "Camera capture canceled."
                                }
                            } ?: "VitaMR could not open the phone camera."
                            if (error != null) status = error
                        },
                        pendingAttachments = pendingAttachments,
                        onRemoveAttachment = { attachment -> pendingAttachments.remove(attachment) },
                        onSendAttachments = {
                            if (pendingAttachments.isNotEmpty()) {
                                val attachmentsToSend = pendingAttachments.toList()
                                val note = input.trim()
                                messages += ChatMessage(
                                    "You",
                                    if (note.isBlank()) {
                                        "Sent ${attachmentsToSend.size} attachment${if (attachmentsToSend.size == 1) "" else "s"}"
                                    } else {
                                        "$note (${attachmentsToSend.size} attachment${if (attachmentsToSend.size == 1) "" else "s"})"
                                    }
                                )
                                input = ""
                                pendingAttachments.clear()
                                scope.launch {
                                    isDollyThinking = true
                                    status = "Sending attachment${if (attachmentsToSend.size == 1) "" else "s"}..."
                                    var uploaded = 0
                                    var savedForLater = 0
                                    attachmentsToSend.forEach { attachment ->
                                        val sent = runCatching {
                                            if (attachment.kind == "voice") {
                                                client.captureRaw(attachment.uri, selectedChartId, attachment.kind, attachment.contentType, note)
                                            } else {
                                                client.capture(attachment.uri, selectedChartId, note)
                                            }
                                        }.onFailure {
                                            client.saveOfflineAttachment(
                                                attachment.uri,
                                                selectedChartId,
                                                selectedChartName,
                                                attachment.kind,
                                                attachment.contentType,
                                                note
                                            )?.let { saved ->
                                                offlineItems += saved
                                                persistOfflineItems()
                                                savedForLater++
                                            }
                                        }.isSuccess
                                        if (sent) uploaded++
                                    }
                                    status = when {
                                        uploaded == attachmentsToSend.size -> "Attachments sent."
                                        savedForLater > 0 -> "Saved $savedForLater attachment${if (savedForLater == 1) "" else "s"} locally for later sync."
                                        else -> "$uploaded of ${attachmentsToSend.size} attachments sent."
                                    }
                                    messages += ChatMessage(
                                        "Dolly",
                                        when {
                                            uploaded == attachmentsToSend.size ->
                                                "Got it. I sent the attachment to VitaMR desktop."
                                            savedForLater > 0 ->
                                                "VitaMR desktop was not reachable, so I saved ${if (savedForLater == 1) "that attachment" else "$savedForLater attachments"} on this phone. It will sync from Saved when Dolly reconnects."
                                            else ->
                                                "One attachment may need another try."
                                        }
                                    )
                                    isDollyThinking = false
                                }
                            }
                        }
                    )
                    "Charts" -> {
                        val record = selectedRecord
                        if (record != null) {
                            RecordReaderScreen(
                                packet = record,
                                onBack = { selectedRecord = null }
                            )
                            return@Surface
                        }
                        val openChart = chartHome
                        if (openChart == null) {
                            ChartsScreen(
                                charts = charts,
                                selectedChartId = selectedChartId,
                                onSelect = {
                                    resetChartReviewState(clearOpenChart = false)
                                    selectedChartId = it.chartId
                                    selectedChartName = it.displayName
                                    chartHome = it
                                    showVitaMasteryDetail = false
                                    status = "Active chart: ${it.displayName}"
                                    refreshVitaMastery(it.chartId)
                                }
                            )
                        } else {
                            if (!vitaMasteryByChart.containsKey(openChart.chartId)) {
                                refreshVitaMastery(openChart.chartId)
                            }
                            ChartHomeScreen(
                                chart = openChart,
                                packetTitle = chartPacketTitle,
                                packetText = chartPacketText,
                                savedPackets = chartPackets,
                                isLoading = isDollyThinking || isTravelSyncing,
                                travelSyncMessage = travelSyncMessage,
                                showNoteOptions = showNoteOptions,
                                showOtherRequest = showOtherRequest,
                                otherRequestText = otherRequestText,
                                onOtherRequestChange = { otherRequestText = it },
                                onOpenRecord = { selectedRecord = it },
                                onBack = {
                                    resetChartReviewState(clearOpenChart = true)
                                },
                                onSyncForTravel = {
                                    syncChartForTravel(openChart)
                                },
                                onOtherLoad = {
                                    val request = otherRequestText.trim()
                                    if (request.isBlank()) {
                                        status = "Tell VitaMR what else to load."
                                    } else {
                                        showOtherRequest = false
                                        scope.launch {
                                            isDollyThinking = true
                                            status = "Loading $request..."
                                            val result = runCatching { client.chartPacket(openChart.chartId, "other", request) }
                                            result.onSuccess { packet ->
                                                chartPackets[packetKey(packet.chartId, packet.packetType, packet.mode)] = packet
                                                persistChartPackets()
                                                chartPacketTitle = packet.title
                                                chartPacketText = packet.body.ifBlank { packet.message }
                                                selectedRecord = packet
                                                status = "${packet.title} saved."
                                            }.onFailure {
                                                status = "Could not load that record."
                                                chartPacketTitle = "Other"
                                                chartPacketText = "VitaMR desktop could not prepare that request: ${it.message}"
                                            }
                                            isDollyThinking = false
                                        }
                                    }
                                },
                                onPacket = { packet ->
                                    when (packet) {
                                        "Notes" -> {
                                            showNoteOptions = !showNoteOptions
                                            showOtherRequest = false
                                            chartPacketTitle = "Notes"
                                            chartPacketText = "Choose PCP, Cardiology, Psychiatry, or Recent notes to load a full-screen saved record."
                                        }
                                        "Other" -> {
                                            showOtherRequest = !showOtherRequest
                                            showNoteOptions = false
                                            chartPacketTitle = "Other"
                                            chartPacketText = "Ask for one or more records, like last echo, last three cardiology notes, CPET, or medication list."
                                        }
                                        "PCP Note", "Cardiology Note", "Psychiatry Note", "Recent Notes",
                                        "Vaccines", "Labs", "Imaging", "Questions" -> {
                                            showOtherRequest = false
                                            val requestType = when (packet) {
                                                "PCP Note", "Cardiology Note", "Psychiatry Note", "Recent Notes" -> "notes"
                                                else -> packet.lowercase()
                                            }
                                            val requestMode = when (packet) {
                                                "PCP Note" -> "pcp"
                                                "Cardiology Note" -> "cardiology"
                                                "Psychiatry Note" -> "psychiatry"
                                                "Recent Notes" -> "recent"
                                                "Labs" -> "latest"
                                                "Imaging" -> "recent"
                                                else -> "current"
                                            }
                                            val cachedPacket = chartPackets[packetKey(openChart.chartId, requestType, requestMode)]
                                            if (cachedPacket != null) {
                                                chartPacketTitle = cachedPacket.title
                                                chartPacketText = cachedPacket.body.ifBlank { cachedPacket.message }
                                                selectedRecord = cachedPacket
                                                status = "Showing saved ${cachedPacket.title}."
                                            }
                                            scope.launch {
                                                isDollyThinking = true
                                                if (cachedPacket == null) {
                                                    status = "Loading ${packet.lowercase()} for ${openChart.displayName}..."
                                                }
                                                val result = runCatching { client.chartPacket(openChart.chartId, requestType, requestMode) }
                                                result.onSuccess { packet ->
                                                    chartPackets[packetKey(packet.chartId, packet.packetType, packet.mode)] = packet
                                                    persistChartPackets()
                                                    val mergedCharts = mergeChartRows(
                                                        charts,
                                                        chartsFromPackets(chartPackets.values)
                                                    )
                                                    charts.clear()
                                                    charts.addAll(mergedCharts)
                                                    persistCharts()
                                                    chartPacketTitle = packet.title
                                                    chartPacketText = packet.body.ifBlank { packet.message }
                                                    selectedRecord = packet
                                                    status = "${packet.title} ready for ${openChart.displayName}."
                                                }.onFailure {
                                                    if (cachedPacket == null) {
                                                        chartPacketTitle = packet
                                                        chartPacketText = "No saved ${packet.lowercase()} record is on this phone yet. Load it once while VitaMR desktop is reachable, then it will travel with you."
                                                        status = "$packet unavailable offline."
                                                    } else {
                                                        status = "Offline copy shown. Desktop refresh unavailable."
                                                    }
                                                }
                                                isDollyThinking = false
                                            }
                                        }
                                        else -> {
                                            chartPacketTitle = packet
                                            chartPacketText = "$packet packets are coming next. Vaccines and Notes are wired first so we can validate the portable chart flow safely."
                                        }
                                    }
                                }
                            )
                        }
                    }
                    "Saved" -> SavedScreen(
                        offlineItems = offlineItems,
                        savedPackets = chartPackets.values.toList(),
                        onSyncNow = { syncPendingOfflineItems() },
                        onDelete = { item ->
                            offlineItems.removeAll { it.localId == item.localId }
                            persistOfflineItems()
                            status = "Saved item removed."
                        }
                    )
                    else -> SettingsScreen(
                        host = host,
                        status = status,
                        pairingCode = pairingCode,
                        onHostChange = {
                            host = it
                            client.host = it
                        },
                        onHealth = {
                            scope.launch {
                                status = "Checking VitaMR desktop..."
                                status = runCatching {
                                    val health = client.health()
                                    val becameReachable = markHostDetected(health)
                                    selectedChartId = health.activeChartId
                                    selectedChartName = health.activePatientDisplayName
                                    if (health.activeLifeMode.isNotBlank()) {
                                        setLifeMode(health.activeLifeMode, syncDesktop = false, announce = false)
                                    }
                                    refreshVitaMastery(health.activeChartId)
                                    if (client.isPaired() && health.activePatientDisplayName.isNotBlank()) {
                                        showDollyWelcomeForChart(health.activeChartId, health.activePatientDisplayName)
                                    }
                                    if (becameReachable) {
                                        announceHostDetectedAndSync("manual refresh")
                                    } else {
                                        syncPendingOfflineItems()
                                    }
                                    health.message
                                }.getOrElse { "Not connected: ${it.message}" }
                            }
                        },
                        onStartPairing = {
                            scope.launch { pairingCode = runCatching { client.startPairing() }.getOrElse { "Failed: ${it.message}" } }
                        },
                        onPair = { code ->
                            scope.launch {
                                status = runCatching {
                                    client.completePairing(code, "Android Phone")
                                    charts.clear()
                                    charts.addAll(client.charts())
                                    persistCharts()
                                    charts.firstOrNull { it.isActive }?.let {
                                        selectedChartId = it.chartId
                                        selectedChartName = it.displayName
                                        refreshVitaMastery(it.chartId)
                                        showDollyWelcomeForChart(it.chartId, it.displayName)
                                    }
                                    announceHostDetectedAndSync("pairing completed")
                                    "Paired"
                                }.getOrElse { "Pair failed: ${it.message}" }
                            }
                        },
                        isPaired = client.isPaired(),
                        biometricEnabled = biometricEnabled,
                        personalCloudEnabled = personalCloudEnabled,
                        personalGeminiKeyConfigured = personalGeminiKeyConfigured,
                        personalGeminiKeyInput = personalGeminiKeyInput,
                        personalCloudStatus = personalCloudStatus,
                        wirelessDebuggingEnabled = wirelessDebuggingEnabled,
                        onEnableBiometric = {
                            (context as? FragmentActivity)?.requestVitaMRBiometric(
                                title = "Enable manager biometric unlock",
                                subtitle = "Use Android face or fingerprint unlock before opening VitaMR.",
                                onResult = { success, message ->
                                    status = message
                                    if (success) {
                                        client.biometricEnabled = true
                                        biometricEnabled = true
                                        biometricUnlocked = true
                                    }
                                }
                            )
                        },
                        onDisableBiometric = {
                            client.biometricEnabled = false
                            biometricEnabled = false
                            biometricUnlocked = true
                            status = "Manager biometric unlock disabled."
                        },
                        onPersonalGeminiKeyChange = { personalGeminiKeyInput = it },
                        onSavePersonalGeminiKey = {
                            client.savePersonalGeminiKey(personalGeminiKeyInput)
                            personalGeminiKeyInput = ""
                            refreshPersonalCloudStatus()
                            status = "Personal Gemini key saved on this phone."
                        },
                        onClearPersonalGeminiKey = {
                            client.clearPersonalGeminiKey()
                            personalGeminiKeyInput = ""
                            refreshPersonalCloudStatus()
                            status = "Personal Gemini key cleared from this phone."
                        },
                        onTogglePersonalCloud = {
                            client.personalCloudEnabled = !client.personalCloudEnabled
                            refreshPersonalCloudStatus()
                            status = client.personalCloudLabel()
                        },
                        onRefreshWirelessDebugging = {
                            status = when (refreshWirelessDebuggingState()) {
                                true -> "Wireless debugging is on."
                                false -> "Wireless debugging is off."
                                null -> "Wireless debugging status is not available on this phone."
                            }
                        },
                        onOpenDeveloperOptions = {
                            val message = activity?.openDeveloperOptions()
                            status = message ?: "Open Developer Options, then turn on Wireless debugging."
                        }
                    )
                }
            }
        }
    }
}

@Composable
fun ChartAvatar(name: String, photo: Bitmap?, onClick: () -> Unit) {
    Surface(
        color = Color(0xFFE8DDF8),
        shape = CircleShape,
        shadowElevation = 2.dp,
        modifier = Modifier
            .padding(end = 14.dp)
            .size(46.dp)
            .clickable(onClick = onClick)
    ) {
        Box(contentAlignment = Alignment.Center) {
            if (photo != null) {
                Image(
                    bitmap = photo.asImageBitmap(),
                    contentDescription = "Chart photo",
                    contentScale = ContentScale.Crop,
                    modifier = Modifier.fillMaxSize().clip(CircleShape)
                )
            } else {
                Text(
                    initialsForAvatar(name),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFF3F2368)
                )
            }
        }
    }
}

private fun initialsForAvatar(name: String): String {
    val parts = name
        .trim()
        .split(Regex("\\s+"))
        .filter { it.isNotBlank() }
    if (parts.isEmpty()) return "V"
    return parts
        .take(2)
        .mapNotNull { it.firstOrNull()?.uppercaseChar()?.toString() }
        .joinToString("")
        .ifBlank { "V" }
}

@Composable
fun ChatScreen(
    messages: List<ChatMessage>,
    vitaMastery: VitaMastery?,
    dataHunterXpPop: DataHunterXpPop?,
    onDataHunterXpPopShown: (Long) -> Unit,
    onDataHunterTap: () -> Unit,
    onQuickReply: (String) -> Unit,
    showDataHunterQuickActions: Boolean,
    healthspanModeEnabled: Boolean,
    healthspanIntensity: String,
    activeLifeMode: String,
    personalCloudStatus: String,
    personalMemorySummaryStatus: String,
    hostDetected: Boolean,
    showHealthspanIntensityChoices: Boolean,
    onRecordsMode: () -> Unit,
    onHealthspanMode: () -> Unit,
    onMedicalMode: () -> Unit,
    onPersonalMode: () -> Unit,
    onLockboxMode: () -> Unit,
    onPetsMode: () -> Unit,
    onHealthspanIntensity: (String) -> Unit,
    isDollyThinking: Boolean,
    input: String,
    onInputChange: (String) -> Unit,
    onSend: () -> Unit,
    onGallery: () -> Unit,
    onCamera: () -> Unit,
    pendingAttachments: List<PendingAttachment>,
    onRemoveAttachment: (PendingAttachment) -> Unit,
    onSendAttachments: () -> Unit
) {
    val listState = rememberLazyListState()
    LaunchedEffect(messages.size, pendingAttachments.size, isDollyThinking) {
        if (messages.isNotEmpty() || isDollyThinking) {
            val targetIndex = if (isDollyThinking) messages.size else messages.lastIndex
            listState.animateScrollToItem(targetIndex)
        }
    }

    Column(Modifier.fillMaxSize().padding(horizontal = 12.dp, vertical = 8.dp)) {
        CompactHuntModeStrip(
            vitaMastery = vitaMastery,
            xpPop = dataHunterXpPop,
            onXpPopShown = onDataHunterXpPopShown,
            onDataHunterClick = onDataHunterTap,
            healthspanModeEnabled = healthspanModeEnabled,
            healthspanIntensity = healthspanIntensity,
            activeLifeMode = activeLifeMode,
            onRecords = onRecordsMode,
            onHealthspan = onHealthspanMode,
            onMedical = onMedicalMode,
            onPersonal = onPersonalMode,
            onLockbox = onLockboxMode,
            onPets = onPetsMode
        )
        Spacer(Modifier.height(8.dp))
        if (activeLifeMode == "Personal") {
            val routeStatus = if (hostDetected) {
                "Personal Host: desktop Dolly detected. Replies and sync prefer host."
            } else {
                "$personalCloudStatus. Non-sensitive Personal notes only."
            }
            Surface(
                color = if (hostDetected) Color(0xFFE6FAF4) else if (personalCloudStatus.contains("Flash-Lite")) Color(0xFFEFEAFF) else Color(0xFFFFF6DD),
                shape = RoundedCornerShape(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    "$routeStatus $personalMemorySummaryStatus",
                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = FontWeight.SemiBold,
                    color = Color(0xFF3E365A)
                )
            }
            Spacer(Modifier.height(8.dp))
        }
        LazyColumn(
            Modifier.weight(1f),
            state = listState,
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(messages) { message ->
                val isUser = message.author == "You"
                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = if (isUser) Color(0xFFD9F3EA) else Color(0xFFF0EAFE)
                    ),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Column(Modifier.padding(12.dp)) {
                        Text(
                            message.author,
                            fontWeight = FontWeight.Bold,
                            color = if (isUser) Color(0xFF086B5A) else Color(0xFF553187)
                        )
                        Text(message.text, color = Color(0xFF1F2933))
                    }
                }
            }
            if (isDollyThinking) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = Color(0xFFF0EAFE)),
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        Row(
                            Modifier.padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text("Dolly", fontWeight = FontWeight.Bold, color = Color(0xFF553187))
                            Spacer(Modifier.width(8.dp))
                            Text("is thinking...", color = Color(0xFF1F2933))
                        }
                    }
                }
            }
        }
        if (showDataHunterQuickActions) {
            Row(
                Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(top = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                listOf("Later", "Not sure", "Does not apply", "Pause").forEach { reply ->
                    AssistChip(
                        onClick = { onQuickReply(reply) },
                        label = { Text(reply, style = MaterialTheme.typography.labelSmall) }
                    )
                }
            }
            Spacer(Modifier.height(8.dp))
        }
        if (showHealthspanIntensityChoices) {
            Row(
                Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(top = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                listOf("Support", "Performance", "Experimental").forEach { mode ->
                    AssistChip(
                        onClick = { onHealthspanIntensity(mode) },
                        label = { Text(mode, style = MaterialTheme.typography.labelSmall) }
                    )
                }
            }
            Spacer(Modifier.height(8.dp))
        }
        if (pendingAttachments.isNotEmpty()) {
            Spacer(Modifier.height(6.dp))
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                pendingAttachments.forEach { attachment ->
                    Card(
                    colors = CardDefaults.cardColors(containerColor = Color(0xFFEAF3FF)),
                        shape = RoundedCornerShape(8.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(
                            Modifier.fillMaxWidth().padding(10.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(Modifier.weight(1f)) {
                                Text(attachment.label, fontWeight = FontWeight.SemiBold)
                                Text("Ready to send. Add a note or tap Send.", style = MaterialTheme.typography.bodySmall)
                            }
                            Button(onClick = onSendAttachments) { Text("Send") }
                            Spacer(Modifier.width(8.dp))
                            Button(onClick = { onRemoveAttachment(attachment) }) { Text("Remove") }
                        }
                    }
                }
            }
        }
        Spacer(Modifier.height(6.dp))
        Row(verticalAlignment = Alignment.Bottom) {
            Column(
                modifier = Modifier.width(42.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(2.dp)
            ) {
                IconButton(
                    onClick = onCamera,
                    modifier = Modifier.size(38.dp)
                ) {
                    Icon(Icons.Default.PhotoCamera, contentDescription = "Take photo")
                }
                IconButton(
                    onClick = onGallery,
                    modifier = Modifier.size(38.dp)
                ) {
                    Icon(Icons.Default.AttachFile, contentDescription = "Attach")
                }
            }
            OutlinedTextField(
                value = input,
                onValueChange = onInputChange,
                modifier = Modifier.weight(1f),
                placeholder = { Text("Ask Dolly...") },
                minLines = 2,
                maxLines = 6
            )
            IconButton(onClick = onSend) { Icon(Icons.Default.Send, contentDescription = "Send") }
        }
    }
}

@Composable
fun ChartsScreen(
    charts: List<ChartRow>,
    selectedChartId: String,
    onSelect: (ChartRow) -> Unit
) {
    LazyColumn(Modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        if (charts.isEmpty()) {
            item {
                Card(
                    colors = CardDefaults.cardColors(containerColor = Color(0xFFFFF6DD)),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text("No saved chart data on this phone yet.", fontWeight = FontWeight.Bold)
                        Text("Open chart packets while VitaMR desktop is reachable. Saved packets will stay readable here when you leave Wi-Fi.")
                    }
                }
            }
        }
        items(charts) { chart ->
            Card(onClick = { onSelect(chart) }, modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text(chart.displayName, fontWeight = FontWeight.Bold)
                        Text("Saved chart")
                    }
                    if (selectedChartId == chart.chartId || chart.isActive) Text("Active")
                }
            }
        }
    }
}

@Composable
fun ChartHomeScreen(
    chart: ChartRow,
    packetTitle: String,
    packetText: String,
    savedPackets: Map<String, ChartPacket>,
    isLoading: Boolean,
    travelSyncMessage: String,
    showNoteOptions: Boolean,
    showOtherRequest: Boolean,
    otherRequestText: String,
    onOtherRequestChange: (String) -> Unit,
    onOpenRecord: (ChartPacket) -> Unit,
    onBack: () -> Unit,
    onSyncForTravel: () -> Unit,
    onOtherLoad: () -> Unit,
    onPacket: (String) -> Unit
) {
    Column(
        Modifier.fillMaxSize().padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Button(onClick = onBack) { Text("Charts") }
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(chart.displayName, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                Text("Portable chart packets", style = MaterialTheme.typography.bodyMedium)
            }
        }

        Button(
            onClick = onSyncForTravel,
            enabled = !isLoading,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text(if (isLoading) "Syncing..." else "Sync for Travel")
        }

        if (travelSyncMessage.isNotBlank()) {
            Surface(
                color = Color(0xFFEAF9F5),
                shape = RoundedCornerShape(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    travelSyncMessage,
                    color = Color(0xFF264B42),
                    style = MaterialTheme.typography.bodySmall,
                    modifier = Modifier.padding(10.dp)
                )
            }
        }

        if (!showNoteOptions && !showOtherRequest) {
            val packetButtons = listOf("Labs", "Imaging", "Notes", "Vaccines", "Questions", "Other")
            packetButtons.chunked(2).forEach { row ->
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                    row.forEach { packet ->
                        val isSaved = hasSavedPacket(savedPackets, chart.chartId, packet.lowercase())
                        Button(
                            onClick = { onPacket(packet) },
                            modifier = Modifier.weight(1f)
                        ) {
                            Text(if (isSaved) "$packet saved" else packet)
                        }
                    }
                    if (row.size == 1) {
                        Spacer(Modifier.weight(1f))
                    }
                }
            }
        }

        if (showNoteOptions) {
            Text("Choose a note", fontWeight = FontWeight.Bold, modifier = Modifier.fillMaxWidth())
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                listOf("PCP Note", "Cardiology Note").forEach { option ->
                    Button(onClick = { onPacket(option) }, modifier = Modifier.weight(1f)) { Text(option) }
                }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                listOf("Psychiatry Note", "Recent Notes").forEach { option ->
                    Button(onClick = { onPacket(option) }, modifier = Modifier.weight(1f)) { Text(option) }
                }
            }
        }

        if (showOtherRequest) {
            Text("Other records", fontWeight = FontWeight.Bold, modifier = Modifier.fillMaxWidth())
            Card(
                colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBF0)),
                shape = RoundedCornerShape(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(
                        value = otherRequestText,
                        onValueChange = onOtherRequestChange,
                        modifier = Modifier.fillMaxWidth(),
                        label = { Text("Other record request") },
                        placeholder = { Text("Last echo, CPET, last 3 cardiology notes...") },
                        minLines = 2
                    )
                    Button(onClick = onOtherLoad, enabled = !isLoading, modifier = Modifier.fillMaxWidth()) {
                        Text("Load Requested Records")
                    }
                }
            }
        }

        val shouldShowRecords = showNoteOptions || showOtherRequest || packetText.isNotBlank()
        val records = if (shouldShowRecords) recordsForChart(savedPackets, chart.chartId) else emptyList()
        LazyColumn(
            Modifier.fillMaxWidth().weight(1f),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            if (!shouldShowRecords) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = Color(0xFFFCFDF9)),
                        shape = RoundedCornerShape(8.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            "Choose a packet or Sync for Travel. Saved records stay on this phone for offline reading.",
                            modifier = Modifier.padding(14.dp)
                        )
                    }
                }
            } else if (records.isEmpty()) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = Color(0xFFFCFDF9)),
                        shape = RoundedCornerShape(8.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            when {
                                isLoading -> "Dolly is loading this record..."
                                packetText.isNotBlank() -> packetText
                                else -> "Tap Sync for Travel or load a record. Saved records will appear here as a local library for future phone-agent search."
                            },
                            modifier = Modifier.padding(14.dp)
                        )
                    }
                }
            }

            if (packetText.isNotBlank() && records.isNotEmpty()) {
                item {
                    Text(
                        packetText,
                        style = MaterialTheme.typography.bodySmall,
                        color = Color(0xFF4B5A66),
                        modifier = Modifier.padding(horizontal = 4.dp)
                    )
                }
            }

            items(records) { record ->
                Card(
                    onClick = { onOpenRecord(record) },
                    colors = CardDefaults.cardColors(containerColor = Color.White),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(record.title.ifBlank { record.packetType }, fontWeight = FontWeight.Bold)
                        Text(
                            "${record.packetType.replaceFirstChar { it.uppercase() }} - ${record.mode.ifBlank { "current" }}",
                            style = MaterialTheme.typography.bodySmall,
                            color = Color(0xFF5E6A72)
                        )
                        if (record.createdAt.isNotBlank()) {
                            Text(
                                "Synced ${record.createdAt}",
                                style = MaterialTheme.typography.bodySmall,
                                color = Color(0xFF7A858C)
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun RecordReaderScreen(packet: ChartPacket, onBack: () -> Unit) {
    Column(Modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Button(onClick = onBack) { Text("Back") }
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(packet.title.ifBlank { "Saved record" }, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                Text(packet.patientDisplayName, style = MaterialTheme.typography.bodyMedium, color = Color(0xFF52635E))
            }
        }
        Card(
            colors = CardDefaults.cardColors(containerColor = Color(0xFFFCFDF9)),
            shape = RoundedCornerShape(8.dp),
            modifier = Modifier.fillMaxWidth().weight(1f)
        ) {
            PacketReader(
                title = packet.title.ifBlank { packet.packetType },
                body = packet.body.ifBlank { packet.message },
                modifier = Modifier.fillMaxSize()
            )
        }
    }
}

@Composable
fun CompactHuntModeStrip(
    vitaMastery: VitaMastery?,
    xpPop: DataHunterXpPop?,
    onXpPopShown: (Long) -> Unit,
    onDataHunterClick: () -> Unit,
    healthspanModeEnabled: Boolean,
    healthspanIntensity: String,
    activeLifeMode: String,
    onRecords: () -> Unit,
    onHealthspan: () -> Unit,
    onMedical: () -> Unit,
    onPersonal: () -> Unit,
    onLockbox: () -> Unit,
    onPets: () -> Unit
) {
    LaunchedEffect(xpPop?.id) {
        val pop = xpPop ?: return@LaunchedEffect
        delay(950)
        onXpPopShown(pop.id)
    }

    Card(
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.fillMaxWidth().height(104.dp)) {
        Row(
            Modifier.padding(start = 10.dp, top = 8.dp, end = 10.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier
                    .weight(1f)
                    .clickable(onClick = onDataHunterClick),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Surface(
                    color = Color(0xFFE6FAF4),
                    shape = CircleShape,
                    modifier = Modifier.size(30.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text("+", color = Color(0xFF087A66), fontWeight = FontWeight.Bold)
                    }
                }
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            "${vitaMastery?.dataHunterTitle?.ifBlank { "Data Hunter" } ?: "Data Hunter"} | ${vitaMastery?.dataHunterStage?.ifBlank { "Basic" } ?: "Basic"}",
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFF1F2933),
                            modifier = Modifier.weight(1f)
                        )
                        Text(
                            "${vitaMastery?.dataHunterPercent ?: 0}%",
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFF1F2933)
                        )
                    }
                }
            }

            CompactModeToggle(
                healthspanEnabled = healthspanModeEnabled,
                healthspanIntensity = healthspanIntensity,
                onRecords = onRecords,
                onHealthspan = onHealthspan,
                modifier = Modifier.width(165.dp)
            )
        }
        DataHunterMiniProgressLine(
            percent = vitaMastery?.dataHunterPercent ?: 0,
            modifier = Modifier.padding(start = 10.dp, end = 10.dp, bottom = 6.dp)
        )
        LifeModeToggle(
            activeMode = activeLifeMode,
            onMedical = onMedical,
            onPersonal = onPersonal,
            onLockbox = onLockbox,
            onPets = onPets,
            modifier = Modifier.padding(start = 10.dp, end = 10.dp, bottom = 8.dp)
        )
        }
    }
}

@Composable
fun LifeModeToggle(
    activeMode: String,
    onMedical: () -> Unit,
    onPersonal: () -> Unit,
    onLockbox: () -> Unit,
    onPets: () -> Unit,
    modifier: Modifier = Modifier
) {
    Surface(
        color = Color(0xFFFFF7F3),
        shape = RoundedCornerShape(8.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFFF0D2C7)),
        modifier = modifier.fillMaxWidth()
    ) {
        Row(Modifier.padding(3.dp), verticalAlignment = Alignment.CenterVertically) {
            CompactModeSegment(
                label = "Medical",
                selected = activeMode == "Medical",
                onClick = onMedical,
                modifier = Modifier.weight(1f),
                selectedColor = Color(0xFFFFD1C7),
                selectedTextColor = Color(0xFF4F1F18),
                unselectedTextColor = Color(0xFF6B4A45)
            )
            CompactModeSegment(
                label = "Personal",
                selected = activeMode == "Personal",
                onClick = onPersonal,
                modifier = Modifier.weight(1f),
                selectedColor = Color(0xFFDED7FF),
                selectedTextColor = Color(0xFF30245F),
                unselectedTextColor = Color(0xFF554A72)
            )
            CompactModeSegment(
                label = "Lockbox",
                selected = activeMode == "Lockbox",
                onClick = onLockbox,
                modifier = Modifier.weight(1f),
                selectedColor = Color(0xFFD7E6F7),
                selectedTextColor = Color(0xFF1D3F63),
                unselectedTextColor = Color(0xFF24405F)
            )
            CompactModeSegment(
                label = "Pets",
                selected = activeMode == "Pets",
                onClick = onPets,
                modifier = Modifier.weight(1f),
                selectedColor = Color(0xFFFFE2A8),
                selectedTextColor = Color(0xFF4C3308),
                unselectedTextColor = Color(0xFF67512A)
            )
        }
    }
}

@Composable
fun DataHunterMiniProgressLine(percent: Int, modifier: Modifier = Modifier) {
    val progress = (percent.coerceIn(0, 100)) / 100f
    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(6.dp)
            .clip(RoundedCornerShape(8.dp))
            .background(Color(0xFFE5DDF4))
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth(progress)
                .height(6.dp)
                .background(Color(0xFFA7F2EA))
        )
    }
}

@Composable
fun CompactModeToggle(
    healthspanEnabled: Boolean,
    healthspanIntensity: String,
    onRecords: () -> Unit,
    onHealthspan: () -> Unit,
    modifier: Modifier = Modifier
) {
    Surface(
        color = Color(0xFFF6FAFC),
        shape = RoundedCornerShape(8.dp),
        border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFFD6E4EF)),
        modifier = modifier
    ) {
        Row(Modifier.padding(3.dp), verticalAlignment = Alignment.CenterVertically) {
            CompactModeSegment(
                label = "Records",
                selected = !healthspanEnabled,
                onClick = onRecords,
                modifier = Modifier.weight(1f)
            )
            CompactModeSegment(
                label = "Healthspan",
                selected = healthspanEnabled,
                onClick = onHealthspan,
                modifier = Modifier.weight(1f),
                starColor = healthspanStarColor(healthspanIntensity),
                showStar = healthspanEnabled
            )
        }
    }
}

@Composable
fun CompactModeSegment(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    starColor: Color = Color.Transparent,
    showStar: Boolean = false,
    selectedColor: Color = Color(0xFFA7F2EA),
    selectedTextColor: Color = Color(0xFF063B32),
    unselectedTextColor: Color = Color(0xFF52635E)
) {
    Surface(
        color = if (selected) selectedColor else Color.Transparent,
        shape = RoundedCornerShape(8.dp),
        shadowElevation = if (selected) 2.dp else 0.dp,
        modifier = modifier
            .height(32.dp)
            .clickable(onClick = onClick)
    ) {
        Box(
            modifier = Modifier.padding(horizontal = 7.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                label,
                style = MaterialTheme.typography.labelMedium,
                fontWeight = if (selected) FontWeight.Bold else FontWeight.SemiBold,
                color = if (selected) selectedTextColor else unselectedTextColor
            )
            if (showStar) {
                Surface(
                    color = Color.White,
                    shape = CircleShape,
                    shadowElevation = 2.dp,
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(top = (-2).dp, end = (-2).dp)
                        .size(20.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(
                            "★",
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = starColor
                        )
                    }
                }
            }
        }
    }
}

private fun healthspanStarColor(healthspanIntensity: String): Color =
    when (healthspanIntensity) {
        "Performance" -> Color(0xFFC0C7D1)
        "Experimental" -> Color(0xFFD9A441)
        else -> Color(0xFF2B7FFF)
    }

@Composable
fun DataHunterHealthBar(
    vitaMastery: VitaMastery?,
    xpPop: DataHunterXpPop?,
    onXpPopShown: (Long) -> Unit,
    onClick: () -> Unit
) {
    LaunchedEffect(xpPop?.id) {
        val pop = xpPop ?: return@LaunchedEffect
        delay(1100)
        onXpPopShown(pop.id)
    }

    Card(
        onClick = onClick,
        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFF8E6)),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(
            defaultElevation = 8.dp,
            pressedElevation = 10.dp
        ),
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 2.dp, vertical = 2.dp)
    ) {
        Box {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        "${vitaMastery?.dataHunterTitle?.ifBlank { "Data Hunter" } ?: "Data Hunter"}: ${vitaMastery?.dataHunterPercent ?: 0}% | ${vitaMastery?.dataHunterStage?.ifBlank { "Basic" } ?: "Basic"}",
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Bold,
                        color = Color(0xFF1F2933),
                        modifier = Modifier.weight(1f)
                    )
                    val xp = vitaMastery?.dataHunterQuestXp ?: 0
                    val status = vitaMastery?.dataHunterQuestStatus.orEmpty()
                    Text(
                        if (xp > 0) "+$xp XP" else status.ifBlank { "Ready" },
                        style = MaterialTheme.typography.bodySmall,
                        color = Color(0xFF5D4214)
                    )
                }
                DataHunterProgressBar(percent = vitaMastery?.dataHunterPercent ?: 0)
                Text(
                    vitaMastery?.dataHunterQuestion?.ifBlank { "Sync Data Hunter from desktop." } ?: "Sync Data Hunter from desktop.",
                    style = MaterialTheme.typography.bodySmall,
                    color = Color(0xFF5D4214)
                )
                val meta = listOfNotNull(
                    vitaMastery?.dataHunterQuestCategory?.takeIf { it.isNotBlank() },
                    vitaMastery?.dataHunterQuestStatus?.takeIf { it.isNotBlank() }
                ).joinToString(" - ")
                if (meta.isNotBlank()) {
                    Text(
                        meta,
                        style = MaterialTheme.typography.labelSmall,
                        color = Color(0xFF087A66)
                    )
                }
                val masterLine = vitaMastery?.let {
                    val target = it.dataHunterMasterTarget.ifBlank { "Master evidence target" }
                    "Master: ${it.dataHunterMasterAcceptedCount} accepted / ${it.dataHunterMasterPendingCount} pending - $target"
                }.orEmpty()
                if (masterLine.isNotBlank()) {
                    Text(
                        masterLine,
                        style = MaterialTheme.typography.labelSmall,
                        color = Color(0xFF36515F)
                    )
                }
            }
            if (xpPop != null) {
                Surface(
                    color = Color(0xFF14F1B2),
                    shape = RoundedCornerShape(8.dp),
                    shadowElevation = 4.dp,
                    modifier = Modifier.align(Alignment.TopEnd).padding(top = 5.dp, end = 10.dp)
                ) {
                    Text(
                        "+${xpPop?.points ?: 0} XP",
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Bold,
                        color = Color(0xFF063B32),
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 5.dp)
                    )
                }
            }
        }
    }
}

@Composable
fun DataHunterProgressBar(percent: Int) {
    val progress = (percent.coerceIn(0, 100)) / 100f
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(6.dp)
            .clip(RoundedCornerShape(8.dp))
            .background(Color(0xFFDCE5EA))
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth(progress)
                .height(6.dp)
                .background(Color(0xFF14F1B2))
        )
    }
}

@Composable
fun DataHunterMiniCard(vitaMastery: VitaMastery?) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFF8E6)),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 2.dp)
    ) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    vitaMastery?.dataHunterTitle?.ifBlank { "Data Hunter" } ?: "Data Hunter",
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFF5D4214),
                    modifier = Modifier.weight(1f)
                )
                Text(
                    "${vitaMastery?.dataHunterStage?.ifBlank { "Basic" } ?: "Basic"} ${vitaMastery?.dataHunterPercent ?: 0}%",
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFF087A66)
                )
            }
            Text(
                vitaMastery?.dataHunterQuestion.orEmpty(),
                style = MaterialTheme.typography.bodySmall,
                color = Color(0xFF5D4214)
            )
            val meta = listOfNotNull(
                vitaMastery?.dataHunterQuestCategory?.takeIf { it.isNotBlank() },
                vitaMastery?.dataHunterQuestStatus?.takeIf { it.isNotBlank() },
                vitaMastery?.dataHunterQuestXp?.takeIf { it > 0 }?.let { "+$it XP" }
            ).joinToString(" - ")
            if (meta.isNotBlank()) {
                Text(
                    meta,
                    style = MaterialTheme.typography.labelSmall,
                    color = Color(0xFF087A66)
                )
            }
        }
    }
}

@Composable
fun VitaMasteryCard(vitaMastery: VitaMastery?, onClick: () -> Unit) {
    Card(
        onClick = onClick,
        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBF8)),
        shape = RoundedCornerShape(24.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = vitaMastery?.currentStage?.ifBlank { "Basic" } ?: "Vita Mastery",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = if (vitaMastery == null) {
                            "Syncing with desktop"
                        } else {
                            "${vitaMastery.percentComplete}% Vita Mastery - ${vitaMastery.earnedPoints}/${vitaMastery.possiblePoints} points"
                        },
                        style = MaterialTheme.typography.bodySmall
                    )
                }
                Text("${vitaMastery?.percentComplete ?: 0}%", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
            }

            androidx.compose.material3.LinearProgressIndicator(
                progress = { ((vitaMastery?.percentComplete ?: 0).coerceIn(0, 100)) / 100f },
                modifier = Modifier.fillMaxWidth().height(12.dp).clip(RoundedCornerShape(8.dp)),
                color = Color(0xFF10CDAB),
                trackColor = Color(0xFFD7E7E1)
            )

            VitaMasteryClimb(percent = vitaMastery?.percentComplete ?: 0)

            val quests = vitaMastery?.activeMicroQuests.orEmpty()
            if (quests.isNotEmpty()) {
                Text("Collect these next", style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.SemiBold)
                quests.take(3).forEach { quest ->
                    QuestCollectibleCard(quest)
                }
            }
        }
    }
}

@Composable
fun VitaMasteryDetailScreen(
    chart: ChartRow,
    vitaMastery: VitaMastery?,
    onBack: () -> Unit
) {
    LazyColumn(
        Modifier.fillMaxSize().padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Button(onClick = onBack) { Text("Chart") }
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text("Vita Mastery", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                    Text(chart.displayName, style = MaterialTheme.typography.bodyMedium, color = Color(0xFF52635E))
                }
            }
        }

        item {
            VitaMasteryProgressSummary(vitaMastery = vitaMastery)
        }

        item {
            DataHunterRewardRulesCard()
        }

        item {
            DataHunterMasterStateCard(vitaMastery = vitaMastery)
        }

        item {
            Text("Your climb", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(8.dp))
            VitaMasteryClimb(percent = vitaMastery?.percentComplete ?: 0)
        }

        item {
            Text("Active quests", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        }

        val quests = vitaMastery?.activeMicroQuests.orEmpty()
        if (quests.isEmpty()) {
            item {
                Surface(
                    modifier = Modifier.fillMaxWidth(),
                    color = Color(0xFFF2F6F4),
                    shape = RoundedCornerShape(18.dp)
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text("No active quests yet", fontWeight = FontWeight.Bold)
                        Text(
                            "VitaMR is waiting for the desktop chart to send the next data goals.",
                            style = MaterialTheme.typography.bodySmall,
                            color = Color(0xFF52635E)
                        )
                    }
                }
            }
        } else {
            items(quests.take(6)) { quest ->
                QuestCollectibleCard(quest)
            }
        }
    }
}

@Composable
fun DataHunterMasterStateCard(vitaMastery: VitaMastery?) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFFFF)),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
            Text("Master Hunt", fontWeight = FontWeight.Bold, color = Color(0xFF1F2933))
            Text(
                "${vitaMastery?.dataHunterMasterAcceptedCount ?: 0} accepted evidence / ${vitaMastery?.dataHunterMasterPendingCount ?: 0} pending",
                style = MaterialTheme.typography.bodySmall,
                fontWeight = FontWeight.SemiBold,
                color = Color(0xFF087A66)
            )
            val target = vitaMastery?.dataHunterMasterTarget.orEmpty()
            if (target.isNotBlank()) {
                Text(
                    target,
                    style = MaterialTheme.typography.bodySmall,
                    color = Color(0xFF1F2933)
                )
            }
            val detail = vitaMastery?.dataHunterMasterTargetDetail.orEmpty()
            if (detail.isNotBlank()) {
                Text(
                    detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = Color(0xFF52635E)
                )
            }
        }
    }
}

@Composable
fun DataHunterRewardRulesCard() {
    Card(
        colors = CardDefaults.cardColors(containerColor = Color(0xFFF8FBFC)),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(7.dp)) {
            Text("How XP works", fontWeight = FontWeight.Bold, color = Color(0xFF1F2933))
            Text(
                "Basic answers add context XP. Category bonuses mark a completed Basic area. Master Hunt XP starts when a source is accepted into the vault.",
                style = MaterialTheme.typography.bodySmall,
                color = Color(0xFF52635E)
            )
            Text(
                "Later, not sure, and does not apply are tracked without inflating evidence progress.",
                style = MaterialTheme.typography.bodySmall,
                color = Color(0xFF52635E)
            )
            Text(
                "User memory provides context. Accepted records provide evidence.",
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.SemiBold,
                color = Color(0xFF087A66)
            )
        }
    }
}

@Composable
fun PhoneHealthspanModeCard(
    healthspanEnabled: Boolean,
    healthspanIntensity: String,
    onRecords: () -> Unit,
    onHealthspan: () -> Unit,
    onIntensity: (String) -> Unit
) {
    val starColor = when (healthspanIntensity) {
        "Performance" -> Color(0xFFC0C7D1)
        "Experimental" -> Color(0xFFD9A441)
        else -> Color(0xFF2B7FFF)
    }
    Card(
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(8.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Box {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Mode", fontWeight = FontWeight.Bold, color = Color(0xFF1F2933))
            Surface(
                color = Color(0xFFF6FAFC),
                shape = RoundedCornerShape(8.dp),
                border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFFD6E4EF)),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(Modifier.padding(3.dp), verticalAlignment = Alignment.CenterVertically) {
                    PhoneModeSegment(
                        label = "Records",
                        selected = !healthspanEnabled,
                        onClick = onRecords,
                        modifier = Modifier.weight(1f)
                    )
                    PhoneModeSegment(
                        label = "Healthspan",
                        selected = healthspanEnabled,
                        onClick = onHealthspan,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    color = Color(0xFFE6FAF4),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.width(22.dp).height(22.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text("*", color = Color(0xFF087A66), fontWeight = FontWeight.Bold)
                    }
                }
                Spacer(Modifier.width(8.dp))
                Text(
                    if (healthspanEnabled) {
                        when (healthspanIntensity) {
                            "Performance" -> "Dolly can add clearer accountability for ambitious healthspan goals."
                            "Experimental" -> "Dolly can track advanced research-aware ideas, separate from medical advice."
                            else -> "Dolly can use records and goals to guide healthspan optimization."
                        }
                    } else {
                        "Dolly focuses on organizing, searching, and protecting records."
                    },
                    style = MaterialTheme.typography.bodySmall,
                    color = Color(0xFF52635E),
                    modifier = Modifier.weight(1f)
                )
            }
        }
        if (healthspanEnabled) {
            Surface(
                color = Color.White,
                shape = RoundedCornerShape(18.dp),
                shadowElevation = 5.dp,
                modifier = Modifier.align(Alignment.TopEnd).padding(top = 34.dp, end = 1.dp)
            ) {
                Text(
                    "★",
                    color = starColor,
                    fontWeight = FontWeight.Bold,
                    fontSize = MaterialTheme.typography.titleLarge.fontSize,
                    modifier = Modifier.padding(horizontal = 7.dp, vertical = 3.dp)
                )
            }
        }
        }
    }
}

@Composable
fun PhoneModeSegment(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Surface(
        color = if (selected) Color(0xFFA7F2EA) else Color.Transparent,
        shape = RoundedCornerShape(8.dp),
        shadowElevation = if (selected) 2.dp else 0.dp,
        modifier = modifier
            .height(28.dp)
            .clickable(onClick = onClick)
    ) {
        Box(
            modifier = Modifier.padding(horizontal = 9.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                label,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = if (selected) FontWeight.Bold else FontWeight.SemiBold,
                maxLines = 1,
                color = if (selected) Color(0xFF111827) else Color(0xFF52635E)
            )
        }
    }
}

@Composable
fun VitaMasteryProgressSummary(vitaMastery: VitaMastery?) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color(0xFFFFFBF8),
        shape = RoundedCornerShape(24.dp),
        tonalElevation = 1.dp
    ) {
        Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(
                        vitaMastery?.currentStage?.ifBlank { "Basic" } ?: "Vita Mastery",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        if (vitaMastery == null) {
                            "Syncing with desktop"
                        } else {
                            "${vitaMastery.earnedPoints}/${vitaMastery.possiblePoints} points collected"
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = Color(0xFF52635E)
                    )
                }
                Text(
                    "${vitaMastery?.percentComplete ?: 0}%",
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFF087A66)
                )
            }
        }
    }
}

@Composable
fun VitaMasteryClimb(percent: Int) {
    val levels = listOf("Basic", "Master", "Legendary")
    val currentIndex = when {
        percent >= 80 -> 2
        percent >= 40 -> 1
        else -> 0
    }

    Row(
        Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.Bottom
    ) {
        levels.forEachIndexed { index, level ->
            val isCurrent = index == currentIndex
            val isComplete = index < currentIndex
            val height = when (index) {
                0 -> 64.dp
                1 -> 84.dp
                else -> 104.dp
            }
            Box(
                modifier = Modifier.weight(1f).height(height),
                contentAlignment = Alignment.TopCenter
            ) {
                if (isCurrent) {
                    Surface(
                        color = Color(0xFF0E7C68),
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Text(
                            "You",
                            color = Color.White,
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        )
                    }
                }
                Surface(
                    modifier = Modifier.align(Alignment.BottomCenter).fillMaxWidth().height(38.dp),
                    color = when {
                        isCurrent -> Color(0xFFE5FBF5)
                        isComplete -> Color(0xFFD6F5EB)
                        else -> Color(0xFFF0F2F4)
                    },
                    shape = RoundedCornerShape(18.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(level, fontWeight = if (isCurrent) FontWeight.Bold else FontWeight.SemiBold)
                    }
                }
            }
        }
    }
}
@Composable
fun QuestCollectibleCard(quest: VitaMasteryQuest) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = Color.White,
        shape = RoundedCornerShape(18.dp),
        tonalElevation = 1.dp
    ) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(
                color = Color(0xFFE6FAF4),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier.width(48.dp).height(48.dp)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text("+${quest.points}", fontWeight = FontWeight.Bold, color = Color(0xFF087A66))
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(quest.title, fontWeight = FontWeight.SemiBold)
                Text(quest.evidenceHint, style = MaterialTheme.typography.bodySmall, color = Color(0xFF5F6B76))
            }
        }
    }
}

@Composable
fun PacketReader(title: String, body: String, modifier: Modifier = Modifier) {
    val sections = body
        .split("\n## ")
        .map { it.trim() }
        .filter { it.isNotBlank() }

    Column(modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
        HorizontalDivider(color = Color(0xFFD7E2EA))
        SelectionContainer {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(sections) { section ->
                    val normalized = if (section.startsWith("## ")) section else section
                    val lines = normalized.lines()
                    val heading = lines.firstOrNull()?.removePrefix("## ") ?: ""
                    val details = lines.drop(1).joinToString("\n").trim()
                    Column(
                        Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(5.dp)
                    ) {
                        if (heading.isNotBlank() && sections.size > 1) {
                            Text(heading, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                        }
                        Text(
                            text = details.ifBlank { normalized },
                            style = MaterialTheme.typography.bodyMedium,
                            color = Color(0xFF26343D)
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun SavedScreen(
    offlineItems: List<OfflineItem>,
    savedPackets: List<ChartPacket>,
    onSyncNow: () -> Unit,
    onDelete: (OfflineItem) -> Unit
) {
    val localNotes = offlineItems.filter {
        it.status == "local_only" ||
            it.status == "personal_vault_synced" ||
            it.status == "lockbox_vault_synced" ||
            it.status == "pet_vault_synced"
    }
    val syncItems = offlineItems.filter { item -> localNotes.none { it.localId == item.localId } }
    val personalNotes = localNotes.filter { it.kind == "personal_text" }
    val lockboxNotes = localNotes.filter { it.kind == "lockbox_note" }
    val petNotes = localNotes.filter { it.kind == "pet_note" }
    val pendingItems = syncItems.count { it.status == "pending" }
    val syncedItems = syncItems.count { it.status == "synced" || it.status == "sent_to_dolly" }
    val patientCount = savedPackets
        .map { it.patientDisplayName.ifBlank { it.chartId } }
        .filter { it.isNotBlank() }
        .distinct()
        .count()
    val latestSync = latestPacketSyncAt(savedPackets)
    val freshnessLine = when {
        savedPackets.isEmpty() -> "No offline chart records saved yet."
        latestSync.isBlank() -> "Offline chart records are saved, but no sync time was provided."
        isOlderThanDays(latestSync, 14) -> "Older offline copy. Refresh before relying on it for travel."
        else -> "Latest chart refresh: $latestSync"
    }
    LazyColumn(
        Modifier.fillMaxSize().padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Saved for later", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                    Text("Medical outbox syncs to desktop. Personal, Lockbox, and pet notes stay in separate lanes.")
                }
                Button(onClick = onSyncNow) { Text("Sync now") }
            }
        }

        item {
            Card(
                colors = CardDefaults.cardColors(containerColor = Color(0xFFEFF7FF)),
                shape = RoundedCornerShape(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text("Sync status", fontWeight = FontWeight.Bold)
                    Text("$patientCount patient${if (patientCount == 1) "" else "s"} with offline chart records.")
                    Text("${savedPackets.size} saved chart record${if (savedPackets.size == 1) "" else "s"} on this phone.")
                    Text("$pendingItems phone item${if (pendingItems == 1) "" else "s"} waiting to sync.")
                    Text("${localNotes.size} personal/lockbox/pet note${if (localNotes.size == 1) "" else "s"} kept separate.")
                    if (syncedItems > 0) {
                        Text("$syncedItems phone item${if (syncedItems == 1) "" else "s"} already sent.")
                    }
                    Text(freshnessLine, fontWeight = FontWeight.SemiBold)
                }
            }
        }

        if (personalNotes.isNotEmpty()) {
            item {
                Text("Personal", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            }
            items(personalNotes) { item ->
                LocalMemoryCard(
                    item = item,
                    title = "Personal note",
                    description = "Local-only. Not in the medical chart.",
                    containerColor = Color(0xFFF4ECFF),
                    onDelete = onDelete
                )
            }
        }

        if (lockboxNotes.isNotEmpty()) {
            item {
                Text("Lockbox", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            }
            items(lockboxNotes) { item ->
                LocalMemoryCard(
                    item = item,
                    title = "Lockbox note",
                    description = "Sensitive lane. No Gemini or medical chart write.",
                    containerColor = Color(0xFFEAF1FA),
                    onDelete = onDelete
                )
            }
        }

        if (petNotes.isNotEmpty()) {
            item {
                Text("Pets", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            }
            items(petNotes) { item ->
                LocalMemoryCard(
                    item = item,
                    title = "Pet note",
                    description = "Animal/pet context. Not a human medical chart.",
                    containerColor = Color(0xFFEAF7ED),
                    onDelete = onDelete
                )
            }
        }

        if (syncItems.isNotEmpty()) {
            item {
                Text("Medical outbox", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            }
        }

        if (offlineItems.isEmpty()) {
            item {
                Card(
                    colors = CardDefaults.cardColors(containerColor = Color(0xFFEFF7FF)),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(Modifier.padding(14.dp)) {
                        Text("Nothing saved locally right now.", fontWeight = FontWeight.SemiBold)
                        Text("If Dolly is offline, messages and attachments you send will wait here.")
                    }
                }
            }
        }

        items(syncItems) { item ->
            val isFile = item.fileUri.isNotBlank()
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = if (item.status == "synced" || item.status == "sent_to_dolly") Color(0xFFEAF7ED) else Color(0xFFFFF6DD)
                ),
                shape = RoundedCornerShape(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(item.patientDisplayName, fontWeight = FontWeight.Bold)
                            Text(
                                when (item.status) {
                                    "sent_to_dolly" -> "Sent to Dolly"
                                    "synced" -> "Synced to desktop"
                                    else -> "Pending sync"
                                }
                            )
                        }
                        Button(onClick = { onDelete(item) }) { Text("Delete") }
                    }
                    Text(if (isFile) "${item.kind.replaceFirstChar { it.uppercase() }} attachment: ${item.fileName.ifBlank { "saved file" }}" else item.note)
                    if (isFile && item.note.isNotBlank()) {
                        Text(item.note)
                    }
                    Text(item.createdAt, style = MaterialTheme.typography.bodySmall)
                }
            }
        }
    }
}

@Composable
fun LocalMemoryCard(
    item: OfflineItem,
    title: String,
    description: String,
    containerColor: Color,
    onDelete: (OfflineItem) -> Unit
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = containerColor),
        shape = RoundedCornerShape(8.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(title, fontWeight = FontWeight.Bold)
                    Text(
                        if (item.status == "personal_vault_synced" || item.status == "pet_vault_synced") {
                            "$description Synced to desktop Personal Vault."
                        } else if (item.status == "lockbox_vault_synced") {
                            "$description Synced to trusted desktop Lockbox."
                        } else {
                            description
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = Color(0xFF52635E)
                    )
                }
                Button(onClick = { onDelete(item) }) { Text("Delete") }
            }
            Text(item.note, color = Color(0xFF1F2933))
            Text(item.createdAt, style = MaterialTheme.typography.bodySmall, color = Color(0xFF52635E))
        }
    }
}

private fun latestPacketSyncAt(packets: List<ChartPacket>): String {
    return packets
        .mapNotNull { packet ->
            runCatching { OffsetDateTime.parse(packet.createdAt) }.getOrNull()
        }
        .maxOrNull()
        ?.toString()
        ?: packets.map { it.createdAt }.filter { it.isNotBlank() }.maxOrNull().orEmpty()
}

private fun isOlderThanDays(value: String, days: Long): Boolean {
    val parsed = runCatching { OffsetDateTime.parse(value) }.getOrNull() ?: return false
    return parsed.isBefore(OffsetDateTime.now().minusDays(days))
}

@Composable
fun BiometricGateScreen(onUnlock: () -> Unit) {
    Surface(Modifier.fillMaxSize(), color = Color(0xFFF8FBFC)) {
        Column(
            Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text("VitaMR locked", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(8.dp))
            Text("Manager face/fingerprint unlock is required on this phone.")
            Spacer(Modifier.height(18.dp))
            Button(onClick = onUnlock) { Text("Unlock") }
        }
    }
}

@Composable
fun SettingsScreen(
    host: String,
    status: String,
    pairingCode: String,
    onHostChange: (String) -> Unit,
    onHealth: () -> Unit,
    onStartPairing: () -> Unit,
    onPair: (String) -> Unit,
    isPaired: Boolean,
    biometricEnabled: Boolean,
    personalCloudEnabled: Boolean,
    personalGeminiKeyConfigured: Boolean,
    personalGeminiKeyInput: String,
    personalCloudStatus: String,
    wirelessDebuggingEnabled: Boolean?,
    onEnableBiometric: () -> Unit,
    onDisableBiometric: () -> Unit,
    onPersonalGeminiKeyChange: (String) -> Unit,
    onSavePersonalGeminiKey: () -> Unit,
    onClearPersonalGeminiKey: () -> Unit,
    onTogglePersonalCloud: () -> Unit,
    onRefreshWirelessDebugging: () -> Unit,
    onOpenDeveloperOptions: () -> Unit
) {
    var code by remember { mutableStateOf("") }
    val wirelessDebuggingText = when (wirelessDebuggingEnabled) {
        true -> "On. Development installs can use Wi-Fi debugging."
        false -> "Off. Only needed when installing new test builds from the computer."
        null -> "Status not available on this phone. Open Developer Options if a test install is needed."
    }
    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(18.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text("Desktop connection", style = MaterialTheme.typography.titleLarge)
        OutlinedTextField(value = host, onValueChange = onHostChange, label = { Text("Desktop API URL") }, modifier = Modifier.fillMaxWidth())
        Text(status, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.SemiBold)
        Row {
            Button(onClick = onHealth) { Text("Test") }
            Spacer(Modifier.width(8.dp))
            Button(onClick = onStartPairing) { Text("Start pairing") }
        }
        if (pairingCode.isNotBlank()) Text("Desktop pairing code: $pairingCode")
        OutlinedTextField(value = code, onValueChange = { code = it }, label = { Text("Enter pairing code") }, modifier = Modifier.fillMaxWidth())
        Button(onClick = { onPair(code) }) { Text("Pair phone") }
        Spacer(Modifier.height(12.dp))
        Text("Manager biometric gate", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        Text("Available after manager-approved pairing. VitaMR uses Android face/fingerprint unlock and does not store face data.")
        if (biometricEnabled) {
            Button(onClick = onDisableBiometric) { Text("Disable biometric unlock") }
        } else {
            Button(onClick = onEnableBiometric, enabled = isPaired) { Text("Enable face/fingerprint unlock") }
        }
        Spacer(Modifier.height(12.dp))
        Text("Personal cloud replies", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        Text("For non-sensitive Personal Mode only. Medical, Lockbox, Pets, chart packets, and Data Hunter do not use this phone key.")
        Text(personalCloudStatus, fontWeight = FontWeight.SemiBold)
        Text(
            "Do not enter sensitive medical, financial, legal, password, identity, or deeply private information when Personal Cloud is on.",
            color = Color(0xFF6B4A45),
            style = MaterialTheme.typography.bodySmall
        )
        OutlinedTextField(
            value = personalGeminiKeyInput,
            onValueChange = onPersonalGeminiKeyChange,
            label = { Text(if (personalGeminiKeyConfigured) "Replace Personal Gemini key" else "Personal Gemini API key") },
            visualTransformation = PasswordVisualTransformation(),
            modifier = Modifier.fillMaxWidth()
        )
        Row {
            Button(onClick = onSavePersonalGeminiKey, enabled = personalGeminiKeyInput.isNotBlank()) { Text("Save key") }
            Spacer(Modifier.width(8.dp))
            Button(onClick = onClearPersonalGeminiKey, enabled = personalGeminiKeyConfigured) { Text("Clear key") }
        }
        Button(
            onClick = onTogglePersonalCloud,
            enabled = personalGeminiKeyConfigured || personalCloudEnabled
        ) {
            Text(if (personalCloudEnabled) "Turn Personal Cloud off" else "Turn Personal Cloud on")
        }
        Spacer(Modifier.height(12.dp))
        Text("Developer install helper", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        Text(wirelessDebuggingText)
        Row {
            Button(onClick = onRefreshWirelessDebugging) { Text("Refresh") }
            Spacer(Modifier.width(8.dp))
            Button(onClick = onOpenDeveloperOptions) { Text("Open Developer Options") }
        }
    }
}

private fun FragmentActivity.requestVitaMRBiometric(
    title: String,
    subtitle: String,
    onResult: (Boolean, String) -> Unit
) {
    val manager = BiometricManager.from(this)
    val canAuthenticate = manager.canAuthenticate(BiometricManager.Authenticators.BIOMETRIC_WEAK or BiometricManager.Authenticators.DEVICE_CREDENTIAL)
    if (canAuthenticate != BiometricManager.BIOMETRIC_SUCCESS) {
        onResult(false, "Biometric unlock is not available or not enrolled on this phone.")
        return
    }

    val promptInfo = BiometricPrompt.PromptInfo.Builder()
        .setTitle(title)
        .setSubtitle(subtitle)
        .setAllowedAuthenticators(BiometricManager.Authenticators.BIOMETRIC_WEAK or BiometricManager.Authenticators.DEVICE_CREDENTIAL)
        .build()
    val prompt = BiometricPrompt(
        this,
        ContextCompat.getMainExecutor(this),
        object : BiometricPrompt.AuthenticationCallback() {
            override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                onResult(true, "Manager biometric unlock confirmed.")
            }

            override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                onResult(false, errString.toString())
            }

            override fun onAuthenticationFailed() {
                onResult(false, "Face/fingerprint was not recognized by Android.")
            }
        }
    )
    prompt.authenticate(promptInfo)
}


