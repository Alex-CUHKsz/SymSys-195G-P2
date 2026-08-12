using Investigation;
using UnityEngine;
using UnityEngine.UI;

namespace MurderVilla.InvestigationSystem
{
    /// <summary>
    /// The final accusation screen: three dropdowns (who drugged the milk,
    /// who killed Felix, how the room was locked), gated behind a minimum
    /// amount of evidence and suspect interviews, resolving to a win/lose
    /// ending panel. Ported from the case-board mechanic in the team's web
    /// prototype (lexi-main) — same three questions, same gating, same
    /// correct answer.
    /// </summary>
    public sealed class CaseVerdictUI : MonoBehaviour
    {
        public static CaseVerdictUI Instance { get; private set; }

        private Canvas canvas;
        private GameObject boardPanel;
        private Text statusLabel;
        private Dropdown druggerDropdown;
        private Dropdown killerDropdown;
        private Dropdown lockDropdown;
        private Button submitButton;
        private Text submitButtonLabel;

        private GameObject endingPanel;
        private Text endingTitle;
        private Text endingBody;
        private Button endingContinueButton;

        private Button openBoardButton;

        private static readonly SuspectId[] SuspectChoices =
        {
            SuspectId.None, SuspectId.Amy, SuspectId.Coco, SuspectId.Dean,
            SuspectId.Ben, SuspectId.Ella,
        };

        private const string GoodEnding =
            "Amy drugged the milk while Ella was away from the kitchen. Once Felix " +
            "lost consciousness, Coco went upstairs, strangled him with the curtain " +
            "cord, and let the bedroom's automatic lock create a false locked room. " +
            "The rustling newspaper was staged to disguise the true time of death.";

        private const string BadEnding =
            "The evidence doesn't support that conclusion. Revisit the milk cup, the " +
            "newspaper, and the automatic lock before accusing anyone.";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Build();
        }

        // ── Public entry point ─────────────────────────────────────
        public void OpenBoard()
        {
            RefreshStatus();
            boardPanel.SetActive(true);
            endingPanel.SetActive(false);
            SetCursor(true);
        }

        public void CloseBoard()
        {
            boardPanel.SetActive(false);
            SetCursor(false);
        }

        private void RefreshStatus()
        {
            int evidenceCount = EvidenceLog.Instance != null ? EvidenceLog.Instance.Collected.Count : 0;
            int suspectCount = SuspectInterviewLog.Instance != null ? SuspectInterviewLog.Instance.Count : 0;
            bool ready = CaseVerdict.MeetsRequirements(evidenceCount, suspectCount);

            statusLabel.text = $"Evidence {evidenceCount}/{CaseVerdict.MinEvidenceRequired}  ·  " +
                $"Suspects questioned {suspectCount}/{CaseVerdict.MinSuspectsRequired}";
            submitButton.interactable = ready;
            submitButtonLabel.text = ready
                ? "Submit Final Deduction"
                : "Question everyone and gather more evidence first";
        }

        private void OnSubmit()
        {
            SuspectId drugger = SuspectChoices[druggerDropdown.value];
            SuspectId killer = SuspectChoices[killerDropdown.value];
            LockMethod lockMethod = (LockMethod)(lockDropdown.value + 1);

            if (drugger == SuspectId.None || killer == SuspectId.None)
                return;

            bool correct = CaseVerdict.IsCorrect(drugger, killer, lockMethod);
            ShowEnding(correct);
        }

        private void ShowEnding(bool correct)
        {
            boardPanel.SetActive(false);
            endingPanel.SetActive(true);
            endingTitle.text = correct ? "The Complete Truth" : "The Deduction Contradicts the Evidence";
            endingBody.text = correct ? GoodEnding : BadEnding;
            endingContinueButton.GetComponentInChildren<Text>().text =
                correct ? "Case Closed" : "Return to Investigation";
        }

        private void OnEndingContinue()
        {
            endingPanel.SetActive(false);
            SetCursor(false);
        }

        private void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        // ── UI construction (matches HorrorVillaSceneBuilder's runtime-UI style) ──
        private void Build()
        {
            GameObject canvasObject = new("Case Verdict Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            openBoardButton = BuildOpenBoardButton(canvasObject.transform);
            boardPanel = BuildBoardPanel(canvasObject.transform);
            endingPanel = BuildEndingPanel(canvasObject.transform);

            boardPanel.SetActive(false);
            endingPanel.SetActive(false);
        }

        private Button BuildOpenBoardButton(Transform parent)
        {
            GameObject buttonObject = new("Open Case Board Button");
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.07f, 0.06f, 0.85f);
            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(OpenBoard);
            SetRect(buttonObject.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(180f, 42f));
            ((RectTransform)buttonObject.transform).pivot = new Vector2(1f, 1f);

            Text label = TextElement(buttonObject.transform, "Label", "Case Board [V]", 16,
                TextAnchor.MiddleCenter);
            StretchFull(label.rectTransform);
            return button;
        }

        private GameObject BuildBoardPanel(Transform parent)
        {
            GameObject backdrop = new("Case Board Panel");
            backdrop.transform.SetParent(parent, false);
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.75f);
            StretchFull((RectTransform)backdrop.transform);

            GameObject board = new("Board");
            board.transform.SetParent(backdrop.transform, false);
            Image boardImage = board.AddComponent<Image>();
            boardImage.color = new Color(0.11f, 0.1f, 0.09f, 0.97f);
            SetRect((RectTransform)board.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 520f));

            Text title = TextElement(board.transform, "Title", "RECONSTRUCT THE VILLA MURDER", 22,
                TextAnchor.UpperCenter);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -20f), new Vector2(0f, 34f));
            title.rectTransform.sizeDelta = new Vector2(-40f, 34f);

            statusLabel = TextElement(board.transform, "Status", string.Empty, 15,
                TextAnchor.UpperCenter);
            statusLabel.color = new Color(0.85f, 0.75f, 0.55f);
            SetRect(statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -58f), new Vector2(0f, 24f));
            statusLabel.rectTransform.sizeDelta = new Vector2(-40f, 24f);

            druggerDropdown = BuildQuestion(board.transform, -100f,
                "Who drugged the milk?", SuspectLabels());
            killerDropdown = BuildQuestion(board.transform, -190f,
                "Who strangled Felix?", SuspectLabels());
            lockDropdown = BuildQuestion(board.transform, -280f,
                "How was the locked room created?", new[]
                {
                    "The door locked automatically",
                    "The killer used a secret passage",
                    "Felix locked it from inside",
                });

            GameObject submitObject = new("Submit Button");
            submitObject.transform.SetParent(board.transform, false);
            Image submitImage = submitObject.AddComponent<Image>();
            submitImage.color = new Color(0.55f, 0.42f, 0.18f);
            submitButton = submitObject.AddComponent<Button>();
            submitButton.onClick.AddListener(OnSubmit);
            SetRect((RectTransform)submitObject.transform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 46f));
            submitButtonLabel = TextElement(submitObject.transform, "Label", "Submit Final Deduction",
                16, TextAnchor.MiddleCenter);
            StretchFull(submitButtonLabel.rectTransform);

            GameObject closeObject = new("Close Button");
            closeObject.transform.SetParent(board.transform, false);
            Image closeImage = closeObject.AddComponent<Image>();
            closeImage.color = new Color(0.3f, 0.3f, 0.3f);
            Button closeButton = closeObject.AddComponent<Button>();
            closeButton.onClick.AddListener(CloseBoard);
            SetRect((RectTransform)closeObject.transform, new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(28f, 28f));
            ((RectTransform)closeObject.transform).pivot = new Vector2(1f, 1f);
            Text closeLabel = TextElement(closeObject.transform, "Label", "X", 16,
                TextAnchor.MiddleCenter);
            StretchFull(closeLabel.rectTransform);

            return backdrop;
        }

        private Dropdown BuildQuestion(Transform parent, float yOffset, string question,
            string[] options)
        {
            Text label = TextElement(parent, question + " Label", question, 15,
                TextAnchor.UpperLeft);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, yOffset), new Vector2(0f, 24f));
            label.rectTransform.sizeDelta = new Vector2(-40f, 24f);

            GameObject dropdownObject = new(question + " Dropdown");
            dropdownObject.transform.SetParent(parent, false);
            Image dropdownImage = dropdownObject.AddComponent<Image>();
            dropdownImage.color = new Color(0.2f, 0.19f, 0.17f);
            Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
            SetRect((RectTransform)dropdownObject.transform, new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(20f, yOffset - 30f), new Vector2(0f, 30f));
            dropdownObject.GetComponent<RectTransform>().sizeDelta = new Vector2(-40f, 30f);

            Text captionText = TextElement(dropdownObject.transform, "Caption", string.Empty, 14,
                TextAnchor.MiddleLeft);
            SetRect(captionText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10f, 0f), new Vector2(-25f, 0f));
            dropdown.captionText = captionText;

            GameObject templateObject = new("Template");
            templateObject.transform.SetParent(dropdownObject.transform, false);
            templateObject.SetActive(false);
            Image templateImage = templateObject.AddComponent<Image>();
            templateImage.color = new Color(0.16f, 0.15f, 0.14f);
            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 26f * options.Length);
            templateObject.AddComponent<ScrollRect>();

            GameObject viewportObject = new("Viewport");
            viewportObject.transform.SetParent(templateObject.transform, false);
            viewportObject.AddComponent<Image>().color = Color.clear;
            viewportObject.AddComponent<Mask>().showMaskGraphic = false;
            StretchFull((RectTransform)viewportObject.transform);
            templateObject.GetComponent<ScrollRect>().viewport = (RectTransform)viewportObject.transform;

            GameObject content = new("Content");
            content.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>() ??
                content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 26f * options.Length);
            templateObject.GetComponent<ScrollRect>().content = contentRect;

            GameObject item = new("Item");
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0f, 26f);
            Toggle itemToggle = item.AddComponent<Toggle>();
            GameObject itemBackground = new("Item Background");
            itemBackground.transform.SetParent(item.transform, false);
            itemBackground.AddComponent<Image>().color = new Color(0.3f, 0.28f, 0.24f);
            StretchFull((RectTransform)itemBackground.transform);
            itemToggle.targetGraphic = itemBackground.GetComponent<Image>();

            GameObject itemCheckmark = new("Item Checkmark");
            itemCheckmark.transform.SetParent(item.transform, false);
            itemCheckmark.AddComponent<Image>().color = new Color(0.55f, 0.42f, 0.18f);
            SetRect((RectTransform)itemCheckmark.transform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(16f, 16f));
            itemToggle.graphic = itemCheckmark.GetComponent<Image>();

            Text itemLabel = TextElement(item.transform, "Item Label", string.Empty, 13,
                TextAnchor.MiddleLeft);
            SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(28f, 0f), new Vector2(-8f, 0f));

            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;

            dropdown.options.Clear();
            foreach (string option in options)
                dropdown.options.Add(new Dropdown.OptionData(option));
            dropdown.value = 0;
            dropdown.RefreshShownValue();

            return dropdown;
        }

        private GameObject BuildEndingPanel(Transform parent)
        {
            GameObject backdrop = new("Ending Panel");
            backdrop.transform.SetParent(parent, false);
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.03f, 0.02f, 0.02f, 0.94f);
            StretchFull((RectTransform)backdrop.transform);

            endingTitle = TextElement(backdrop.transform, "Ending Title", string.Empty, 30,
                TextAnchor.MiddleCenter);
            SetRect(endingTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(640f, 50f));

            endingBody = TextElement(backdrop.transform, "Ending Body", string.Empty, 17,
                TextAnchor.UpperCenter);
            endingBody.color = new Color(0.85f, 0.82f, 0.76f);
            SetRect(endingBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(640f, 140f));

            GameObject continueObject = new("Continue Button");
            continueObject.transform.SetParent(backdrop.transform, false);
            Image continueImage = continueObject.AddComponent<Image>();
            continueImage.color = new Color(0.55f, 0.42f, 0.18f);
            endingContinueButton = continueObject.AddComponent<Button>();
            endingContinueButton.onClick.AddListener(OnEndingContinue);
            SetRect((RectTransform)continueObject.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(260f, 44f));
            Text continueLabel = TextElement(continueObject.transform, "Label", "Continue", 16,
                TextAnchor.MiddleCenter);
            StretchFull(continueLabel.rectTransform);

            return backdrop;
        }

        private static string[] SuspectLabels()
        {
            return new[] { "Choose...", "Amy", "Coco", "Dean", "Ben", "Ella" };
        }

        private Text TextElement(Transform parent, string name, string text, int size,
            TextAnchor alignment)
        {
            GameObject target = new(name);
            target.transform.SetParent(parent, false);
            Text label = target.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.V) && !boardPanel.activeSelf &&
                !endingPanel.activeSelf && !MurderVilla.Dialogue.DialogueManager.IsDialogueOpen)
            {
                OpenBoard();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && boardPanel.activeSelf)
            {
                CloseBoard();
            }
        }
    }
}
