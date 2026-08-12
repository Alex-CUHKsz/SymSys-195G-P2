using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MurderVilla.Dialogue
{
    /// <summary>
    /// Singleton dialogue manager that drives the branching conversation UI.
    /// Supports simple responses, multi-round Q&amp;A chat bubbles, and a
    /// 30-second cooldown between conversations.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────
        public static DialogueManager Instance { get; private set; }
        public static bool IsDialogueOpen => Instance != null && Instance.state != DialogueState.Hidden;

        // ── State ──────────────────────────────────────────────────
        private DialogueState state = DialogueState.Hidden;
        private NPCDialogue currentSpeaker;
        private string characterName;
        private DialogueBranch[] branches;
        private int activeBranchIndex;
        private int currentQAIndex;
        private float stateEnterTime;

        // ── UI root objects ────────────────────────────────────────
        private Canvas canvas;
        private GameObject overlay;
        private GameObject mainPanel;
        private Text titleLabel;

        // Per-state containers
        private GameObject idleGroup;
        private Button startButton;

        private GameObject questionSelectGroup;
        private Button optionButtonA;
        private Button optionButtonB;

        private GameObject responseGroup;
        private Text responseText;
        private Button continueButton;

        private GameObject qaGroup;
        private RectTransform qaContentArea;
        private GameObject qaDetectiveBubble;
        private GameObject qaNpcBubble;
        private Button qaNextButton;
        private Text qaNextButtonLabel;

        private GameObject endedGroup;
        private Text endedLabel;

        private GameObject cooldownGroup;
        private Text cooldownLabel;
        private Text cooldownFlavorText;

        // ── Initialisation ─────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Instance != null) return;
            GameObject manager = new(nameof(DialogueManager));
            DontDestroyOnLoad(manager);
            manager.AddComponent<DialogueManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
        }

        // ── Update (cooldown timer) ────────────────────────────────
        private void Update()
        {
            // Per-character cooldown: tick the UI if we're showing it
            if (state == DialogueState.Cooldown && currentSpeaker != null)
            {
                if (!currentSpeaker.IsOnCooldown)
                {
                    SetState(DialogueState.Idle);
                }
                else
                {
                    int seconds = Mathf.CeilToInt(currentSpeaker.CooldownRemaining);
                    cooldownLabel.text = $"Cooldown: {seconds}s";
                }
            }
        }

        // ── Public entry point ─────────────────────────────────────
        public void Begin(NPCDialogue speaker)
        {
            if (speaker == null) return;

            // Per-character cooldown: this NPC is still cooling down
            if (speaker.IsOnCooldown)
            {
                canvas.gameObject.SetActive(true);
                SetPlayerInteraction(true);
                SetCursor(false);
                currentSpeaker = speaker;
                characterName = speaker.CharacterName;
                SetState(DialogueState.Cooldown);
                return;
            }

            currentSpeaker = speaker;
            characterName = speaker.CharacterName;
            branches = speaker.Branches;

            if (!speaker.IsMonologue)
                Investigation.SuspectInterviewLog.Instance?.RecordConversation(characterName);

            if (speaker.IsMonologue)
            {
                // Monologue mode: no branches needed
            }
            else if (branches == null || branches.Length == 0)
            {
                Debug.LogWarning($"{characterName} has no dialogue branches defined.");
                return;
            }

            canvas.gameObject.SetActive(true);
            SetPlayerInteraction(true);
            SetCursor(false);
            SetState(DialogueState.Idle);
        }

        public void Close()
        {
            if (state == DialogueState.Hidden) return;
            SetState(DialogueState.Hidden);
            canvas.gameObject.SetActive(false);
            currentSpeaker?.SetTalking(false);
            currentSpeaker = null;
            SetPlayerInteraction(false);
            SetCursor(true);
        }

        // ── State transitions ──────────────────────────────────────
        private void SetState(DialogueState newState)
        {
            state = newState;
            stateEnterTime = Time.unscaledTime;
            RefreshUi();
        }

        private void RefreshUi()
        {
            // Hide everything first
            idleGroup.SetActive(false);
            questionSelectGroup.SetActive(false);
            responseGroup.SetActive(false);
            qaGroup.SetActive(false);
            endedGroup.SetActive(false);
            cooldownGroup.SetActive(false);

            switch (state)
            {
                case DialogueState.Hidden:
                    break;

                case DialogueState.Idle:
                    titleLabel.text = characterName.ToUpperInvariant();
                    idleGroup.SetActive(true);
                    startButton.interactable = true;
                    break;

                case DialogueState.QuestionSelect:
                    titleLabel.text = characterName.ToUpperInvariant();
                    questionSelectGroup.SetActive(true);
                    // Populate the two option buttons
                    if (branches.Length >= 1)
                    {
                        optionButtonA.GetComponentInChildren<Text>().text = branches[0].questionText;
                        optionButtonA.gameObject.SetActive(true);
                    }
                    else optionButtonA.gameObject.SetActive(false);

                    if (branches.Length >= 2)
                    {
                        optionButtonB.GetComponentInChildren<Text>().text = branches[1].questionText;
                        optionButtonB.gameObject.SetActive(true);
                    }
                    else optionButtonB.gameObject.SetActive(false);
                    break;

                case DialogueState.ShowingResponse:
                    titleLabel.text = characterName.ToUpperInvariant();
                    responseGroup.SetActive(true);
                    if (currentSpeaker != null && currentSpeaker.IsMonologue)
                    {
                        responseText.text = currentSpeaker.MonologueText;
                        continueButton.GetComponentInChildren<Text>().text = "Close";
                    }
                    else
                    {
                        responseText.text = branches[activeBranchIndex].responseText;
                        continueButton.GetComponentInChildren<Text>().text = "Continue";
                    }
                    break;

                case DialogueState.QASequence:
                {
                    titleLabel.text = characterName.ToUpperInvariant();
                    qaGroup.SetActive(true);

                    var qa = branches[activeBranchIndex].qaSequence[currentQAIndex];

                    // Clear previous bubbles
                    foreach (Transform child in qaContentArea)
                        Destroy(child.gameObject);

                    // Detective bubble (left-aligned, gray)
                    var qBubble = Instantiate(qaDetectiveBubble, qaContentArea, false);
                    SetBubbleContent(qBubble, "Detective", qa.question,
                        new Color(0.22f, 0.24f, 0.28f), TextAnchor.MiddleLeft);
                    qBubble.SetActive(true);

                    // NPC bubble (right-aligned, blue)
                    var aBubble = Instantiate(qaNpcBubble, qaContentArea, false);
                    SetBubbleContent(aBubble, characterName, qa.answer,
                        new Color(0.10f, 0.18f, 0.30f), TextAnchor.MiddleRight);
                    aBubble.SetActive(true);

                    bool isLast = currentQAIndex >= branches[activeBranchIndex].qaSequence.Length - 1;
                    qaNextButtonLabel.text = isLast ? "End Conversation" : "Next";
                    break;
                }

                case DialogueState.Ended:
                    titleLabel.text = characterName.ToUpperInvariant();
                    endedGroup.SetActive(true);
                    if (currentSpeaker != null && currentSpeaker.IsMonologue)
                    {
                        endedLabel.text = "— End of testimony —";
                        var endBtn = endedGroup.transform.Find("Ended Continue");
                        if (endBtn != null)
                            endBtn.GetComponentInChildren<Text>().text = "Close";
                    }
                    else
                    {
                        endedLabel.text = "Conversation ended";
                    }
                    break;

                case DialogueState.Cooldown:
                    titleLabel.text = characterName.ToUpperInvariant();
                    cooldownGroup.SetActive(true);
                    cooldownLabel.text = $"Cooldown: {Mathf.CeilToInt(currentSpeaker != null ? currentSpeaker.CooldownRemaining : 0f)}s";
                    cooldownFlavorText.text = "\"Go ask someone else instead of pestering me nonstop. Why are you so suspicious of me anyway?\"";
                    startButton.interactable = false;
                    break;
            }
        }

        private static void SetBubbleContent(GameObject bubble, string speakerLabel,
            string message, Color bgColor, TextAnchor alignment)
        {
            // Background color
            var bg = bubble.transform.Find("Bg");
            if (bg != null)
                bg.GetComponent<Image>().color = bgColor;

            // Speaker label
            var label = bubble.transform.Find("Label");
            if (label != null)
            {
                var labelText = label.GetComponent<Text>();
                labelText.text = speakerLabel;
                labelText.alignment = alignment == TextAnchor.MiddleLeft
                    ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            }

            // Message text
            var msg = bubble.transform.Find("Text");
            if (msg != null)
            {
                var msgText = msg.GetComponent<Text>();
                msgText.text = message;
                msgText.alignment = alignment;
            }
        }

        // ── Button handlers ────────────────────────────────────────
        public void OnStartClicked()
        {
            if (state != DialogueState.Idle) return;
            if (currentSpeaker != null && currentSpeaker.IsMonologue)
            {
                // Monologue: skip question select, go straight to the text
                currentSpeaker.SetTalking(false);
                SetState(DialogueState.ShowingResponse);
                return;
            }
            SetState(DialogueState.QuestionSelect);
        }

        public void OnOptionA()
        {
            if (state != DialogueState.QuestionSelect) return;
            activeBranchIndex = 0;
            StartBranch();
        }

        public void OnOptionB()
        {
            if (state != DialogueState.QuestionSelect) return;
            if (branches.Length < 2) return;
            activeBranchIndex = 1;
            StartBranch();
        }

        private void StartBranch()
        {
            currentSpeaker?.SetTalking(true);

            var branch = branches[activeBranchIndex];
            if (branch.qaSequence != null && branch.qaSequence.Length > 0)
            {
                currentQAIndex = 0;
                SetState(DialogueState.QASequence);
            }
            else
            {
                SetState(DialogueState.ShowingResponse);
            }
        }

        public void OnContinueClicked()
        {
            if (state != DialogueState.ShowingResponse) return;
            // Conversation done → go to ended → cooldown
            GoToEnded();
        }

        public void OnQANextClicked()
        {
            if (state != DialogueState.QASequence) return;

            var branch = branches[activeBranchIndex];
            currentQAIndex++;

            if (currentQAIndex >= branch.qaSequence.Length)
            {
                GoToEnded();
            }
            else
            {
                RefreshUi(); // show next Q&A pair
            }
        }

        public void OnEndedContinueClicked()
        {
            if (state != DialogueState.Ended) return;
            if (currentSpeaker != null && currentSpeaker.IsMonologue)
            {
                // Monologue: no cooldown, just close
                Close();
                return;
            }
            // Start per-character cooldown
            currentSpeaker?.SetTalking(false);
            currentSpeaker?.StartCooldown();
            SetState(DialogueState.Cooldown);
        }

        private void GoToEnded()
        {
            currentSpeaker?.SetTalking(false);
            SetState(DialogueState.Ended);
        }

        // ── Input (keyboard shortcuts) ─────────────────────────────
        // We also support E to advance and Q to quit for convenience
        private void LateUpdate()
        {
            if (state == DialogueState.Hidden) return;

            // Debounce: ignore input for first 0.2s after state change
            if (Time.unscaledTime - stateEnterTime < 0.2f) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.qKey.wasPressedThisFrame)
            {
                if (state == DialogueState.Idle || state == DialogueState.Ended)
                {
                    Close();
                    return;
                }
                if (state == DialogueState.Cooldown)
                {
                    // Close UI but cooldown persists on the NPC
                    Close();
                    return;
                }
            }
        }

        // ── UI Construction ────────────────────────────────────────
        private void BuildUi()
        {
            // Canvas root
            var canvasGo = new GameObject("Dialogue Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dark overlay
            overlay = MakeUiObject("Overlay", canvasGo.transform);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.55f);
            Stretch(overlay.GetComponent<RectTransform>());

            // Main panel
            mainPanel = MakeUiObject("Main Panel", canvasGo.transform);
            var panelImg = mainPanel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.07f, 0.09f, 0.97f);
            var panelRect = mainPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.12f);
            panelRect.anchorMax = new Vector2(0.9f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Title
            titleLabel = MakeText(mainPanel.transform, "Title", 30, FontStyle.Bold,
                TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(600f, 52f));
            titleLabel.color = new Color(0.82f, 0.22f, 0.15f);

            BuildIdleGroup();
            BuildQuestionSelectGroup();
            BuildResponseGroup();
            BuildQAGroup();
            BuildEndedGroup();
            BuildCooldownGroup();

            canvasGo.SetActive(false);
        }

        private void BuildIdleGroup()
        {
            idleGroup = MakeUiObject("Idle Group", mainPanel.transform);
            idleGroup.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            idleGroup.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            idleGroup.GetComponent<RectTransform>().offsetMin = new Vector2(40f, 100f);
            idleGroup.GetComponent<RectTransform>().offsetMax = new Vector2(-40f, -80f);

            startButton = MakeButton(idleGroup.transform, "Start Button", "Start Conversation",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(340f, 68f));
            startButton.onClick.AddListener(OnStartClicked);
        }

        private void BuildQuestionSelectGroup()
        {
            questionSelectGroup = MakeUiObject("Question Select Group", mainPanel.transform);
            var r = questionSelectGroup.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(40f, 120f);
            r.offsetMax = new Vector2(-40f, -60f);

            optionButtonA = MakeButton(questionSelectGroup.transform, "Option A", "",
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
                new Vector2(0f, 0f), new Vector2(700f, 72f));
            optionButtonA.onClick.AddListener(OnOptionA);

            optionButtonB = MakeButton(questionSelectGroup.transform, "Option B", "",
                new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f),
                new Vector2(0f, 0f), new Vector2(700f, 72f));
            optionButtonB.onClick.AddListener(OnOptionB);
        }

        private void BuildResponseGroup()
        {
            responseGroup = MakeUiObject("Response Group", mainPanel.transform);
            var r = responseGroup.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(60f, 160f);
            r.offsetMax = new Vector2(-60f, -60f);

            responseText = MakeText(responseGroup.transform, "Response Text", 26, FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 0.3f), new Vector2(1f, 0.85f),
                new Vector2(40f, 0f), new Vector2(-40f, -10f));
            responseText.horizontalOverflow = HorizontalWrapMode.Wrap;
            responseText.verticalOverflow = VerticalWrapMode.Truncate;

            continueButton = MakeButton(responseGroup.transform, "Continue Btn", "Continue",
                new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f),
                new Vector2(0f, 0f), new Vector2(220f, 56f));
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        private void BuildQAGroup()
        {
            qaGroup = MakeUiObject("QA Group", mainPanel.transform);
            var r = qaGroup.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(20f, 80f);
            r.offsetMax = new Vector2(-20f, -60f);

            // Simple content area — shows one Q&A pair at a time
            qaContentArea = MakeUiObject("Content", qaGroup.transform).GetComponent<RectTransform>();
            qaContentArea.anchorMin = new Vector2(0f, 0.20f);
            qaContentArea.anchorMax = new Vector2(1f, 0.95f);
            qaContentArea.offsetMin = Vector2.zero;
            qaContentArea.offsetMax = Vector2.zero;

            var layoutGroup = qaContentArea.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.spacing = 14f;
            layoutGroup.padding = new RectOffset(16, 16, 8, 8);
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            // Create bubble templates (inactive, stored outside content area)
            qaDetectiveBubble = MakeBubble("Detective Bubble", qaGroup.transform,
                new Color(0.22f, 0.24f, 0.28f), TextAnchor.MiddleLeft,
                "Detective");
            qaDetectiveBubble.SetActive(false);

            qaNpcBubble = MakeBubble("NPC Bubble", qaGroup.transform,
                new Color(0.12f, 0.20f, 0.32f), TextAnchor.MiddleRight,
                "NPC");
            qaNpcBubble.SetActive(false);

            // Next / End button
            qaNextButton = MakeButton(qaGroup.transform, "QA Next Btn", "Next",
                new Vector2(0.5f, 0.04f), new Vector2(0.5f, 0.04f),
                new Vector2(0f, 0f), new Vector2(260f, 56f));
            qaNextButtonLabel = qaNextButton.GetComponentInChildren<Text>();
            qaNextButton.onClick.AddListener(OnQANextClicked);
        }

        private static GameObject MakeBubble(string name, Transform parent,
            Color bgColor, TextAnchor alignment, string speakerLabel)
        {
            var go = MakeUiObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 110f);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 90f;
            layout.preferredHeight = 110f;

            // Background
            var bgGo = MakeUiObject("Bg", go.transform);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = bgColor;

            // Speaker label
            var labelAlign = alignment == TextAnchor.MiddleLeft
                ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            var label = MakeText(go.transform, "Label", 15, FontStyle.Bold,
                labelAlign,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -6f), new Vector2(-18f, 22f));
            label.color = new Color(0.55f, 0.55f, 0.58f);
            label.text = speakerLabel;

            // Message text
            var text = MakeText(go.transform, "Text", 21, FontStyle.Normal,
                alignment,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(22f, 10f), new Vector2(-22f, -30f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return go;
        }

        private void BuildEndedGroup()
        {
            endedGroup = MakeUiObject("Ended Group", mainPanel.transform);
            var r = endedGroup.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(40f, 150f);
            r.offsetMax = new Vector2(-40f, -60f);

            endedLabel = MakeText(endedGroup.transform, "Ended Label", 28, FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(0f, 0f), new Vector2(500f, 50f));
            endedLabel.color = new Color(0.7f, 0.7f, 0.7f);

            var endBtn = MakeButton(endedGroup.transform, "Ended Continue", "Continue",
                new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f),
                new Vector2(0f, 0f), new Vector2(220f, 56f));
            endBtn.onClick.AddListener(OnEndedContinueClicked);
        }

        private void BuildCooldownGroup()
        {
            cooldownGroup = MakeUiObject("Cooldown Group", mainPanel.transform);
            var r = cooldownGroup.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(40f, 150f);
            r.offsetMax = new Vector2(-40f, -60f);

            cooldownLabel = MakeText(cooldownGroup.transform, "Cooldown Label", 30, FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f),
                new Vector2(0f, 0f), new Vector2(400f, 60f));
            cooldownLabel.color = new Color(0.55f, 0.55f, 0.55f);

            // Flavor text during cooldown
            cooldownFlavorText = MakeText(cooldownGroup.transform, "Flavor Text", 18, FontStyle.Italic,
                TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.52f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            cooldownFlavorText.color = new Color(0.45f, 0.45f, 0.50f);
            cooldownFlavorText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Disabled "Start Conversation" button
            var disabledBtn = MakeButton(cooldownGroup.transform, "Disabled Start", "Start Conversation",
                new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f),
                new Vector2(0f, 0f), new Vector2(340f, 68f));
            disabledBtn.interactable = false;
            var btnColors = disabledBtn.colors;
            btnColors.disabledColor = new Color(0.15f, 0.15f, 0.18f);
            disabledBtn.colors = btnColors;
        }

        // ── UI helpers ─────────────────────────────────────────────
        private static GameObject MakeUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text MakeText(Transform parent, string name, int size,
            FontStyle style, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = MakeUiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = MakeUiObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.24f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.20f, 0.24f);
            colors.highlightedColor = new Color(0.28f, 0.30f, 0.35f);
            colors.pressedColor = new Color(0.12f, 0.13f, 0.16f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.14f);
            btn.colors = colors;

            // Button text
            var btnTextGo = MakeUiObject("Text", go.transform);
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 22;
            btnText.fontStyle = FontStyle.Normal;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = new Color(0.9f, 0.9f, 0.9f);
            btnText.raycastTarget = false;
            btnText.text = label;

            var textRect = btnTextGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            return btn;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ── Player / cursor helpers ────────────────────────────────
        private static void SetCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private static void SetPlayerInteraction(bool interacting)
        {
            foreach (var behaviour in
                     FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var type = behaviour.GetType();
                if (type.FullName != "FpsHorrorKit.FpsController") continue;
                var field = type.GetField("isInteracting");
                field?.SetValue(behaviour, interacting);
                break;
            }
        }
    }
}
