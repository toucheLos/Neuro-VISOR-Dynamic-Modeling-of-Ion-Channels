using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using C2M2.Interaction;
using C2M2.NeuronalDynamics.Simulation;
using C2M2.NeuronalDynamics.Interaction;
using C2M2.Utils;

namespace C2M2.NeuronalDynamics.Interaction.UI
{
    /// <summary>
    /// World-space experiment panel. Place on a Canvas child of the simulation GameObject (same as IonChannelConfig).
    /// Allows setting injected current timing/amplitude, simulation duration, and CSV output before running.
    /// </summary>
    public class ExperimentPanel : MonoBehaviour
    {
        // ── Experiment parameters ──────────────────────────────────────────────
        public double injDelay     = 0.05;    // seconds
        public double injDuration  = 0.10;    // seconds
        public double injAmplitude = 0.15e-11; // Amps
        public double simDuration  = 1.0;     // seconds
        public bool   csvEnabled   = false;

        // ── Step sizes for [−]/[+] buttons ────────────────────────────────────
        private const double DelayStep     = 0.01;
        private const double DurationStep  = 0.01;
        private const double AmplitudeStep = 0.5e-12;
        private const double SimDurStep    = 0.1;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color BtnColor      = new Color(0.20f, 0.45f, 0.80f);
        private static readonly Color RunColor      = new Color(0.18f, 0.75f, 0.18f);
        private static readonly Color CsvOnColor    = new Color(0.18f, 0.75f, 0.18f);
        private static readonly Color CsvOffColor   = new Color(0.70f, 0.70f, 0.70f);

        // ── Internal state ────────────────────────────────────────────────────
        private SparseSolverTestv1 solver;
        private Transform listParent;
        private bool uiBuilt = false;

        // Value labels updated by +/- buttons
        private TextMeshProUGUI lblDelay, lblDuration, lblAmplitude, lblSimDur;
        private Image csvBtnImage;

        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            solver = GameManager.instance?.activeSims?.Count > 0
                ? GameManager.instance.activeSims[0] as SparseSolverTestv1
                : null;

            if (!uiBuilt) BuildUI();
            RefreshLabels();
        }

        private void Update()
        {
            // Re-fetch solver if neuron was respawned
            if (solver == null && GameManager.instance?.activeSims?.Count > 0)
            {
                solver = GameManager.instance.activeSims[0] as SparseSolverTestv1;
                RefreshLabels();
            }
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            Transform bg = transform.Find("Background");
            if (bg == null) { Debug.LogError("ExperimentPanel: no 'Background' child."); return; }

            // Remove any pre-existing list
            Transform existing = bg.Find("ExpList");
            if (existing != null) Destroy(existing.gameObject);

            GameObject listGO = new GameObject("ExpList");
            listGO.transform.SetParent(bg, false);

            RectTransform listRect = listGO.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.5f, 1f);
            listRect.anchorMax = new Vector2(0.5f, 1f);
            listRect.pivot     = new Vector2(0.5f, 1f);
            listRect.anchoredPosition = new Vector2(-5f, -45f);
            listRect.sizeDelta = new Vector2(290f, 340f);

            VerticalLayoutGroup vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(8, 8, 4, 4);

            listParent = listGO.transform;

            // Neuron name (read-only)
            AddReadOnlyRow("Neuron", solver != null ? solver.name : "—");

            // Parameter rows
            lblDelay     = AddParamRow("Inj Start",   injDelay.ToString("F3")   + " s",
                () => { injDelay     += DelayStep;     RefreshLabels(); },
                () => { injDelay      = System.Math.Max(0, injDelay - DelayStep); RefreshLabels(); });

            lblDuration  = AddParamRow("Inj Duration", injDuration.ToString("F3")  + " s",
                () => { injDuration  += DurationStep;  RefreshLabels(); },
                () => { injDuration   = System.Math.Max(0, injDuration - DurationStep); RefreshLabels(); });

            lblAmplitude = AddParamRow("Inj Strength", FormatAmp(injAmplitude),
                () => { injAmplitude += AmplitudeStep; RefreshLabels(); },
                () => { injAmplitude  = System.Math.Max(0, injAmplitude - AmplitudeStep); RefreshLabels(); });

            lblSimDur    = AddParamRow("Sim Duration", simDuration.ToString("F2")  + " s",
                () => { simDuration  += SimDurStep;    RefreshLabels(); },
                () => { simDuration   = System.Math.Max(0.1, simDuration - SimDurStep); RefreshLabels(); });

            // CSV toggle
            AddToggleRow("CSV Output", CsvOffColor, ref csvBtnImage,
                () => { csvEnabled = !csvEnabled; csvBtnImage.color = csvEnabled ? CsvOnColor : CsvOffColor; });

            // Ion Channels button
            AddActionRow("Ion Channels", BtnColor, OpenIonChannelPanel);

            // Run button
            AddActionRow("RUN", RunColor, OnRunPressed);

            uiBuilt = true;
        }

        // ── Row builders ──────────────────────────────────────────────────────

        private void AddReadOnlyRow(string labelText, string valueText)
        {
            GameObject rowGO = MakeRow(labelText + "_RO");

            TextMeshProUGUI lbl = MakeLabel(rowGO.transform, labelText + ":", 17f);
            lbl.color = new Color(0.8f, 0.8f, 0.8f);

            TextMeshProUGUI val = MakeLabel(rowGO.transform, valueText, 17f);
            val.color = Color.white;
        }

        /// <summary>Returns the value TextMeshProUGUI so the caller can update it later.</summary>
        private TextMeshProUGUI AddParamRow(string labelText, string initialValue,
            System.Action onPlus, System.Action onMinus)
        {
            GameObject rowGO = MakeRow(labelText + "_Row");

            MakeLabel(rowGO.transform, labelText + ":", 16f).color = new Color(0.8f, 0.8f, 0.8f);

            TextMeshProUGUI valLabel = MakeLabel(rowGO.transform, initialValue, 16f);
            valLabel.color = Color.white;

            MakeSmallButton(rowGO.transform, "−", BtnColor, onMinus);
            MakeSmallButton(rowGO.transform, "+", BtnColor, onPlus);

            return valLabel;
        }

        private void AddToggleRow(string labelText, Color offColor, ref Image imageOut,
            System.Action onPress)
        {
            GameObject rowGO = MakeRow(labelText + "_Toggle");
            MakeLabel(rowGO.transform, labelText + ":", 16f).color = new Color(0.8f, 0.8f, 0.8f);
            imageOut = MakeSmallButton(rowGO.transform, csvEnabled ? "ON" : "OFF", offColor, onPress);
        }

        private void AddActionRow(string label, Color color, System.Action onPress)
        {
            GameObject rowGO = MakeRow(label + "_Action");
            LayoutElement le = rowGO.AddComponent<LayoutElement>();
            le.minHeight = 36f;
            MakeButton(rowGO.transform, label, color, onPress);
        }

        // ── Generic widget builders ───────────────────────────────────────────

        private GameObject MakeRow(string name)
        {
            GameObject rowGO = new GameObject(name);
            rowGO.transform.SetParent(listParent, false);
            rowGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

            HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(2, 2, 2, 2);
            return rowGO;
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string text, float fontSize)
        {
            GameObject go = new GameObject("Lbl_" + text);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private const float SmallBtnSize = 22f;

        private Image MakeSmallButton(Transform parent, string label, Color color, System.Action onPress)
        {
            GameObject btnGO = new GameObject("Btn_" + label);
            btnGO.transform.SetParent(parent, false);

            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(SmallBtnSize, SmallBtnSize);

            LayoutElement le = btnGO.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth  = SmallBtnSize;
            le.minHeight = le.preferredHeight = SmallBtnSize;
            le.flexibleWidth = 0f;

            Image img = btnGO.AddComponent<Image>();
            img.color = color;

            btnGO.layer = LayerMask.NameToLayer("Raycast");
            BoxCollider col = btnGO.AddComponent<BoxCollider>();
            col.size = new Vector3(SmallBtnSize, SmallBtnSize, 1f);

            RaycastPressEvents pressEvents = btnGO.AddComponent<RaycastPressEvents>();
            btnGO.AddComponent<RaycastEventManager>().LRTrigger = pressEvents;
            pressEvents.OnPress.AddListener(_ => onPress());

            // Label on button
            GameObject lblGO = new GameObject("BtnLbl");
            lblGO.transform.SetParent(btnGO.transform, false);
            RectTransform lblRt = lblGO.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return img;
        }

        private void MakeButton(Transform parent, string label, Color color, System.Action onPress)
        {
            GameObject btnGO = new GameObject("Btn_" + label);
            btnGO.transform.SetParent(parent, false);

            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 32f);

            LayoutElement le = btnGO.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth  = 200f;
            le.minHeight = le.preferredHeight = 32f;

            Image img = btnGO.AddComponent<Image>();
            img.color = color;

            btnGO.layer = LayerMask.NameToLayer("Raycast");
            BoxCollider col = btnGO.AddComponent<BoxCollider>();
            col.size = new Vector3(200f, 32f, 1f);

            RaycastPressEvents pressEvents = btnGO.AddComponent<RaycastPressEvents>();
            btnGO.AddComponent<RaycastEventManager>().LRTrigger = pressEvents;
            pressEvents.OnPress.AddListener(_ => onPress());

            GameObject lblGO = new GameObject("BtnLbl");
            lblGO.transform.SetParent(btnGO.transform, false);
            RectTransform lblRt = lblGO.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnRunPressed()
        {
            if (solver == null)
            {
                Debug.LogWarning("ExperimentPanel: no solver found — spawn a neuron first.");
                return;
            }

            // Push timing parameters to all placed electrodes
            if (solver.injectedCurrentManager != null)
            {
                lock (solver.injectedCurrentLock)
                {
                    foreach (var e in solver.injectedCurrentManager.electrodes)
                    {
                        if (e == null) continue;
                        e.Delay     = injDelay;
                        e.Duration  = injDuration;
                        e.Amplitude = injAmplitude;
                    }
                }
            }

            // Set simulation duration
            solver.endTime = simDuration;

            // Arm CSV if requested
            if (csvEnabled)
            {
                if (solver.csv == null)
                {
                    CSVWriter writer = solver.gameObject.AddComponent<CSVWriter>();
                    solver.csv = writer;
                    solver.convert = false;
                }
            }
            else
            {
                // If CSV was previously armed, finalize it
                if (solver.csv != null)
                    solver.convert = true;
            }

            solver.RestartSimulation();
        }

        private void OpenIonChannelPanel()
        {
            // Find the IonChannelConfig panel in the scene and toggle it
            C2M2.NeuronalDynamics.IonChannels.IonChannelConfig[] results = Resources.FindObjectsOfTypeAll<C2M2.NeuronalDynamics.IonChannels.IonChannelConfig>();
            C2M2.NeuronalDynamics.IonChannels.IonChannelConfig ionPanel = results.Length > 0 ? results[0] : null;
            if (ionPanel != null)
                ionPanel.gameObject.SetActive(!ionPanel.gameObject.activeSelf);
            else
                Debug.LogWarning("ExperimentPanel: IonChannelConfig panel not found in scene.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshLabels()
        {
            if (lblDelay     != null) lblDelay.text     = injDelay.ToString("F3")    + " s";
            if (lblDuration  != null) lblDuration.text  = injDuration.ToString("F3") + " s";
            if (lblAmplitude != null) lblAmplitude.text = FormatAmp(injAmplitude);
            if (lblSimDur    != null) lblSimDur.text    = simDuration.ToString("F2")  + " s";
            if (csvBtnImage  != null) csvBtnImage.color = csvEnabled ? CsvOnColor : CsvOffColor;
        }

        private static string FormatAmp(double amp)
        {
            // Show in pA for readability (1 pA = 1e-12 A)
            return (amp * 1e12).ToString("F2") + " pA";
        }
    }
}
