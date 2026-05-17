using System.Net.Http;
using System.Text;
using System.Text.Json;
using VitaMR.Models;

namespace VitaMR.Services;

public sealed class OllamaGemmaActionPacketService : IGemmaActionPacketService
{
    private const string DollyVoiceContract =
        "Dolly voice contract for any user-facing text:\n" +
        "- Sound like a calm, capable health-record partner, not an engineer or schema validator.\n" +
        "- Keep most replies to 1 or 2 short sentences unless the user asks for detail.\n" +
        "- Use plain words: say chart, file, update, background check, or safe review instead of packet, schema, route, backend, payload, validation, or tool.\n" +
        "- Be warm without being cutesy. Do not over-apologize, over-explain, or narrate internal mechanics.\n" +
        "- For progress text, say what is happening and what stayed protected.\n" +
        "- For unclear requests, ask one direct question and offer the safest next step.\n" +
        "- For medical boundaries, be steady and brief: explain that you can organize stored information, but cannot diagnose or direct care.\n\n";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer_question",
        "query_chart",
        "fetch_original_source",
        "show_roster",
        "set_active_patient",
        "delete_patient_chart",
        "review_chart_cleanup",
        "run_task_board",
        "run_dream_runner",
        "run_specialty_weaver",
        "run_symptom_scan",
        "run_drug_scan",
        "run_pre_visit_brief",
        "refresh_working_summary",
        "store_patient_update",
        "store_vaccine_record",
        "store_user_reminder",
        "request_patient_photo",
        "edit_chart",
        "clarify_patient",
        "small_talk",
        "unknown"
    };

    private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "task_board",
        "dream_runner",
        "specialty_weaver",
        "symptom_scan",
        "drug_scan",
        "pre_visit_brief",
        "working_summary",
        "original_source"
    };

    private static readonly HashSet<string> AllowedResponseOwners = new(StringComparer.OrdinalIgnoreCase)
    {
        "local_agent",
        "backend",
        "orchestrator"
    };

    private static readonly HashSet<string> AllowedDisplayModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "chat",
        "chart_answer",
        "status",
        "tool_result",
        "confirmation",
        "original_source"
    };

    private readonly HttpClient _httpClient = new();

    public async Task<GemmaActionPacketResult> BuildPacketAsync(
        string endpoint,
        string modelName,
        string userText,
        string conversationContext,
        string localSessionContext,
        string activePatientDisplayName,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var previousRaw = string.Empty;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var prompt = attempt == 1
                ? BuildPacketPrompt(userText, conversationContext, localSessionContext, activePatientDisplayName, knownPatients)
                : BuildRepairPrompt(userText, previousRaw, errors, activePatientDisplayName, knownPatients);

            var raw = await AskGemmaAsync(endpoint, modelName, prompt, cancellationToken);
            previousRaw = raw;

            var result = TryValidate(raw, knownPatients);
            result.Attempts = attempt;
            result.RawResponse = raw;

            if (result.WasValid)
            {
                return result;
            }

            errors = result.Errors;
        }

        return new GemmaActionPacketResult
        {
            WasValid = false,
            Status = "INVALID_AFTER_REPAIR_ATTEMPTS",
            Errors = errors,
            RawResponse = previousRaw,
            Attempts = 3
        };
    }

    private async Task<string> AskGemmaAsync(
        string endpoint,
        string modelName,
        string prompt,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = modelName,
            prompt,
            stream = false,
            keep_alive = "45s",
            options = new
            {
                temperature = 0.1,
                num_predict = 700
            }
        };

        using var request = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            LaptopWorkerProtocol.BuildInferenceUri(endpoint),
            request,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemma action packet request failed with {(int)response.StatusCode}.");
        }

        return LaptopWorkerProtocol.ExtractModelResponseOrThrow(body);
    }

    private static string BuildPacketPrompt(
        string userText,
        string conversationContext,
        string localSessionContext,
        string activePatientDisplayName,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        return
            "You are Gemma, VitaMR's typed-chat intent router. The user typed a message into Dolly's omnibox.\n" +
            "Return exactly one JSON object. Do not include markdown, comments, or explanation outside JSON.\n\n" +
            "C# will validate this object and will reject anything outside the contract. You may suggest actions, but C# executes only allowlisted intents.\n\n" +
            "Allowed intents:\n" +
            "- answer_question\n- query_chart\n- fetch_original_source\n- show_roster\n- set_active_patient\n- delete_patient_chart\n- review_chart_cleanup\n- request_patient_photo\n- run_task_board\n- run_dream_runner\n- run_specialty_weaver\n- run_symptom_scan\n- run_drug_scan\n- run_pre_visit_brief\n- refresh_working_summary\n- store_patient_update\n- store_vaccine_record\n- store_user_reminder\n- edit_chart\n- clarify_patient\n- small_talk\n- unknown\n\n" +
            "Allowed tools: task_board, dream_runner, specialty_weaver, symptom_scan, drug_scan, pre_visit_brief, working_summary, original_source, or empty string.\n\n" +
            "Allowed response_owner values: local_agent, backend, orchestrator.\n" +
            "Allowed display_mode values: chat, chart_answer, status, tool_result, confirmation, original_source.\n\n" +
            "Required JSON shape:\n" +
            "{\"intent\":\"answer_question\",\"patient_display_name\":\"\",\"confidence\":0.0,\"requires_confirmation\":false,\"tool\":\"\",\"backend_needed\":false,\"response_owner\":\"local_agent\",\"clinical_payload_mutable\":true,\"display_mode\":\"chat\",\"interstitial_message\":\"\",\"reason\":\"\",\"user_facing_answer\":\"\",\"edit_action\":{\"operation\":\"\",\"target_type\":\"\",\"target_id\":\"\",\"target_hint\":\"\",\"new_status\":\"\",\"reason\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"vaccine_action\":{\"records\":[{\"vaccine_name\":\"\",\"date_given\":\"\",\"dose_or_series\":\"\",\"location_or_provider\":\"\",\"notes\":\"\"}],\"source_hint\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"delete_patient_action\":{\"patient_display_name\":\"\",\"target_hint\":\"\",\"reason\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"reminder_action\":{\"records\":[{\"reminder_text\":\"\",\"due_date\":\"\",\"related_patient_display_name\":\"\",\"priority\":\"routine\",\"notes\":\"\"}],\"requires_user_verification\":true,\"verification_prompt\":\"\"}}\n\n" +
            "C# exposed backend action map:\n" +
            "- review_chart_cleanup: variables none. Lists likely accidental/test charts and exact Chart Manager confirmation phrases. No file moves.\n" +
            "- delete_patient_chart: variables delete_patient_action.patient_display_name or target_hint. Requires exact user confirmation before C# moves a chart to the 7-day wastebasket.\n" +
            "- set_active_patient: variables patient_display_name or reason previous_chart/newest_chart. Switches focus only after C# validates one safe match.\n" +
            "- store_vaccine_record: variables vaccine_action.records. Requires confirmation before writing Vaccines.md.\n" +
            "- store_user_reminder: variables reminder_action.records. Requires confirmation before writing User_Reminders.md.\n" +
            "- edit_chart: variables edit_action operation/target/new_status. Requires confirmation before changing chart files.\n\n" +
            DollyVoiceContract +
            "Rules:\n" +
            "- patient_display_name must be empty or exactly one known patient display name.\n" +
            "- Use the active patient if the user says he/she/his/her/the patient or asks a chart question without naming someone.\n" +
            "- confidence must be a number from 0 to 1.\n" +
            "- Any store_patient_update intent must set requires_confirmation true.\n" +
            "- If the user says they had, got, received, or need to save a vaccine/immunization such as Tdap, flu, influenza, COVID, RSV, shingles, pneumococcal, MMR, varicella, hepatitis, HPV, or meningococcal, set intent store_vaccine_record, requires_confirmation true, and fill vaccine_action.records with one row per vaccine. vaccine_name and date_given are required when supplied. Use yyyy-MM-dd when possible. If the user gives only a year or year range, preserve it as 2005 or 2005-2008 instead of dropping the vaccine.\n" +
            "- If the user asks to cancel, close, resolve, complete, or change a pending chart item, set intent edit_chart, requires_confirmation true, and fill edit_action. Allowed edit operations are cancel_care_gap, resolve_care_gap, update_care_gap_status. target_type must be care_gap. new_status must be open, resolved, or canceled.\n" +
            "- If the user asks a medical/chart/wiki question that needs stored chart facts, set intent query_chart, backend_needed true, response_owner backend, clinical_payload_mutable false, and display_mode chart_answer.\n" +
            "- If the user asks to open, switch to, focus on, or go back to a patient/chart, set intent set_active_patient, response_owner orchestrator, display_mode status, backend_needed false. Use patient_display_name for a named patient.\n" +
            "- For 'go back', 'last patient', or 'previous chart', set set_active_patient and leave patient_display_name empty; put previous_chart in reason.\n" +
            "- For 'new chart', 'new patient', or 'show me the new chart', set set_active_patient and leave patient_display_name empty; put newest_chart in reason.\n" +
            "- If the user asks to delete, remove, archive, or trash a patient chart/file, set intent delete_patient_chart, response_owner orchestrator, display_mode confirmation, backend_needed false, requires_confirmation true, and fill delete_patient_action. C# will require the vault-owner confirmation phrase before moving anything.\n" +
            "- If the user asks to review, find, clean up, clean out, remove fake/test/accidental charts, or says chart cleanup without naming one exact patient, set intent review_chart_cleanup, response_owner orchestrator, display_mode tool_result, backend_needed false, requires_confirmation false. C# will list cleanup candidates and exact confirmation phrases; no files move from this intent.\n" +
            "- If the user asks to add, set, upload, change, or update a patient/chart photo, set intent request_patient_photo, response_owner orchestrator, display_mode confirmation, backend_needed false. Use patient_display_name when the patient is named.\n" +
            "- If the user asks for a future reminder, follow-up schedule, or check-back task, set intent store_user_reminder, requires_confirmation true, and fill reminder_action. Convert relative dates to yyyy-MM-dd using today's date from the context when available.\n" +
            "- For backend-owned chart answers, user_facing_answer may contain only a short progress acknowledgement. Do not answer the clinical question locally.\n" +
            "- If the user asks to fetch, show, open, return, or download a saved original/source document, image, photo, or PDF, set intent fetch_original_source, tool original_source, response_owner orchestrator, clinical_payload_mutable false, and display_mode original_source.\n" +
            "- Do not summarize original source files when the user asks to fetch the original. C# will attach the unchanged saved file.\n" +
            "- If the user asks for a supported tool, set the matching run_* intent and matching tool.\n" +
            "- If the user asks what patients/files exist, use show_roster.\n" +
            "- If the user asks what is happening or what task is running, use run_task_board.\n" +
            "- If answering directly, put the friendly answer in user_facing_answer using only supplied sterile context.\n" +
            "- For interstitial_message, write a short Dolly status line for the user, not an internal routing note.\n" +
            "- Do not give diagnosis, triage, prescriptions, orders, treatment instructions, or emergency instructions.\n" +
            "- Do not expose internal chart IDs, file paths, or task IDs unless the user explicitly asks for technical details.\n\n" +
            $"Active patient: {activePatientDisplayName}\n" +
            $"Known patients:\n{BuildKnownPatientList(knownPatients)}\n\n" +
            $"Recent conversation:\n{conversationContext}\n\n" +
            "Sterile local session context:\n" +
            "```markdown\n" +
            localSessionContext.Trim() +
            "\n```\n\n" +
            $"User message:\n{userText.Trim()}\n\n" +
            "Return JSON only.";
    }

    private static string BuildRepairPrompt(
        string userText,
        string previousRaw,
        IReadOnlyList<string> errors,
        string activePatientDisplayName,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        return
            "Your previous VitaMR action packet failed C# validation. Return corrected JSON only.\n\n" +
            $"Validation errors:\n- {string.Join("\n- ", errors)}\n\n" +
            "Allowed intents: answer_question, query_chart, fetch_original_source, show_roster, set_active_patient, delete_patient_chart, review_chart_cleanup, request_patient_photo, run_task_board, run_dream_runner, run_specialty_weaver, run_symptom_scan, run_drug_scan, run_pre_visit_brief, refresh_working_summary, store_patient_update, store_vaccine_record, store_user_reminder, edit_chart, clarify_patient, small_talk, unknown.\n" +
            "Allowed tools: task_board, dream_runner, specialty_weaver, symptom_scan, drug_scan, pre_visit_brief, working_summary, original_source, or empty string.\n" +
            "Allowed response_owner values: local_agent, backend, orchestrator. Allowed display_mode values: chat, chart_answer, status, tool_result, confirmation, original_source.\n" +
            "Required shape: {\"intent\":\"answer_question\",\"patient_display_name\":\"\",\"confidence\":0.0,\"requires_confirmation\":false,\"tool\":\"\",\"backend_needed\":false,\"response_owner\":\"local_agent\",\"clinical_payload_mutable\":true,\"display_mode\":\"chat\",\"interstitial_message\":\"\",\"reason\":\"\",\"user_facing_answer\":\"\",\"edit_action\":{\"operation\":\"\",\"target_type\":\"\",\"target_id\":\"\",\"target_hint\":\"\",\"new_status\":\"\",\"reason\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"vaccine_action\":{\"records\":[{\"vaccine_name\":\"\",\"date_given\":\"\",\"dose_or_series\":\"\",\"location_or_provider\":\"\",\"notes\":\"\"}],\"source_hint\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"delete_patient_action\":{\"patient_display_name\":\"\",\"target_hint\":\"\",\"reason\":\"\",\"requires_user_verification\":true,\"verification_prompt\":\"\"},\"reminder_action\":{\"records\":[{\"reminder_text\":\"\",\"due_date\":\"\",\"related_patient_display_name\":\"\",\"priority\":\"routine\",\"notes\":\"\"}],\"requires_user_verification\":true,\"verification_prompt\":\"\"}}\n" +
            "patient_display_name must be empty or exactly one known patient display name. confidence must be 0 to 1. store_patient_update and store_vaccine_record require confirmation. Chart/wiki medical questions must be backend-owned and immutable.\n\n" +
            DollyVoiceContract +
            $"Active patient: {activePatientDisplayName}\n" +
            $"Known patients:\n{BuildKnownPatientList(knownPatients)}\n\n" +
            $"User message:\n{userText.Trim()}\n\n" +
            "Previous response:\n" +
            previousRaw.Trim() +
            "\n\nReturn corrected JSON only.";
    }

    private static GemmaActionPacketResult TryValidate(
        string raw,
        IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var errors = new List<string>();
        GemmaActionPacket? packet = null;

        try
        {
            packet = JsonSerializer.Deserialize<GemmaActionPacket>(
                ExtractJsonObject(raw),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            errors.Add($"Response was not valid JSON: {exception.Message}");
        }

        if (packet is null)
        {
            return Invalid("INVALID_JSON", errors);
        }

        packet.Intent = (packet.Intent ?? string.Empty).Trim();
        packet.PatientDisplayName = (packet.PatientDisplayName ?? string.Empty).Trim();
        packet.Tool = (packet.Tool ?? string.Empty).Trim();
        packet.ResponseOwner = string.IsNullOrWhiteSpace(packet.ResponseOwner)
            ? (packet.BackendNeeded ? "backend" : "local_agent")
            : packet.ResponseOwner.Trim();
        packet.DisplayMode = string.IsNullOrWhiteSpace(packet.DisplayMode)
            ? (packet.ResponseOwner.Equals("backend", StringComparison.OrdinalIgnoreCase) ? "chart_answer" : "chat")
            : packet.DisplayMode.Trim();
        packet.InterstitialMessage = (packet.InterstitialMessage ?? string.Empty).Trim();
        packet.Reason = (packet.Reason ?? string.Empty).Trim();
        packet.UserFacingAnswer = (packet.UserFacingAnswer ?? string.Empty).Trim();
        NormalizeEditAction(packet.EditAction);
        NormalizeVaccineAction(packet.VaccineAction);
        NormalizeDeletePatientAction(packet.DeletePatientAction);
        NormalizeReminderAction(packet.ReminderAction);
        NormalizePatientDisplayName(packet, knownPatients);

        if (!AllowedIntents.Contains(packet.Intent))
        {
            errors.Add($"intent '{packet.Intent}' is not allowed.");
        }

        if (!AllowedTools.Contains(packet.Tool))
        {
            errors.Add($"tool '{packet.Tool}' is not allowed.");
        }

        if (!AllowedResponseOwners.Contains(packet.ResponseOwner))
        {
            errors.Add($"response_owner '{packet.ResponseOwner}' is not allowed.");
        }

        if (!AllowedDisplayModes.Contains(packet.DisplayMode))
        {
            errors.Add($"display_mode '{packet.DisplayMode}' is not allowed.");
        }

        if (packet.Confidence is < 0 or > 1 || double.IsNaN(packet.Confidence))
        {
            errors.Add("confidence must be a number from 0 to 1.");
        }

        if (!string.IsNullOrWhiteSpace(packet.PatientDisplayName) &&
            !knownPatients.Any(patient => patient.PatientDisplayName.Equals(packet.PatientDisplayName, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"patient_display_name '{packet.PatientDisplayName}' did not exactly match the sealed registry.");
        }

        if ((packet.Intent.Equals("store_patient_update", StringComparison.OrdinalIgnoreCase) ||
             packet.Intent.Equals("edit_chart", StringComparison.OrdinalIgnoreCase) ||
             packet.Intent.Equals("delete_patient_chart", StringComparison.OrdinalIgnoreCase) ||
             packet.Intent.Equals("store_user_reminder", StringComparison.OrdinalIgnoreCase) ||
             packet.Intent.Equals("store_vaccine_record", StringComparison.OrdinalIgnoreCase)) &&
            !packet.RequiresConfirmation)
        {
            errors.Add($"{packet.Intent} must set requires_confirmation true.");
        }

        if (packet.Intent.Equals("edit_chart", StringComparison.OrdinalIgnoreCase))
        {
            ValidateEditAction(packet.EditAction, errors);
        }

        if (packet.Intent.Equals("store_vaccine_record", StringComparison.OrdinalIgnoreCase))
        {
            ValidateVaccineAction(packet.VaccineAction, errors);
        }

        if (packet.Intent.Equals("delete_patient_chart", StringComparison.OrdinalIgnoreCase))
        {
            ValidateDeletePatientAction(packet.DeletePatientAction, errors);
        }

        if (packet.Intent.Equals("store_user_reminder", StringComparison.OrdinalIgnoreCase))
        {
            ValidateReminderAction(packet.ReminderAction, knownPatients, errors);
        }

        if (packet.Intent.Equals("query_chart", StringComparison.OrdinalIgnoreCase) ||
            packet.ResponseOwner.Equals("backend", StringComparison.OrdinalIgnoreCase) ||
            (packet.BackendNeeded && packet.Intent.Equals("answer_question", StringComparison.OrdinalIgnoreCase)))
        {
            packet.BackendNeeded = true;
            packet.ResponseOwner = "backend";
            packet.ClinicalPayloadMutable = false;
            packet.DisplayMode = "chart_answer";
        }

        if (packet.Intent.Equals("fetch_original_source", StringComparison.OrdinalIgnoreCase))
        {
            packet.Tool = "original_source";
            packet.BackendNeeded = false;
            packet.ResponseOwner = "orchestrator";
            packet.ClinicalPayloadMutable = false;
            packet.DisplayMode = "original_source";
        }

        if (RequiresAnswer(packet.Intent) && string.IsNullOrWhiteSpace(packet.UserFacingAnswer))
        {
            errors.Add("user_facing_answer is required for this intent.");
        }

        return errors.Count == 0
            ? new GemmaActionPacketResult
            {
                WasValid = true,
                Status = "VALID",
                Packet = packet
            }
            : Invalid("SCHEMA_VALIDATION_FAILED", errors, packet);
    }

    private static void NormalizeEditAction(ChartEditActionPacket? editAction)
    {
        if (editAction is null)
        {
            return;
        }

        editAction.Operation = (editAction.Operation ?? string.Empty).Trim();
        editAction.TargetType = (editAction.TargetType ?? string.Empty).Trim();
        editAction.TargetId = (editAction.TargetId ?? string.Empty).Trim();
        editAction.TargetHint = (editAction.TargetHint ?? string.Empty).Trim();
        editAction.NewStatus = (editAction.NewStatus ?? string.Empty).Trim();
        editAction.Reason = (editAction.Reason ?? string.Empty).Trim();
        editAction.VerificationPrompt = (editAction.VerificationPrompt ?? string.Empty).Trim();
    }

    private static void NormalizePatientDisplayName(GemmaActionPacket packet, IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        if (string.IsNullOrWhiteSpace(packet.PatientDisplayName))
        {
            return;
        }

        var requested = NormalizeName(packet.PatientDisplayName);
        var exact = knownPatients.FirstOrDefault(patient =>
            NormalizeName(patient.PatientDisplayName).Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            packet.PatientDisplayName = exact.PatientDisplayName;
            return;
        }

        var firstNameMatches = knownPatients
            .Where(patient => ExtractFirstName(patient.PatientDisplayName).Equals(requested, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (firstNameMatches.Count == 1)
        {
            packet.PatientDisplayName = firstNameMatches[0].PatientDisplayName;
        }
    }

    private static void ValidateEditAction(ChartEditActionPacket? editAction, ICollection<string> errors)
    {
        if (editAction is null)
        {
            errors.Add("edit_chart requires edit_action.");
            return;
        }

        var allowedOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cancel_care_gap",
            "resolve_care_gap",
            "update_care_gap_status"
        };
        var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "open",
            "resolved",
            "canceled"
        };

        if (!allowedOperations.Contains(editAction.Operation))
        {
            errors.Add($"edit_action.operation '{editAction.Operation}' is not allowed.");
        }

        if (!editAction.TargetType.Equals("care_gap", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("edit_action.target_type must be care_gap.");
        }

        if (string.IsNullOrWhiteSpace(editAction.TargetId) &&
            string.IsNullOrWhiteSpace(editAction.TargetHint))
        {
            errors.Add("edit_action requires target_id or target_hint.");
        }

        if (!allowedStatuses.Contains(editAction.NewStatus))
        {
            errors.Add("edit_action.new_status must be open, resolved, or canceled.");
        }

        if (!editAction.RequiresUserVerification)
        {
            errors.Add("edit_action.requires_user_verification must be true.");
        }
    }

    private static void NormalizeVaccineAction(VaccineActionPacket? vaccineAction)
    {
        if (vaccineAction is null)
        {
            return;
        }

        vaccineAction.SourceHint = (vaccineAction.SourceHint ?? string.Empty).Trim();
        vaccineAction.VerificationPrompt = (vaccineAction.VerificationPrompt ?? string.Empty).Trim();

        foreach (var record in vaccineAction.Records)
        {
            record.VaccineName = (record.VaccineName ?? string.Empty).Trim();
            record.DateGiven = (record.DateGiven ?? string.Empty).Trim();
            record.DoseOrSeries = (record.DoseOrSeries ?? string.Empty).Trim();
            record.LocationOrProvider = (record.LocationOrProvider ?? string.Empty).Trim();
            record.Notes = (record.Notes ?? string.Empty).Trim();
        }
    }

    private static void ValidateVaccineAction(VaccineActionPacket? vaccineAction, ICollection<string> errors)
    {
        if (vaccineAction is null)
        {
            errors.Add("store_vaccine_record requires vaccine_action.");
            return;
        }

        if (!vaccineAction.RequiresUserVerification)
        {
            errors.Add("vaccine_action.requires_user_verification must be true.");
        }

        if (vaccineAction.Records.Count == 0)
        {
            errors.Add("vaccine_action.records must contain at least one vaccine record.");
            return;
        }

        foreach (var record in vaccineAction.Records)
        {
            if (string.IsNullOrWhiteSpace(record.VaccineName))
            {
                errors.Add("each vaccine record requires vaccine_name.");
            }

            if (string.IsNullOrWhiteSpace(record.DateGiven))
            {
                errors.Add("each vaccine record requires date_given when the user supplied a date.");
            }
        }
    }

    private static void NormalizeDeletePatientAction(DeletePatientActionPacket? deleteAction)
    {
        if (deleteAction is null)
        {
            return;
        }

        deleteAction.PatientDisplayName = (deleteAction.PatientDisplayName ?? string.Empty).Trim();
        deleteAction.TargetHint = (deleteAction.TargetHint ?? string.Empty).Trim();
        deleteAction.Reason = (deleteAction.Reason ?? string.Empty).Trim();
        deleteAction.VerificationPrompt = (deleteAction.VerificationPrompt ?? string.Empty).Trim();
    }

    private static void ValidateDeletePatientAction(DeletePatientActionPacket? deleteAction, ICollection<string> errors)
    {
        if (deleteAction is null)
        {
            errors.Add("delete_patient_chart requires delete_patient_action.");
            return;
        }

        if (!deleteAction.RequiresUserVerification)
        {
            errors.Add("delete_patient_action.requires_user_verification must be true.");
        }

        if (string.IsNullOrWhiteSpace(deleteAction.PatientDisplayName) &&
            string.IsNullOrWhiteSpace(deleteAction.TargetHint))
        {
            errors.Add("delete_patient_action requires patient_display_name or target_hint.");
        }
    }

    private static void NormalizeReminderAction(ReminderActionPacket? reminderAction)
    {
        if (reminderAction is null)
        {
            return;
        }

        reminderAction.VerificationPrompt = (reminderAction.VerificationPrompt ?? string.Empty).Trim();
        foreach (var record in reminderAction.Records)
        {
            record.ReminderText = (record.ReminderText ?? string.Empty).Trim();
            record.DueDate = (record.DueDate ?? string.Empty).Trim();
            record.RelatedPatientDisplayName = (record.RelatedPatientDisplayName ?? string.Empty).Trim();
            record.Priority = string.IsNullOrWhiteSpace(record.Priority) ? "routine" : record.Priority.Trim();
            record.Notes = (record.Notes ?? string.Empty).Trim();
        }
    }

    private static void ValidateReminderAction(
        ReminderActionPacket? reminderAction,
        IReadOnlyList<PatientIdentityRecord> knownPatients,
        ICollection<string> errors)
    {
        if (reminderAction is null)
        {
            errors.Add("store_user_reminder requires reminder_action.");
            return;
        }

        if (!reminderAction.RequiresUserVerification)
        {
            errors.Add("reminder_action.requires_user_verification must be true.");
        }

        if (reminderAction.Records.Count == 0)
        {
            errors.Add("reminder_action.records must contain at least one reminder.");
            return;
        }

        foreach (var record in reminderAction.Records)
        {
            if (string.IsNullOrWhiteSpace(record.ReminderText))
            {
                errors.Add("each reminder record requires reminder_text.");
            }

            if (!DateOnly.TryParse(record.DueDate, out _))
            {
                errors.Add("each reminder record requires due_date in yyyy-MM-dd or parseable date format.");
            }

            if (!string.IsNullOrWhiteSpace(record.RelatedPatientDisplayName) &&
                !knownPatients.Any(patient => patient.PatientDisplayName.Equals(record.RelatedPatientDisplayName, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"reminder related_patient_display_name '{record.RelatedPatientDisplayName}' did not exactly match the sealed registry.");
            }
        }
    }

    private static bool RequiresAnswer(string intent)
    {
        return intent.Equals("answer_question", StringComparison.OrdinalIgnoreCase) ||
               intent.Equals("small_talk", StringComparison.OrdinalIgnoreCase) ||
               intent.Equals("clarify_patient", StringComparison.OrdinalIgnoreCase) ||
               intent.Equals("unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static GemmaActionPacketResult Invalid(
        string status,
        List<string> errors,
        GemmaActionPacket? packet = null)
    {
        return new GemmaActionPacketResult
        {
            WasValid = false,
            Status = status,
            Errors = errors,
            Packet = packet
        };
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        var fenceStart = trimmed.IndexOf("```", StringComparison.Ordinal);

        if (fenceStart >= 0)
        {
            var afterFence = trimmed[(fenceStart + 3)..].TrimStart();
            if (afterFence.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                afterFence = afterFence[4..].TrimStart();
            }

            var fenceEnd = afterFence.IndexOf("```", StringComparison.Ordinal);
            trimmed = fenceEnd >= 0 ? afterFence[..fenceEnd].Trim() : afterFence.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new JsonException("No JSON object found.");
        }

        return trimmed[start..(end + 1)];
    }

    private static string BuildKnownPatientList(IReadOnlyList<PatientIdentityRecord> knownPatients)
    {
        var names = knownPatients
            .Where(patient => !string.IsNullOrWhiteSpace(patient.PatientDisplayName))
            .Select(patient => patient.PatientDisplayName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count == 0
            ? "- none"
            : string.Join(Environment.NewLine, names.Select(name => $"- {name}"));
    }

    private static string ExtractFirstName(string value)
    {
        return NormalizeName(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"['’]s\b", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s'-]+", " ");
        return string.Join(
            ' ',
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
