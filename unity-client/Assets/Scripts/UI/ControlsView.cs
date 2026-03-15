using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HijackPoker.Managers;

namespace HijackPoker.UI
{
    /// <summary>
    /// Bottom-center control bar with Next Step, Reset, Auto Play toggle,
    /// and Speed cycle buttons.
    /// </summary>
    public class ControlsView : MonoBehaviour
    {
        public event Action OnNextStep;
        public event Action OnReset;
        public event Action OnAutoPlayToggle;
        public event Action<int> OnTableConnect;

        private Button _nextStepButton;
        private Button _resetButton;
        private Button _autoPlayButton;
        private Button _speedButton;
        private Button _muteButton;
        private Button _connectButton;
        private TMP_InputField _tableIdInput;
        private TextMeshProUGUI _autoPlayLabel;
        private TextMeshProUGUI _speedLabel;
        private TextMeshProUGUI _muteLabel;
        private RectTransform _rt;

        private static readonly float[] SpeedOptions = { 0.25f, 0.5f, 1.0f, 2.0f };
        private int _speedIndex = 2; // default 1.0s

        public float CurrentSpeed => SpeedOptions[_speedIndex];

        public static ControlsView Create(Transform parent)
        {
            bool portrait = LayoutConfig.IsPortrait;
            float barHeight = LayoutConfig.ControlsBarHeight;
            float barPad = LayoutConfig.ControlsBarPadding;

            // Dark bar background — participates in parent VerticalLayoutGroup
            var barBg = UIFactory.CreatePanel("ControlsBar", parent,
                UIFactory.ControlsBarBg);
            var barLE = barBg.gameObject.AddComponent<LayoutElement>();
            barLE.preferredHeight = barHeight;
            barLE.flexibleWidth = 1;

            // Subtle top border on the bar
            var topLine = UIFactory.CreatePanel("TopLine", barBg,
                UIFactory.SubtleBorder);
            var topLineRt = topLine.GetComponent<RectTransform>();
            topLineRt.anchorMin = new Vector2(0, 1);
            topLineRt.anchorMax = new Vector2(1, 1);
            topLineRt.pivot = new Vector2(0.5f, 1);
            topLineRt.sizeDelta = new Vector2(0, 1);

            // Controls container — stretches to fill bar with padding
            var go = new GameObject("Controls", typeof(RectTransform));
            go.transform.SetParent(barBg, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(barPad, 0);
            rt.offsetMax = new Vector2(-barPad, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var view = go.AddComponent<ControlsView>();
            view._rt = rt;
            view.BuildUI();
            return view;
        }

        private void BuildUI()
        {
            Color btnBg = UIFactory.ButtonDefault;
            bool portrait = LayoutConfig.IsPortrait;
            float btnH = LayoutConfig.ControlsButtonHeight;
            float secH = LayoutConfig.ControlsSecondaryHeight;

            if (portrait)
                BuildPortraitLayout(btnBg, btnH, secH);
            else
                BuildLandscapeLayout(btnBg, btnH);
        }

        private void BuildPortraitLayout(Color btnBg, float btnH, float secH)
        {
            // Two-row layout using anchor-based positioning
            // Row 1 (top):    Next Step | Reset | Auto Play
            // Row 2 (bottom): Speed | SFX | sep | Table [input] Go
            var row1 = CreateRowContainer("Row1", 0.52f, 1.0f);
            var row2 = CreateRowContainer("Row2", 0.0f, 0.48f);

            // Row 1 — primary actions
            _nextStepButton = CreateAnchoredButton("NextStep", "Next Step", 17f,
                UIFactory.AccentCyan, UIFactory.Background, btnH,
                0.0f, 0.38f, row1);
            _nextStepButton.onClick.AddListener(() => OnNextStep?.Invoke());

            _resetButton = CreateAnchoredButton("Reset", "Reset", 16f,
                btnBg, UIFactory.TextSecondary, btnH,
                0.40f, 0.62f, row1);
            _resetButton.onClick.AddListener(() => OnReset?.Invoke());

            _autoPlayButton = CreateAnchoredButton("AutoPlay", "Auto", 15f,
                btnBg, UIFactory.TextPrimary, btnH,
                0.64f, 1.0f, row1);
            _autoPlayButton.onClick.AddListener(() => OnAutoPlayToggle?.Invoke());
            _autoPlayLabel = _autoPlayButton.GetComponentInChildren<TextMeshProUGUI>();

            // Row 2 — settings + table connection
            _speedButton = CreateAnchoredButton("Speed", "Delay: 1.0s", 14f,
                btnBg, UIFactory.TextSecondary, secH,
                0.0f, 0.26f, row2);
            _speedButton.onClick.AddListener(CycleSpeed);
            _speedLabel = _speedButton.GetComponentInChildren<TextMeshProUGUI>();

            _muteButton = CreateAnchoredButton("Mute", "SFX", 14f,
                btnBg, UIFactory.TextSecondary, secH,
                0.28f, 0.40f, row2);
            _muteLabel = _muteButton.GetComponentInChildren<TextMeshProUGUI>();
            _muteButton.onClick.AddListener(HandleMuteToggle);

            var sep = UIFactory.CreatePanel("Sep", row2,
                UIFactory.SeparatorColor, new Vector2(1, 32));
            var sepRt = sep.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0.42f, 0.5f);
            sepRt.anchorMax = new Vector2(0.42f, 0.5f);

            var tableLabel = UIFactory.CreateText("TableLabel", row2, "Table",
                13f, UIFactory.TextMuted, TextAlignmentOptions.Center);
            var tableLabelRt = tableLabel.GetComponent<RectTransform>();
            tableLabelRt.anchorMin = new Vector2(0.44f, 0f);
            tableLabelRt.anchorMax = new Vector2(0.56f, 1f);
            tableLabelRt.offsetMin = Vector2.zero;
            tableLabelRt.offsetMax = Vector2.zero;

            _tableIdInput = CreateAnchoredInputField("TableIdInput", row2, "1",
                0.57f, 0.78f, secH);

            _connectButton = CreateAnchoredButton("Connect", "Go", 14f,
                new Color(0.15f, 0.15f, 0.28f, 1f), UIFactory.AccentCyan, secH,
                0.80f, 1.0f, row2);
            _connectButton.onClick.AddListener(HandleConnect);
        }

        private void BuildLandscapeLayout(Color btnBg, float btnH)
        {
            // Single-row layout — anchor-based positioning for responsiveness
            // Uses anchor fractions across the container width:
            // [NextStep | Reset | Auto]  |  [Delay | SFX]  |  [Table __ Go]
            //  0.0                  0.42    0.46      0.60    0.64         1.0

            _nextStepButton = CreateAnchoredButton("NextStep", "Next Step", 16f,
                UIFactory.AccentCyan, UIFactory.Background, btnH,
                0.0f, 0.22f);
            _nextStepButton.onClick.AddListener(() => OnNextStep?.Invoke());

            _resetButton = CreateAnchoredButton("Reset", "Reset", 15f,
                btnBg, UIFactory.TextSecondary, btnH,
                0.23f, 0.37f);
            _resetButton.onClick.AddListener(() => OnReset?.Invoke());

            _autoPlayButton = CreateAnchoredButton("AutoPlay", "Auto", 14f,
                btnBg, UIFactory.TextPrimary, btnH,
                0.38f, 0.52f);
            _autoPlayButton.onClick.AddListener(() => OnAutoPlayToggle?.Invoke());
            _autoPlayLabel = _autoPlayButton.GetComponentInChildren<TextMeshProUGUI>();

            // Separator 1
            var sep1 = UIFactory.CreatePanel("Sep1", transform,
                UIFactory.SeparatorColor, new Vector2(1, 28));
            var sep1Rt = sep1.GetComponent<RectTransform>();
            sep1Rt.anchorMin = new Vector2(0.535f, 0.5f);
            sep1Rt.anchorMax = new Vector2(0.535f, 0.5f);

            _speedButton = CreateAnchoredButton("Speed", "Delay: 1.0s", 13f,
                btnBg, UIFactory.TextSecondary, btnH,
                0.55f, 0.71f);
            _speedButton.onClick.AddListener(CycleSpeed);
            _speedLabel = _speedButton.GetComponentInChildren<TextMeshProUGUI>();

            _muteButton = CreateAnchoredButton("Mute", "SFX", 13f,
                btnBg, UIFactory.TextSecondary, btnH,
                0.72f, 0.80f);
            _muteLabel = _muteButton.GetComponentInChildren<TextMeshProUGUI>();
            _muteButton.onClick.AddListener(HandleMuteToggle);

            // Separator 2
            var sep2 = UIFactory.CreatePanel("Sep2", transform,
                UIFactory.SeparatorColor, new Vector2(1, 28));
            var sep2Rt = sep2.GetComponent<RectTransform>();
            sep2Rt.anchorMin = new Vector2(0.815f, 0.5f);
            sep2Rt.anchorMax = new Vector2(0.815f, 0.5f);

            // Table label
            var tableLabel = UIFactory.CreateText("TableLabel", transform, "Table",
                13f, UIFactory.TextMuted, TextAlignmentOptions.Center);
            var tableLabelRt = tableLabel.GetComponent<RectTransform>();
            tableLabelRt.anchorMin = new Vector2(0.83f, 0f);
            tableLabelRt.anchorMax = new Vector2(0.88f, 1f);
            tableLabelRt.offsetMin = Vector2.zero;
            tableLabelRt.offsetMax = Vector2.zero;

            // Table ID input — anchored
            _tableIdInput = CreateAnchoredInputField("TableIdInput", transform, "1",
                0.885f, 0.94f, btnH);

            // Go button — anchored
            _connectButton = CreateAnchoredButton("Connect", "Go", 14f,
                new Color(0.15f, 0.15f, 0.28f, 1f), UIFactory.AccentCyan, btnH,
                0.945f, 1.0f);
            _connectButton.onClick.AddListener(HandleConnect);
        }

        /// <summary>
        /// Creates a button that positions itself using horizontal anchors
        /// within the parent, making it scale with container width.
        /// </summary>
        private Button CreateAnchoredButton(string name, string label, float fontSize,
            Color bgColor, Color textColor, float height,
            float anchorLeft, float anchorRight, Transform parent = null)
        {
            var btn = UIFactory.CreateButton(name, parent ?? transform, label, fontSize,
                bgColor, textColor, new Vector2(100, height));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorLeft, 0.5f);
            rt.anchorMax = new Vector2(anchorRight, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Height from sizeDelta, width from anchors
            rt.offsetMin = new Vector2(2, -height / 2f);
            rt.offsetMax = new Vector2(-2, height / 2f);
            return btn;
        }

        /// <summary>
        /// Creates a row container stretching horizontally, occupying a vertical
        /// slice of the parent defined by anchor Y range.
        /// </summary>
        private RectTransform CreateRowContainer(string name, float anchorYMin, float anchorYMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, anchorYMin);
            rt.anchorMax = new Vector2(1, anchorYMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// Creates an input field positioned using horizontal anchors.
        /// </summary>
        private TMP_InputField CreateAnchoredInputField(string name, Transform parent,
            string defaultValue, float anchorLeft, float anchorRight, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorLeft, 0.5f);
            rt.anchorMax = new Vector2(anchorRight, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(2, -height / 2f);
            rt.offsetMax = new Vector2(-2, height / 2f);

            var bg = go.AddComponent<Image>();
            bg.color = UIFactory.InputFieldBg;

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(go.transform, false);
            viewportGo.AddComponent<RectMask2D>();
            var vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(6, 2);
            vpRt.offsetMax = new Vector2(-6, -2);

            var textTmp = UIFactory.CreateText("Text", viewportGo.transform, "",
                14f, UIFactory.TextPrimary, TextAlignmentOptions.Center);
            UIFactory.StretchFill(textTmp.GetComponent<RectTransform>());

            var placeholder = UIFactory.CreateText("Placeholder", viewportGo.transform,
                "ID", 14f, UIFactory.TextMuted, TextAlignmentOptions.Center,
                FontStyles.Italic);
            UIFactory.StretchFill(placeholder.GetComponent<RectTransform>());

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = vpRt;
            input.textComponent = textTmp;
            input.placeholder = placeholder;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.text = defaultValue;
            input.targetGraphic = bg;

            return input;
        }

        private void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % SpeedOptions.Length;
            _speedLabel.text = $"Delay: {SpeedOptions[_speedIndex]:0.0#}s";
        }

        private void HandleConnect()
        {
            string text = _tableIdInput?.text;
            if (string.IsNullOrEmpty(text)) return;
            if (int.TryParse(text, out int tableId) && tableId > 0)
                OnTableConnect?.Invoke(tableId);
        }

        private void HandleMuteToggle()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.ToggleMute();
            _muteLabel.text = audio.IsMuted ? "MUTE" : "SFX";
            _muteLabel.color = audio.IsMuted ? UIFactory.TextMuted : UIFactory.TextSecondary;
        }

        public void SetAutoPlayActive(bool active)
        {
            _autoPlayLabel.text = active ? "Pause" : "Auto";

            // Change Next Step label to indicate pause behavior during auto-play
            var nextStepLabel = _nextStepButton.GetComponentInChildren<TextMeshProUGUI>();
            if (nextStepLabel != null)
                nextStepLabel.text = active ? "Pause" : "Next Step";

            var img = _autoPlayButton.GetComponent<Image>();
            img.color = active
                ? UIFactory.AutoPlayActiveBg
                : UIFactory.ButtonDefault;
        }

        public void SetInteractable(bool interactable)
        {
            _nextStepButton.interactable = interactable;
            _resetButton.interactable = interactable;
            _autoPlayButton.interactable = interactable;
        }
    }
}
